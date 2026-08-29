using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using REGOwintergarden.Knx;
using REGOwintergarden.Model;

namespace REGOwintergarden.App;

/// <summary>Wie es um die Verbindung zum Bus steht.</summary>
public enum Busstand
{
    Getrennt,
    Verbinde,
    Verbunden,
    Fehler,
}

/// <summary>Eine Zeile im Protokoll.</summary>
public sealed class Protokollzeile
{
    public Protokollzeile(string was, string dazu, bool problem = false)
    {
        Zeit = DateTime.Now;
        Was = was;
        Dazu = dazu;
        Problem = problem;
    }

    public DateTime Zeit { get; }
    public string Was { get; }
    public string Dazu { get; }
    public bool Problem { get; }

    public string Uhrzeit => Zeit.ToString("HH:mm:ss", CultureInfo.CurrentCulture);

    public override string ToString() =>
        Zeit.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.CurrentCulture) + "  " + Was + ": " + Dazu;
}

/// <summary>
/// Der Dienst: Bus, Wetter, Automatik und Protokoll.
///
/// Hier steckt alles, was an Netz und Zeit haengt, waehrend Antriebe, Regeln
/// und Sonnenstand reine Logik bleiben und sich ohne Gateway pruefen lassen.
/// Dieselbe Klasse laeuft in der Oberflaeche und im Windows-Dienst - der
/// Unterschied ist nur, wer sie startet.
/// </summary>
public sealed class Wintergartendienst : IAsyncDisposable
{
    private readonly object _schloss = new();
    private readonly Dictionary<GroupAddress, Messwert> _bus = new();
    private readonly Automatik _automatik = new();
    private readonly Zeitschaltuhr _uhr = new();
    private readonly Wetterabruf _wetterabruf = new();
    private readonly string _ordner;

    private KnxTunnelClient? _client;
    private CancellationTokenSource? _lauf;
    private DateTime _letzteVorhersage = DateTime.MinValue;

    public Wintergartendienst(Einstellungen einstellungen, string ordner)
    {
        Einstellungen = einstellungen;
        _ordner = ordner;
        Directory.CreateDirectory(ordner);

        if (einstellungen.Projektdatei.Length > 0) ProjektLaden(einstellungen.Projektdatei, out _);
    }

    public Einstellungen Einstellungen { get; }

    public Anlage Anlage => Einstellungen.Anlage;

    public Busstand Stand { get; private set; } = Busstand.Getrennt;

    /// <summary>Die zuletzt berechnete Lage je Antrieb - fuer die Uebersicht.</summary>
    public IReadOnlyList<Lage> Lagen { get; private set; } = Array.Empty<Lage>();

    /// <summary>Der Sonnenstand, wie er zuletzt galt.</summary>
    public Sonnenstand Sonne { get; private set; } = new(180, 0, null, null);

    /// <summary>Woher Azimut und Elevation kommen - Station oder Rechnung.</summary>
    public string Sonnenquelle { get; private set; } = "gerechnet";

    /// <summary>Die Gruppenadressen aus dem geladenen KNX-Projekt.</summary>
    public IReadOnlyList<GroupAddressEntry> Adresspool { get; private set; } = Array.Empty<GroupAddressEntry>();

    public string Projektquelle { get; private set; } = "";

    public event Action<Busstand, string?>? StandGeaendert;

    public event Action<Protokollzeile>? Protokolliert;

    /// <summary>Es hat sich etwas getan, das die Anzeige angeht.</summary>
    public event Action? Aufgefrischt;

    /// <summary>Ob die Automatik gerade laeuft.</summary>
    public bool Laeuft => _lauf is not null;

    // ---- Bus ---------------------------------------------------------------

    public static bool TryGateway(string? text, out IPEndPoint? ziel)
    {
        ziel = null;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var s = text.Trim();
        if (!s.Contains(':')) s += ":3671";
        return IPEndPoint.TryParse(s, out ziel);
    }

