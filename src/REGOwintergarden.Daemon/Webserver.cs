using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using REGOwintergarden.App;
using REGOwintergarden.Model;
using REGOwintergarden.Web;

namespace REGOwintergarden.Daemon;

/// <summary>
/// Ein kleiner Webserver für die Bedienung auf dem Raspberry Pi.
///
/// <b>Warum selbst gebaut:</b> ein Rahmenwerk wäre hier die dritte
/// Abhängigkeit und die erste, die regelmäßig Sicherheitsaktualisierungen
/// braucht. <see cref="HttpListener"/> steckt im Rahmenwerk, kann alles, was
/// eine Seite mit sechs Knöpfen braucht, und kommt ohne NuGet aus - wie der
/// Rest dieses Programms.
///
/// <b>Wer darf zugreifen:</b> jeder im Netz. Das ist eine bewusste
/// Entscheidung und keine Nachlässigkeit - eine Wintergartensteuerung im
/// Heimnetz hinter einer Anmeldung zu verstecken, führt dazu, dass das
/// Kennwort auf einem Zettel am Tablet klebt. Wer sie von außen erreichbar
/// macht, gehört hinter einen Reverse Proxy mit Anmeldung; das steht auch so
/// in der Anleitung.
/// </summary>
public sealed class Webserver : IDisposable
{
    private HttpListener? _listener;
    private readonly Wintergartendienst _dienst;
    private readonly Action<string, string, bool> _melden;
    private readonly string _ordner;

    public Webserver(Wintergartendienst dienst, string ordner, int port,
        Action<string, string, bool> melden)
    {
        _dienst = dienst;
        _ordner = ordner;
        _melden = melden;
        Port = port;
    }

    public int Port { get; }

    /// <summary>Woran der Server hängt - für die Meldung beim Start.</summary>
    public string Adresse { get; private set; } = "";

    /// <summary>
    /// Startet den Server. Schlägt das Binden fehl, wird es gemeldet und der
    /// Rest läuft weiter: eine belegte Portnummer ist kein Grund, die
    /// Beschattung einzustellen.
    ///
    /// Erst über alle Netzwerkkarten, dann nur örtlich. Der zweite Versuch ist
    /// für Windows: dort braucht <c>http://+:</c> eine eingetragene
    /// Reservierung oder erhöhte Rechte, und daran soll ein Probelauf auf dem
    /// Entwicklungsrechner nicht scheitern. Auf dem Raspberry Pi greift immer
    /// der erste.
    /// </summary>
    public bool Starten()
    {
        var port = Port.ToString(CultureInfo.InvariantCulture);
        foreach (var praefix in new[] { "http://+:" + port + "/", "http://localhost:" + port + "/" })
        {
            // Je Versuch ein frischer Listener: ein misslungenes Start()
            // entsorgt ihn, und der zweite Versuch liefe sonst auf ein totes
            // Objekt - mit einer Ausnahme, die das ganze Programm mitnimmt.
            var versuch = new HttpListener();
            versuch.Prefixes.Add(praefix);
            try
            {
                versuch.Start();
                _listener = versuch;
                Adresse = praefix;
                _melden("Weboberflaeche", praefix == "http://+:" + port + "/"
                    ? "erreichbar im Netz auf Port " + port
                    : "nur oertlich erreichbar auf Port " + port
                      + " - fuer das ganze Netz braucht Windows eine Reservierung", false);
                _ = Bedienen();
                return true;
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException)
            {
                _melden("Weboberflaeche", praefix + " geht nicht: " + ex.Message, true);
                versuch.Close();
            }
        }

        _melden("Weboberflaeche nicht gestartet",
            "Port " + port + " liess sich nicht binden - laeuft dort schon etwas?", true);
        return false;
    }