    public async Task VerbindenAsync(string gateway)
    {
        await TrennenAsync().ConfigureAwait(false);

        if (!TryGateway(gateway, out var ziel))
        {
            Stand = Busstand.Fehler;
            StandGeaendert?.Invoke(Stand, "Erwartet IP:Port, etwa 192.168.1.10:3671");
            return;
        }

        Stand = Busstand.Verbinde;
        StandGeaendert?.Invoke(Stand, null);
        try
        {
            var client = await KnxTunnelClient.ConnectAsync(ziel!, new IPEndPoint(IPAddress.Any, 0))
                .ConfigureAwait(false);
            client.TelegramReceived += AufTelegramm;
            lock (_schloss) _client = client;

            Stand = Busstand.Verbunden;
            StandGeaendert?.Invoke(Stand, client.IndividualAddress.ToString());
            Melden("verbunden", gateway + ", eigene Adresse " + client.IndividualAddress);

            await AbfragenAsync().ConfigureAwait(false);
        }
        catch (KnxException ex)
        {
            Stand = Busstand.Fehler;
            StandGeaendert?.Invoke(Stand, ex.Message);
            Melden("nicht verbunden", ex.Message, true);
        }
    }

    public async Task TrennenAsync()
    {
        KnxTunnelClient? client;
        lock (_schloss)
        {
            client = _client;
            _client = null;
        }
        if (client is null) return;

        client.TelegramReceived -= AufTelegramm;
        try { await client.DisconnectAsync().ConfigureAwait(false); }
        catch (KnxException) { }
        client.Dispose();

        Stand = Busstand.Getrennt;
        StandGeaendert?.Invoke(Stand, null);
    }

    private void AufTelegramm(BusTelegram telegramm)
    {
        // Antworten auf Leseanforderungen zaehlen mit: nach dem Verbinden
        // kommt der halbe Zustand der Anlage genau so herein.
        if (telegramm.IsConfirmation) return;
        if (telegramm.Service == ApciService.GroupValueRead) return;
        lock (_schloss) _bus[telegramm.Destination] = new Messwert(0, DateTime.Now);
        Merken(telegramm);
        Aufgefrischt?.Invoke();
    }

    private readonly Dictionary<GroupAddress, Payload> _rohwerte = new();

    private void Merken(BusTelegram telegramm)
    {
        lock (_schloss) _rohwerte[telegramm.Destination] = telegramm.Payload;
    }

    /// <summary>Der zuletzt gesehene Rohwert einer Adresse.</summary>
    public Payload? Roh(string adresse)
    {
        if (string.IsNullOrWhiteSpace(adresse)) return null;
        GroupAddress ziel;
        try { ziel = GroupAddress.Parse3Level(adresse.Trim()); }
        catch (KnxException) { return null; }

        lock (_schloss) return _rohwerte.TryGetValue(ziel, out var wert) ? wert : null;
    }

    /// <summary>Wann auf dieser Adresse zuletzt etwas kam.</summary>
    public DateTime? Zeitpunkt(string adresse)
    {
        if (string.IsNullOrWhiteSpace(adresse)) return null;
        GroupAddress ziel;
        try { ziel = GroupAddress.Parse3Level(adresse.Trim()); }
        catch (KnxException) { return null; }

        lock (_schloss) return _bus.TryGetValue(ziel, out var wert) ? wert.Zeit : null;
    }

    /// <summary>Fragt alle eingetragenen Adressen ab - mit Luft dazwischen.</summary>
    public async Task<int> AbfragenAsync()
    {
        KnxTunnelClient? client;
        lock (_schloss) client = _client;
        if (client is null)
        {
            Melden("nicht abgefragt", "keine Verbindung zum Bus", true);
            return 0;
        }

        var gefragt = new HashSet<GroupAddress>();
        foreach (var adresse in AlleAdressen())
        {
            GroupAddress ziel;
            try { ziel = GroupAddress.Parse3Level(adresse.Trim()); }
            catch (KnxException) { continue; }
            if (!gefragt.Add(ziel)) continue;

            try { await client.SendReadAsync(ziel).ConfigureAwait(false); }
            catch (KnxException) { }

            // Kurz Luft lassen: auf jede Leseanforderung antwortet ein Geraet,
            // und ohne Pause stehen doppelt so viele Telegramme gleichzeitig
            // auf der Leitung, wie der Tunnel vertraegt.
            await Task.Delay(30).ConfigureAwait(false);
        }

        Melden("abgefragt", gefragt.Count.ToString(CultureInfo.CurrentCulture) + " Adressen");
        return gefragt.Count;
    }

    private IEnumerable<string> AlleAdressen()
    {
        var anlage = Anlage;
        foreach (var adresse in new[]
                 {
                     anlage.AdresseRegen, anlage.AdresseWindalarm, anlage.AdresseWind,
                     anlage.AdresseAussen, anlage.AdresseInnen,
                     anlage.AdresseHellOst, anlage.AdresseHellSued, anlage.AdresseHellWest,
                     anlage.AdresseAzimut, anlage.AdresseElevation,
                 })
        {
            if (adresse.Length > 0) yield return adresse;
        }

        foreach (var motor in anlage.Motoren)
        {
            if (motor.AdressePositionStatus.Length > 0) yield return motor.AdressePositionStatus;
            if (motor.AdresseLamelleStatus.Length > 0) yield return motor.AdresseLamelleStatus;
        }
    }

    // ---- Wetter ------------------------------------------------------------

    /// <summary>
    /// Die Wetterlage, wie sie sich aus den Telegrammen ergibt.
    ///
    /// Jeder Wert bekommt den Zeitpunkt mit, zu dem er kam - ohne den waere
    /// eine ausgefallene Wetterstation nicht von Windstille zu unterscheiden.
    /// </summary>
    public Wetterlage Wetter()
    {
        var anlage = Anlage;
        return new Wetterlage
        {
            Regen = Lies(anlage.AdresseRegen, "1.001"),
            Windalarm = Lies(anlage.AdresseWindalarm, "1.001"),
            Wind = Lies(anlage.AdresseWind, "9.005"),
            Aussen = Lies(anlage.AdresseAussen, "9.001"),
            Innen = Lies(anlage.AdresseInnen, "9.001"),
            HellOst = Lies(anlage.AdresseHellOst, "9.004"),
            HellSued = Lies(anlage.AdresseHellSued, "9.004"),
            HellWest = Lies(anlage.AdresseHellWest, "9.004"),
            Azimut = Lies(anlage.AdresseAzimut, "14.007"),
            Elevation = Lies(anlage.AdresseElevation, "14.007"),
        };
    }

    private Messwert? Lies(string adresse, string dpt)
    {
        var roh = Roh(adresse);
        var zeit = Zeitpunkt(adresse);
        if (roh is null || zeit is null) return null;

        var wert = Zahl(dpt, roh);
        return wert is null ? null : new Messwert(wert.Value, zeit.Value);
    }

    /// <summary>
    /// Aus den Bytes eine Zahl. Bewusst hier und nicht im ValueCodec: der
    /// liefert Text zum Anzeigen, gerechnet wird aber mit Zahlen, und ein
    /// „21,5 °C" laesst sich nicht mit einer Grenze vergleichen.
    /// </summary>
    public static double? Zahl(string dpt, Payload payload)
    {
        try
        {
            return ValueCodec.MainNumber(dpt) switch
            {
                1 => Dpt.Dpt1Decode(payload) ? 1 : 0,
                5 => payload.IsSmall || payload.Bytes.Length != 1 ? null : payload.Bytes[0] / 255.0 * 100.0,
                7 => Dpt.Dpt7Decode(payload),
                9 => Dpt.Dpt9Decode(payload),
                12 or 13 => null,
                14 => Dpt.Dpt14Decode(payload),
                _ => null,
            };
        }
        catch (KnxException)
        {
            return null;
        }
    }