    private async Task Bedienen()
    {
        var listener = _listener;
        if (listener is null) return;

        while (listener.IsListening)
        {
            HttpListenerContext kontext;
            try { kontext = await listener.GetContextAsync().ConfigureAwait(false); }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException) { return; }

            // Jede Anfrage für sich: eine kaputte darf die nächste nicht
            // verhindern.
            _ = Task.Run(() => Beantworten(kontext));
        }
    }

    private async Task Beantworten(HttpListenerContext kontext)
    {
        try
        {
            var pfad = kontext.Request.Url?.AbsolutePath ?? "/";
            switch (pfad)
            {
                case "/":
                case "/index.html":
                    Senden(kontext, 200, "text/html; charset=utf-8",
                        Webseite.Bauen(_dienst, DateTime.Now));
                    return;

                case "/automatik":
                    Senden(kontext, 200, "text/html; charset=utf-8",
                        Webseite.Automatikseite(_dienst, DateTime.Now));
                    return;

                case "/verlauf":
                    Senden(kontext, 200, "text/html; charset=utf-8",
                        Webseite.Verlaufsseite(_dienst, DateTime.Now,
                            Zahl(kontext.Request.QueryString["stunden"], 24)));
                    return;

                case "/konfig":
                    Senden(kontext, 200, "text/html; charset=utf-8",
                        Webseite.Konfigseite(_dienst, DateTime.Now, Gut(kontext)));
                    return;

                case "/konfig/antriebe":
                    Senden(kontext, 200, "text/html; charset=utf-8",
                        Webseite.Antriebsliste(_dienst, DateTime.Now, Gut(kontext)));
                    return;

                case "/konfig/antrieb":
                    Senden(kontext, 200, "text/html; charset=utf-8",
                        Webseite.Antriebsformular(_dienst, DateTime.Now,
                            kontext.Request.QueryString["id"] ?? "", Gut(kontext)));
                    return;

                case "/konfig/zeiten":
                    Senden(kontext, 200, "text/html; charset=utf-8",
                        Webseite.Zeitenseite(_dienst, DateTime.Now, Gut(kontext)));
                    return;

                case "/konfig/anschluss":
                    Senden(kontext, 200, "text/html; charset=utf-8",
                        Webseite.Anschlussseite(_dienst, DateTime.Now, Gut(kontext)));
                    return;

                case "/konfig/protokoll":
                    Senden(kontext, 200, "text/html; charset=utf-8",
                        Webseite.Protokollseite(_dienst, DateTime.Now, Gut(kontext)));
                    return;

                case "/schalten":
                    await SchaltenAsync(kontext).ConfigureAwait(false);
                    return;

                case "/einstellen":
                    await EinstellenAsync(kontext).ConfigureAwait(false);
                    return;

                case "/antrieb":
                    await AntriebAsync(kontext).ConfigureAwait(false);
                    return;

                case "/zeit":
                    await ZeitAsync(kontext).ConfigureAwait(false);
                    return;

                case "/anschluss":
                    await AnschlussAsync(kontext).ConfigureAwait(false);
                    return;

                case "/abfragen":
                    await _dienst.AbfragenAsync().ConfigureAwait(false);
                    Zurueck(kontext, "/konfig/anschluss?gut=1");
                    return;

                case "/lage.json":
                    Senden(kontext, 200, "application/json; charset=utf-8", Lage());
                    return;

                case "/bus.json":
                    // Fuer das zweite Gesicht: die Rohwerte, nicht die fertige
                    // Anzeige. Damit rechnet drueben derselbe Quelltext
                    // dasselbe aus, statt eine zweite Wahrheit zu pflegen.
                    Senden(kontext, 200, "application/json; charset=utf-8", Buswerte());
                    return;

                case "/einstellungen.json":
                    // Damit die Fernbedienung dieselbe Anlage vor sich hat -
                    // dieselben Antriebe, dieselben Grenzen, dieselben
                    // Himmelsrichtungen. Ohne das rechnete sie richtig, aber
                    // ueber eine andere Anlage.
                    Senden(kontext, 200, "application/json; charset=utf-8", Einstellungstext());
                    return;

                case "/fahren":
                    await FahrenAsync(kontext).ConfigureAwait(false);
                    return;

                case "/senden":
                    await SendenAsync(kontext).ConfigureAwait(false);
                    return;

                case "/gesundheit":
                    // Für Docker und für jede Überwachung: eine Zeile, die
                    // sagt, ob die Automatik noch rechnet.
                    Senden(kontext, _dienst.Laeuft ? 200 : 503, "text/plain; charset=utf-8",
                        _dienst.Laeuft ? "laeuft" : "steht");
                    return;

                default:
                    Senden(kontext, 404, "text/plain; charset=utf-8", "Nicht gefunden");
                    return;
            }
        }
        catch (Exception ex) when (ex is IOException or HttpListenerException)
        {
            // Der Browser ist weg. Das ist kein Fehler dieser Steuerung.
        }
        catch (Exception ex)
        {
            _melden("Weboberflaeche", ex.Message, true);
            try { Senden(kontext, 500, "text/plain; charset=utf-8", "Fehler: " + ex.Message); }
            catch (Exception weiterer) when (weiterer is IOException or HttpListenerException) { }
        }
    }

    /// <summary>Ein Handgriff von der Seite: Auf, Stopp oder Ab.</summary>
    private async Task FahrenAsync(HttpListenerContext kontext)
    {
        var felder = await FelderAsync(kontext).ConfigureAwait(false);
        felder.TryGetValue("motor", out var id);
        felder.TryGetValue("was", out var was);

        var motor = _dienst.Anlage.Finde(id ?? "");
        if (motor is not null && was is not null) await _dienst.BefehlAsync(motor, was).ConfigureAwait(false);

        // Nach dem Absenden zurück auf die Seite - sonst zeigt der Browser
        // eine leere Antwort und ein erneutes Laden fährt noch einmal.
        kontext.Response.StatusCode = 303;
        kontext.Response.RedirectLocation = "/";
        kontext.Response.Close();
    }

    /// <summary>
    /// Ein Wert auf den Bus, gebeten von der Fernbedienung.
    ///
    /// <b>Warum das hier stehen darf:</b> geschrieben wird weiterhin nur an
    /// einer Stelle - hier. Die Fernbedienung hat keinen eigenen Tunnel, sie
    /// bittet. Damit bleibt es bei einer Automatik und einem zyklischen
    /// Windtelegramm, egal wie viele Fenster offen sind.
    /// </summary>
    private async Task SendenAsync(HttpListenerContext kontext)
    {
        var felder = await FelderAsync(kontext).ConfigureAwait(false);
        felder.TryGetValue("adresse", out var adresse);
        felder.TryGetValue("dpt", out var dpt);
        felder.TryGetValue("wert", out var wert);

        if (string.IsNullOrWhiteSpace(adresse) || string.IsNullOrWhiteSpace(dpt) || wert is null)
        {
            Senden(kontext, 400, "text/plain; charset=utf-8", "adresse, dpt und wert werden gebraucht");
            return;
        }

        var gut = await _dienst.SendenFuerFernAsync(adresse, dpt, wert).ConfigureAwait(false);
        Senden(kontext, gut ? 200 : 502, "text/plain; charset=utf-8",
            gut ? "gesendet" : "nicht gesendet");
    }

    /// <summary>Eine Regel ein- oder ausschalten.</summary>
    private async Task SchaltenAsync(HttpListenerContext kontext)
    {
        var felder = await FelderAsync(kontext).ConfigureAwait(false);
        felder.TryGetValue("regel", out var regel);
        var an = felder.TryGetValue("an", out var wert) && wert == "1";

        var anlage = _dienst.Anlage;
        switch (regel)
        {
            case "hauptschalter": anlage.AutomatikAktiv = an; break;
            case "wind": anlage.WindschutzAktiv = an; break;
            case "regen": anlage.RegenschutzAktiv = an; break;
            case "frost": anlage.FrostschutzAktiv = an; break;
            case "beschattung": anlage.BeschattungAktiv = an; break;
            case "lueftung": anlage.LueftungAktiv = an; break;
            case "uhr": anlage.ZeitschaltuhrAktiv = an; break;
            case "waermegewinn": anlage.WaermegewinnAktiv = an; break;
            case "hitzevorsorge": anlage.HitzevorsorgeAktiv = an; break;
            case "nachtauskuehlung": anlage.NachtauskuehlungAktiv = an; break;
            case "vorhersage": anlage.VorhersageAktiv = an; break;
            default:
                Senden(kontext, 400, "text/plain; charset=utf-8", "unbekannte Regel");
                return;
        }

        _melden("Automatik", regel + (an ? " eingeschaltet" : " ausgeschaltet"), false);
        Sichern();
        Zurueck(kontext, "/automatik");
    }

    /// <summary>Die Anlageseite - Standort, Adressen, Grenzen.</summary>
    private async Task EinstellenAsync(HttpListenerContext kontext)
    {
        var felder = await FelderAsync(kontext).ConfigureAwait(false);
        var anlage = _dienst.Anlage;

        anlage.Name = Wort(felder, "name", anlage.Name);
        anlage.Ort = Wort(felder, "ort", anlage.Ort);
        anlage.Breite = Zahl(felder, "breite", anlage.Breite);
        anlage.Laenge = Zahl(felder, "laenge", anlage.Laenge);

        // Adressen duerfen geleert werden - eine Wetterstation ohne
        // Helligkeitsfuehler ist ein gewoehnlicher Fall.
        anlage.AdresseWindalarm = Adressfeld(felder, "adr_windalarm", anlage.AdresseWindalarm);
        anlage.AdresseWind = Adressfeld(felder, "adr_wind", anlage.AdresseWind);
        anlage.AdresseRegen = Adressfeld(felder, "adr_regen", anlage.AdresseRegen);
        anlage.AdresseAussen = Adressfeld(felder, "adr_aussen", anlage.AdresseAussen);
        anlage.AdresseInnen = Adressfeld(felder, "adr_innen", anlage.AdresseInnen);
        anlage.AdresseHellOst = Adressfeld(felder, "adr_ost", anlage.AdresseHellOst);
        anlage.AdresseHellSued = Adressfeld(felder, "adr_sued", anlage.AdresseHellSued);
        anlage.AdresseHellWest = Adressfeld(felder, "adr_west", anlage.AdresseHellWest);
        anlage.AdresseAzimut = Adressfeld(felder, "adr_azimut", anlage.AdresseAzimut);
        anlage.AdresseElevation = Adressfeld(felder, "adr_elevation", anlage.AdresseElevation);
        anlage.AdresseWindausgabe = Adressfeld(felder, "adr_windaus", anlage.AdresseWindausgabe);
        anlage.AdresseRegenausgabe = Adressfeld(felder, "adr_regenaus", anlage.AdresseRegenausgabe);

        anlage.AusgabetaktSekunden = Zahl(felder, "ausgabetakt", anlage.AusgabetaktSekunden);
        anlage.WindgrenzeAusgabe = Zahl(felder, "windausgabe", anlage.WindgrenzeAusgabe);
        anlage.AusgabeInvertiert = Haken(felder, "invertiert", anlage.AusgabeInvertiert);

        // Was nicht als Zahl lesbar ist, bleibt stehen. Ein leeres Feld soll
        // keine Grenze auf null setzen - das waere die Beschattung, die nie
        // mehr abschaltet.
        anlage.Helligkeitsschwelle = Zahl(felder, "helligkeit", anlage.Helligkeitsschwelle);
        anlage.EinschaltverzoegerungMinuten = Zahl(felder, "ein", anlage.EinschaltverzoegerungMinuten);
        anlage.AusschaltverzoegerungMinuten = Zahl(felder, "aus", anlage.AusschaltverzoegerungMinuten);
        anlage.InnenWarm = Zahl(felder, "innenwarm", anlage.InnenWarm);
        anlage.WarmFaktor = Zahl(felder, "warmfaktor", anlage.WarmFaktor);

        anlage.LueftungAb = Zahl(felder, "lueftungab", anlage.LueftungAb);
        anlage.LueftungHysterese = Zahl(felder, "lueftunghyst", anlage.LueftungHysterese);
        anlage.LueftungUnterschied = Zahl(felder, "lueftungdelta", anlage.LueftungUnterschied);
        anlage.Lueftungsposition = Zahl(felder, "lueftungspos", anlage.Lueftungsposition);

        anlage.WaermegewinnAussen = Zahl(felder, "waermeaussen", anlage.WaermegewinnAussen);
        anlage.WaermegewinnInnen = Zahl(felder, "waermeinnen", anlage.WaermegewinnInnen);
        anlage.HitzevorsorgeAb = Zahl(felder, "hitzeab", anlage.HitzevorsorgeAb);
        anlage.NachtauskuehlungAb = Zahl(felder, "nachtab", anlage.NachtauskuehlungAb);
        anlage.NachtauskuehlungZiel = Zahl(felder, "nachtziel", anlage.NachtauskuehlungZiel);

        anlage.WindNachlaufMinuten = Zahl(felder, "windnachlauf", anlage.WindNachlaufMinuten);
        anlage.RegenNachlaufMinuten = Zahl(felder, "regennachlauf", anlage.RegenNachlaufMinuten);
        anlage.HandsperreMinuten = Zahl(felder, "handsperre", anlage.HandsperreMinuten);
        anlage.HoechstalterWindMinuten = Zahl(felder, "alterwind", anlage.HoechstalterWindMinuten);
        anlage.HoechstalterRegenMinuten = Zahl(felder, "alterregen", anlage.HoechstalterRegenMinuten);
        anlage.HoechstalterTemperaturMinuten = Zahl(felder, "altertemp", anlage.HoechstalterTemperaturMinuten);
        anlage.HoechstalterHelligkeitMinuten = Zahl(felder, "alterhell", anlage.HoechstalterHelligkeitMinuten);
        anlage.TaktSekunden = Zahl(felder, "takt", anlage.TaktSekunden);
        anlage.MindestpauseSekunden = Zahl(felder, "pause", anlage.MindestpauseSekunden);

        _melden("Konfiguration", "Anlage ueber die Weboberflaeche geaendert", false);
        Sichern();
        Zurueck(kontext, "/konfig?gut=1");
    }

    /// <summary>Ein Antrieb mit allen Feldern.</summary>
    private async Task AntriebAsync(HttpListenerContext kontext)
    {
        var felder = await FelderAsync(kontext).ConfigureAwait(false);
        felder.TryGetValue("id", out var id);

        var motor = _dienst.Anlage.Finde(id ?? "");
        if (motor is null)
        {
            Senden(kontext, 404, "text/plain; charset=utf-8", "Antrieb nicht gefunden");
            return;
        }

        motor.Name = Wort(felder, "name", motor.Name);
        if (felder.TryGetValue("art", out var art)
            && Enum.TryParse<Antriebsart>(art, ignoreCase: true, out var gelesen))
        {
            motor.Art = gelesen;
        }
        motor.Ausrichtung = Motor.Normiert(Zahl(felder, "ausrichtung", motor.Ausrichtung));

        motor.Oeffnungswinkel = Zahl(felder, "oeffnung", motor.Oeffnungswinkel);
        motor.ElevationMin = Zahl(felder, "elevmin", motor.ElevationMin);
        motor.ElevationMax = Zahl(felder, "elevmax", motor.ElevationMax);
        motor.Beschattungsposition = Zahl(felder, "beschattung", motor.Beschattungsposition);
        motor.Lamellenposition = Zahl(felder, "lamelle", motor.Lamellenposition);
        motor.Freiposition = Zahl(felder, "frei", motor.Freiposition);

        motor.Windgrenze = Zahl(felder, "wind", motor.Windgrenze);
        motor.Frostgrenze = Zahl(felder, "frost", motor.Frostgrenze);
        motor.Regenschutz = Haken(felder, "regenschutz", motor.Regenschutz);

        motor.BeschattungAktiv = Haken(felder, "beschattungaktiv", motor.BeschattungAktiv);
        motor.LueftungAktiv = Haken(felder, "lueftungaktiv", motor.LueftungAktiv);
        motor.ZeitAktiv = Haken(felder, "zeitaktiv", motor.ZeitAktiv);

        motor.AdresseFahren = Adressfeld(felder, "adr_fahren", motor.AdresseFahren);
        motor.AdresseStopp = Adressfeld(felder, "adr_stopp", motor.AdresseStopp);
        motor.AdressePosition = Adressfeld(felder, "adr_position", motor.AdressePosition);
        motor.AdressePositionStatus = Adressfeld(felder, "adr_positionstatus", motor.AdressePositionStatus);
        motor.AdresseLamelle = Adressfeld(felder, "adr_lamelle", motor.AdresseLamelle);
        motor.AdresseLamelleStatus = Adressfeld(felder, "adr_lamellestatus", motor.AdresseLamelleStatus);

        _melden("Konfiguration", motor.Name + " ueber die Weboberflaeche geaendert", false);
        Sichern();
        Zurueck(kontext, "/konfig/antrieb?gut=1&id=" + Uri.EscapeDataString(motor.Id));
    }

    /// <summary>Eine Schaltzeit anlegen, schalten oder loeschen.</summary>
    private async Task ZeitAsync(HttpListenerContext kontext)
    {
        var felder = await FelderAsync(kontext).ConfigureAwait(false);
        felder.TryGetValue("was", out var was);
        felder.TryGetValue("id", out var id);
        var anlage = _dienst.Anlage;

        switch (was)
        {
            case "neu":
                var zeit = new Schaltzeit
                {
                    Zeit = Wort(felder, "zeit", "07:00"),
                    Versatz = (int)Zahl(felder, "versatz", 0),
                    Tage = Wort(felder, "tage", "1234567"),
                    MotorId = Wort(felder, "motor", ""),
                    Position = Zahl(felder, "position", 100),
                    Bemerkung = Wort(felder, "bemerkung", ""),
                };
                anlage.Schaltzeiten.Add(zeit);
                _melden("Zeitschaltuhr", "angelegt: " + zeit.Beschreibung(), false);
                break;

            case "schalten":
                var treffer = anlage.Schaltzeiten.Find(z => z.Id == id);
                if (treffer is null) break;
                treffer.Aktiv = !treffer.Aktiv;
                _melden("Zeitschaltuhr", (treffer.Aktiv ? "eingeschaltet: " : "ausgeschaltet: ")
                                         + treffer.Beschreibung(), false);
                break;

            case "loeschen":
                var weg = anlage.Schaltzeiten.Find(z => z.Id == id);
                if (weg is null) break;
                anlage.Schaltzeiten.Remove(weg);
                _melden("Zeitschaltuhr", "geloescht: " + weg.Beschreibung(), false);
                break;

            default:
                Senden(kontext, 400, "text/plain; charset=utf-8", "unbekannter Auftrag");
                return;
        }

        Sichern();
        Zurueck(kontext, "/konfig/zeiten?gut=1");
    }

    /// <summary>Gateway eintragen und verbinden.</summary>
    private async Task AnschlussAsync(HttpListenerContext kontext)
    {
        var felder = await FelderAsync(kontext).ConfigureAwait(false);
        var gateway = Wort(felder, "gateway", _dienst.Einstellungen.Gateway).Trim();

        _dienst.Einstellungen.Gateway = gateway;
        Sichern();

        if (gateway.Length > 0) await _dienst.VerbindenAsync(gateway).ConfigureAwait(false);
        else await _dienst.TrennenAsync().ConfigureAwait(false);

        Zurueck(kontext, "/konfig/anschluss?gut=1");
    }

    private static string Gut(HttpListenerContext kontext) =>
        kontext.Request.QueryString["gut"] is null ? "" : "Uebernommen.";

    private static string Wort(Dictionary<string, string> felder, string name, string sonst) =>
        felder.TryGetValue(name, out var text) && text.Trim().Length > 0 ? text.Trim() : sonst;

    /// <summary>
    /// Eine Adresse darf geleert werden - anders als ein Name. Ohne diesen
    /// Unterschied liesse sich ein einmal eingetragener Helligkeitsfuehler nie
    /// wieder loswerden.
    /// </summary>
    private static string Adressfeld(Dictionary<string, string> felder, string name, string sonst) =>
        felder.TryGetValue(name, out var text) ? text.Trim() : sonst;

    private static bool Haken(Dictionary<string, string> felder, string name, bool sonst) =>
        felder.TryGetValue(name, out var text) ? text == "1" : sonst;

    /// <summary>
    /// Sofort in die Datei. Wer am Tablet eine Grenze aendert, erwartet nicht,
    /// dass sie beim naechsten Neustart des Dienstes wieder dasteht wie zuvor.
    /// </summary>
    private void Sichern()
    {
        try { _dienst.Einstellungen.Speichern(_ordner); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _melden("nicht gespeichert", ex.Message, true);
        }
    }

    /// <summary>
    /// Nach dem Absenden zurueck auf die Seite - sonst zeigt der Browser eine
    /// leere Antwort, und ein erneutes Laden schaltet noch einmal.
    /// </summary>
    private static void Zurueck(HttpListenerContext kontext, string wohin)
    {
        kontext.Response.StatusCode = 303;
        kontext.Response.RedirectLocation = wohin;
        kontext.Response.Close();
    }

    private static double Zahl(Dictionary<string, string> felder, string name, double sonst) =>
        felder.TryGetValue(name, out var text)
        && double.TryParse(text.Replace(',', '.'), NumberStyles.Float,
            CultureInfo.InvariantCulture, out var wert)
            ? wert
            : sonst;

    private static int Zahl(string? text, int sonst) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var wert) ? wert : sonst;

    /// <summary>Die Rohwerte des Busses - die Grundlage fuer das zweite Gesicht.</summary>
    private string Buswerte()
    {
        var speicher = new MemoryStream();
        using (var schreiber = new Utf8JsonWriter(speicher, new JsonWriterOptions { Indented = false }))
        {
            schreiber.WriteStartObject();
            schreiber.WriteString("version", Programmstand.Version);
            schreiber.WriteString("anlage", _dienst.Anlage.Name);
            schreiber.WriteString("zeit", DateTime.Now.ToString("O", CultureInfo.InvariantCulture));

            schreiber.WriteStartArray("werte");
            foreach (var wert in _dienst.Buswerte())
            {
                schreiber.WriteStartObject();
                schreiber.WriteString("adresse", wert.Adresse);
                schreiber.WriteString("roh", Fernsteuerung.Hex(wert.Wert));
                schreiber.WriteBoolean("klein", wert.Wert.IsSmall);
                schreiber.WriteString("zeit", wert.Zeit.ToString("O", CultureInfo.InvariantCulture));
                schreiber.WriteEndObject();
            }
            schreiber.WriteEndArray();

            schreiber.WriteStartObject("handsperren");
            foreach (var sperre in _dienst.Handsperren())
            {
                schreiber.WriteString(sperre.Key, sperre.Value.ToString("O", CultureInfo.InvariantCulture));
            }
            schreiber.WriteEndObject();

            schreiber.WriteEndObject();
        }
        return Encoding.UTF8.GetString(speicher.ToArray());
    }

    /// <summary>Die Einstellungen, so wie sie in der Datei stehen.</summary>
    private string Einstellungstext()
    {
        var pfad = Path.Combine(_ordner, Einstellungen.Dateiname);
        try { if (File.Exists(pfad)) return File.ReadAllText(pfad); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }

        // Noch nie gespeichert - dann eben das, was gerade gilt.
        return JsonSerializer.Serialize(_dienst.Einstellungen,
            new JsonSerializerOptions { WriteIndented = true });
    }

    private static async Task<Dictionary<string, string>> FelderAsync(HttpListenerContext kontext)
    {
        var felder = new Dictionary<string, string>(StringComparer.Ordinal);
        using var leser = new StreamReader(kontext.Request.InputStream, Encoding.UTF8);
        var text = await leser.ReadToEndAsync().ConfigureAwait(false);

        foreach (var paar in text.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var teile = paar.Split('=', 2);
            if (teile.Length != 2) continue;
            felder[Uri.UnescapeDataString(teile[0])] = Uri.UnescapeDataString(teile[1].Replace('+', ' '));
        }
        return felder;
    }

    /// <summary>Derselbe Stand als JSON - für eine Visualisierung, die ihn abholen will.</summary>
    private string Lage()
    {
        var jetzt = DateTime.Now;
        var anlage = _dienst.Anlage;
        var wetter = _dienst.Wetter();
        var (ueberschrift, _) = Lagebericht.Ueberschrift(anlage, _dienst.Lagen);

        var speicher = new MemoryStream();
        using (var schreiber = new Utf8JsonWriter(speicher, new JsonWriterOptions { Indented = true }))
        {
            schreiber.WriteStartObject();
            schreiber.WriteString("zeit", jetzt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            schreiber.WriteString("anlage", anlage.Name);
            schreiber.WriteString("zustand", ueberschrift);
            schreiber.WriteBoolean("bus", _dienst.Stand == Busstand.Verbunden);

            schreiber.WriteStartObject("wetter");
            Zahl(schreiber, "wind", wetter.Wind);
            Zahl(schreiber, "windalarm", wetter.Windalarm);
            Zahl(schreiber, "regen", wetter.Regen);
            Zahl(schreiber, "aussen", wetter.Aussen);
            Zahl(schreiber, "innen", wetter.Innen);
            Zahl(schreiber, "helligkeit", wetter.HellsteRichtung());
            schreiber.WriteEndObject();

            schreiber.WriteStartObject("sonne");
            schreiber.WriteNumber("azimut", Math.Round(_dienst.Sonne.Azimut, 1));
            schreiber.WriteNumber("elevation", Math.Round(_dienst.Sonne.Elevation, 1));
            schreiber.WriteEndObject();

            schreiber.WriteStartObject("sicherheit");
            schreiber.WriteBoolean("wind", _dienst.Sicherheitslage.Wind);
            schreiber.WriteBoolean("regen", _dienst.Sicherheitslage.Regen);
            schreiber.WriteString("grund", _dienst.Sicherheitslage.Grund);
            schreiber.WriteEndObject();

            schreiber.WriteStartArray("antriebe");
            foreach (var lage in _dienst.Lagen)
            {
                schreiber.WriteStartObject();
                schreiber.WriteString("id", lage.Motor.Id);
                schreiber.WriteString("name", lage.Motor.Name);
                schreiber.WriteString("art", lage.Motor.Art.ToString());
                schreiber.WriteNumber("ausrichtung", Math.Round(lage.Motor.Ausrichtung));
                schreiber.WriteString("stufe", lage.Stufe.ToString());
                schreiber.WriteString("grund", lage.Grund);
                if (lage.Ziel is { } ziel) schreiber.WriteNumber("ziel", Math.Round(ziel));
                else schreiber.WriteNull("ziel");
                schreiber.WriteEndObject();
            }
            schreiber.WriteEndArray();
            schreiber.WriteEndObject();
        }
        return Encoding.UTF8.GetString(speicher.ToArray());
    }

    private static void Zahl(Utf8JsonWriter schreiber, string name, Messwert? wert)
    {
        if (wert is null) schreiber.WriteNull(name);
        else schreiber.WriteNumber(name, Math.Round(wert.Value.Wert, 2));
    }

    private static void Senden(HttpListenerContext kontext, int status, string art, string inhalt)
    {
        var bytes = Encoding.UTF8.GetBytes(inhalt);
        kontext.Response.StatusCode = status;
        kontext.Response.ContentType = art;
        kontext.Response.ContentLength64 = bytes.Length;
        kontext.Response.OutputStream.Write(bytes, 0, bytes.Length);
        kontext.Response.Close();
    }

    public void Dispose()
    {
        try { _listener?.Close(); }
        catch (ObjectDisposedException) { }
    }
}