    /// <summary>
    /// Der Sonnenstand: von der Station, wenn sie ihn frisch liefert, sonst
    /// gerechnet.
    ///
    /// Die Station misst nicht, sie rechnet auch - aber sie kennt ihren
    /// Standort genauer als eine von Hand eingetragene Koordinate. Auf- und
    /// Untergang kommen in jedem Fall aus der eigenen Rechnung: die meldet
    /// keine Station.
    /// </summary>
    public Sonnenstand Sonnenstand(DateTime jetzt)
    {
        var anlage = Anlage;
        var gerechnet = Astro.Berechnen(jetzt, anlage.Breite, anlage.Laenge);

        var wetter = Wetter();
        var azimut = wetter.Azimut;
        var elevation = wetter.Elevation;
        var frisch = azimut is not null && elevation is not null
                     && azimut.Value.IstFrisch(jetzt, TimeSpan.FromMinutes(15))
                     && elevation.Value.IstFrisch(jetzt, TimeSpan.FromMinutes(15));

        if (!frisch)
        {
            Sonnenquelle = "gerechnet fuer " + anlage.Ort;
            return gerechnet;
        }

        Sonnenquelle = "von der Wetterstation";
        return gerechnet with { Azimut = azimut!.Value.Wert, Elevation = elevation!.Value.Wert };
    }

    // ---- Automatik ---------------------------------------------------------

    /// <summary>Startet den Rechentakt.</summary>
    public void Starten()
    {
        if (_lauf is not null) return;
        _lauf = new CancellationTokenSource();
        _ = Schleife(_lauf.Token);
        Melden("Automatik", "gestartet");
    }

    public void Anhalten()
    {
        _lauf?.Cancel();
        _lauf?.Dispose();
        _lauf = null;
        Melden("Automatik", "angehalten");
    }

    private async Task Schleife(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await TaktAsync(DateTime.Now, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is KnxException or IOException or InvalidOperationException)
            {
                // Ein Fehler in einem Takt darf die Automatik nicht beenden.
                // Ein Wintergarten, dessen Steuerung nach der ersten Stoerung
                // stillsteht, ist schlimmer als einer ohne Steuerung: dort
                // weiss wenigstens jeder, dass er von Hand fahren muss.
                Melden("Takt gestoert", ex.Message, true);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(Anlage.TaktSekunden, 5, 300)), ct)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Ein Rechendurchlauf. Oeffentlich, damit die Pruefung ihn mit einer
    /// eigenen Uhr aufrufen kann.
    /// </summary>
    public async Task TaktAsync(DateTime jetzt, CancellationToken ct = default)
    {
        var anlage = Anlage;
        Sonne = Sonnenstand(jetzt);

        if (Einstellungen.VorhersageHolen && anlage.VorhersageAktiv
            && jetzt - _letzteVorhersage > TimeSpan.FromMinutes(30))
        {
            _letzteVorhersage = jetzt;
            var sicht = await _wetterabruf.HolenAsync(anlage.Breite, anlage.Laenge, jetzt, ct)
                .ConfigureAwait(false);
            if (sicht is not null)
            {
                anlage.Vorhersage = sicht;
                Melden("Vorhersage", sicht.ToString());
            }
        }

        var wetter = Wetter();
        var lagen = _automatik.Bewerten(anlage, wetter, Sonne, jetzt);
        Lagen = lagen;

        if (anlage.AutomatikAktiv)
        {
            foreach (var lage in lagen)
            {
                if (lage.Ziel is null) continue;
                await AusfuehrenAsync(lage, jetzt).ConfigureAwait(false);
            }

            foreach (var faellig in _uhr.Faellige(anlage, Sonne, jetzt))
            {
                var merker = _automatik.Merker(faellig.Motor.Id);
                if (merker.HandBis is { } bis && bis > jetzt) continue;

                await FahrenAsync(faellig.Motor, faellig.Zeit.Position, null).ConfigureAwait(false);
                merker.Gesendet = faellig.Zeit.Position;
                merker.Gefahren = jetzt;
                Melden("Zeitschaltuhr", faellig.ToString());
            }
        }

        Aufgefrischt?.Invoke();
    }

    private async Task AusfuehrenAsync(Lage lage, DateTime jetzt)
    {
        var merker = _automatik.Merker(lage.Motor.Id);

        // Nur bei Aenderung senden - und nicht oefter als die Mindestpause.
        // Sonst schickt die Anlage bei jedem Takt dasselbe Telegramm, sobald
        // ein Messwert um eine Schwelle pendelt.
        if (merker.Gesendet is { } zuletzt && Math.Abs(zuletzt - lage.Ziel!.Value) < 0.5) return;
        if (merker.Gefahren is { } gefahren
            && jetzt - gefahren < TimeSpan.FromSeconds(Anlage.MindestpauseSekunden))
        {
            return;
        }

        await FahrenAsync(lage.Motor, lage.Ziel!.Value, lage.Lamelle).ConfigureAwait(false);
        merker.Gesendet = lage.Ziel;
        merker.Gefahren = jetzt;
        Melden(lage.Motor.Name, lage.Stufe + ": " + lage.Grund + " → "
                                + lage.Ziel.Value.ToString("0", CultureInfo.CurrentCulture) + " %");
    }

    // ---- Fahren ------------------------------------------------------------

    /// <summary>
    /// Faehrt einen Antrieb auf eine Position. Ohne Handsperre - das ist der
    /// Weg, den die Automatik nimmt.
    /// </summary>
    public async Task<bool> FahrenAsync(Motor motor, double position, double? lamelle)
    {
        var gut = await SendenAsync(motor.AdressePosition, "5.001",
            Math.Clamp(position, 0, 100).ToString("0", CultureInfo.CurrentCulture)).ConfigureAwait(false);

        if (lamelle is not null && motor.AdresseLamelle.Length > 0)
        {
            await SendenAsync(motor.AdresseLamelle, "5.001",
                Math.Clamp(lamelle.Value, 0, 100).ToString("0", CultureInfo.CurrentCulture))
                .ConfigureAwait(false);
        }
        return gut;
    }

    /// <summary>
    /// Ein Handgriff aus der Oberflaeche: faehrt und haelt danach die
    /// Automatik zurueck.
    ///
    /// Ohne diese Sperre faehrt die Automatik im naechsten Takt zurueck, und
    /// der Anwender haelt das Programm fuer kaputt - dabei tut es genau das,
    /// was eingestellt ist.
    /// </summary>
    public async Task<bool> VonHandAsync(Motor motor, double position, double? lamelle = null)
    {
        var jetzt = DateTime.Now;
        _automatik.VonHand(motor, jetzt, Anlage.Handsperre);

        var merker = _automatik.Merker(motor.Id);
        merker.Gesendet = position;
        merker.Gefahren = jetzt;

        var gut = await FahrenAsync(motor, position, lamelle).ConfigureAwait(false);
        Melden(motor.Name, "von Hand auf " + position.ToString("0", CultureInfo.CurrentCulture)
                           + " % - Automatik pausiert " + Anlage.HandsperreMinuten
                               .ToString("0", CultureInfo.CurrentCulture) + " min");
        Aufgefrischt?.Invoke();
        return gut;
    }

    /// <summary>Auf, Ab oder Stopp - fuer die Knoepfe in der Oberflaeche.</summary>
    public async Task<bool> BefehlAsync(Motor motor, string was)
    {
        var jetzt = DateTime.Now;
        _automatik.VonHand(motor, jetzt, Anlage.Handsperre);
        _automatik.Merker(motor.Id).Gesendet = null;

        var gut = was switch
        {
            "auf" => await SendenAsync(motor.AdresseFahren, "1.008", "aus").ConfigureAwait(false),
            "ab" => await SendenAsync(motor.AdresseFahren, "1.008", "ein").ConfigureAwait(false),
            _ => await SendenAsync(motor.AdresseStopp, "1.007", "aus").ConfigureAwait(false),
        };
        Melden(motor.Name, "von Hand: " + was);
        Aufgefrischt?.Invoke();
        return gut;
    }

    private async Task<bool> SendenAsync(string adresse, string dpt, string wert)
    {
        if (string.IsNullOrWhiteSpace(adresse))
        {
            Melden("nicht gesendet", "fuer diesen Schritt ist keine Adresse eingetragen", true);
            return false;
        }

        KnxTunnelClient? client;
        lock (_schloss) client = _client;
        if (client is null)
        {
            Melden("nicht gesendet", "keine Verbindung zum Bus", true);
            return false;
        }

        var nutzwert = ValueCodec.Encode(dpt, wert, out var fehler);
        if (nutzwert is null)
        {
            Melden("nicht gesendet", fehler, true);
            return false;
        }

        try
        {
            var ziel = GroupAddress.Parse3Level(adresse.Trim());
            await client.WriteAsync(ziel, nutzwert).ConfigureAwait(false);

            // Den eigenen Wert mitschreiben: ein Antrieb ohne Rueckmeldung
            // bleibt so wenigstens in der Anzeige nachvollziehbar.
            lock (_schloss)
            {
                _rohwerte[ziel] = nutzwert;
                _bus[ziel] = new Messwert(0, DateTime.Now);
            }
            return true;
        }
        catch (KnxException ex)
        {
            Melden("nicht gesendet", adresse + ": " + ex.Message, true);
            return false;
        }
    }

    // ---- Projekt -----------------------------------------------------------

    public bool ProjektLaden(string pfad, out string hinweis)
    {
        var eintraege = ProjectImport.Load(pfad, out hinweis);
        if (eintraege.Count == 0) return false;
        Adresspool = eintraege;
        Projektquelle = pfad;
        Einstellungen.Projektdatei = pfad;
        return true;
    }

    public void ProjektVergessen()
    {
        Adresspool = Array.Empty<GroupAddressEntry>();
        Projektquelle = "";
        Einstellungen.Projektdatei = "";
    }

    // ---- Protokoll ---------------------------------------------------------

    private const long ProtokollGrenze = 1024 * 1024;

    public void Melden(string was, string dazu, bool problem = false)
    {
        var zeile = new Protokollzeile(was, dazu, problem);
        Protokolliert?.Invoke(zeile);
        Schreiben(zeile);
    }

    /// <summary>
    /// Das Protokoll in eine Datei, damit man morgens nachsehen kann, warum
    /// die Markise nachts eingefahren ist. Bei einem Megabyte wird umgelegt -
    /// eine Datei, die still das Laufwerk fuellt, hilft niemandem.
    /// </summary>
    private void Schreiben(Protokollzeile zeile)
    {
        try
        {
            var pfad = Path.Combine(_ordner, "protokoll.log");
            var datei = new FileInfo(pfad);
            if (datei.Exists && datei.Length > ProtokollGrenze)
            {
                var alt = Path.Combine(_ordner, "protokoll.alt.log");
                File.Copy(pfad, alt, overwrite: true);
                File.Delete(pfad);
            }
            File.AppendAllText(pfad, zeile + Environment.NewLine);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Ein Protokoll, das sich nicht schreiben laesst, darf die
            // Steuerung nicht anhalten.
        }
    }

    public async ValueTask DisposeAsync()
    {
        Anhalten();
        await TrennenAsync().ConfigureAwait(false);
        _wetterabruf.Dispose();
    }
}
