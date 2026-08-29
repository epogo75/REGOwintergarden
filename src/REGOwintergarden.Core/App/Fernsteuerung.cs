using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using REGOwintergarden.Knx;

namespace REGOwintergarden.App;

/// <summary>Ein Wert vom führenden Dienst: Adresse, Rohwert, Zeitpunkt.</summary>
public readonly record struct Fernwert(string Adresse, Payload Wert, DateTime Zeit);

/// <summary>
/// Was der führende Dienst über seinen Bus weiß.
///
/// Übertragen werden die <b>Rohwerte</b> und nicht die fertig ausgerechnete
/// Anzeige. Das ist der Kern des Ganzen: mit denselben Bytes und denselben
/// Einstellungen rechnet das zweite Gesicht dasselbe aus wie das erste, und
/// zwar mit demselben Quelltext. Eine zweite Darstellung, die eigene Zahlen
/// bekommt, geht früher oder später auseinander - und dann glaubt niemand
/// mehr einer von beiden.
///
/// Die Handsperren kommen mit, weil sie das Einzige sind, was sich nicht
/// nachrechnen lässt: dass jemand am Pi einen Antrieb von Hand gefahren hat,
/// steht in keinem Telegramm.
/// </summary>
public sealed record Fernzustand(
    IReadOnlyList<Fernwert> Werte,
    IReadOnlyDictionary<string, DateTime> Handsperren,
    string Version,
    string Anlage);

/// <summary>
/// Der Draht zum führenden Dienst - dem auf dem Raspberry Pi.
///
/// <b>Wer schreibt auf den Bus:</b> nur der führende Dienst. Diese Klasse
/// schickt ihm Befehle und holt sich seinen Stand ab; ein eigener KNX-Tunnel
/// wird in der Fernbedienung nie geöffnet. Sonst hätte die Anlage zwei
/// Automatiken, zwei zyklische Windtelegramme und keinen Chef.
/// </summary>
public sealed class Fernsteuerung : IDisposable
{
    private readonly HttpClient _http;

    public Fernsteuerung(string adresse)
    {
        Adresse = Aufraeumen(adresse);
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
    }

    /// <summary>Die Adresse des Dienstes, ohne Schrägstrich am Ende.</summary>
    public string Adresse { get; }

    /// <summary>
    /// Macht aus dem, was jemand eintippt, eine brauchbare Adresse.
    /// „192.168.1.229:5200" ist das, was man von Hand schreibt - ohne
    /// http:// davor kann <see cref="Uri"/> damit nichts anfangen.
    /// </summary>
    public static string Aufraeumen(string adresse)
    {
        var s = (adresse ?? "").Trim().TrimEnd('/');
        if (s.Length == 0) return "";
        if (!s.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            s = "http://" + s;
        }
        return s;
    }

    /// <summary>Lebt der Dienst? Die Frage, die auch Docker stellt.</summary>
    public async Task<bool> LebtAsync(CancellationToken ct = default)
    {
        try
        {
            var antwort = await _http.GetAsync(Adresse + "/gesundheit", ct).ConfigureAwait(false);
            return antwort.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UriFormatException
                                       or InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>Holt den Busstand. <c>null</c> heißt: der Dienst antwortet nicht.</summary>
    public async Task<Fernzustand?> HolenAsync(CancellationToken ct = default)
    {
        var text = await TextAsync(Adresse + "/bus.json", ct).ConfigureAwait(false);
        if (text is null) return null;

        try { return Lesen(text); }
        catch (JsonException) { return null; }
    }

    /// <summary>Die Einstellungen des führenden Dienstes, als Text wie in der Datei.</summary>
    public Task<string?> EinstellungenAsync(CancellationToken ct = default) =>
        TextAsync(Adresse + "/einstellungen.json", ct);

    /// <summary>
    /// Lässt den führenden Dienst einen Wert auf den Bus legen. Er entscheidet,
    /// ob er es tut - hier wird nur gebeten.
    /// </summary>
    public async Task<bool> SendenAsync(string adresse, string dpt, string wert,
        CancellationToken ct = default)
    {
        var koerper = "adresse=" + Uri.EscapeDataString(adresse)
                      + "&dpt=" + Uri.EscapeDataString(dpt)
                      + "&wert=" + Uri.EscapeDataString(wert);
        try
        {
            using var inhalt = new StringContent(koerper, Encoding.UTF8,
                "application/x-www-form-urlencoded");
            var antwort = await _http.PostAsync(Adresse + "/senden", inhalt, ct).ConfigureAwait(false);
            return antwort.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UriFormatException
                                       or InvalidOperationException)
        {
            return false;
        }
    }

    private async Task<string?> TextAsync(string adresse, CancellationToken ct)
    {
        try
        {
            var antwort = await _http.GetAsync(adresse, ct).ConfigureAwait(false);
            if (!antwort.IsSuccessStatusCode) return null;
            return await antwort.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UriFormatException
                                       or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Aus dem JSON des Dienstes wieder Rohwerte machen. Öffentlich, damit die
    /// Prüfung den Weg hin und zurück gehen kann, ohne ein Netz zu brauchen.
    /// </summary>
    public static Fernzustand Lesen(string json)
    {
        using var papier = JsonDocument.Parse(json);
        var wurzel = papier.RootElement;

        var werte = new List<Fernwert>();
        if (wurzel.TryGetProperty("werte", out var liste) && liste.ValueKind == JsonValueKind.Array)
        {
            foreach (var eintrag in liste.EnumerateArray())
            {
                var adresse = Text(eintrag, "adresse");
                if (adresse.Length == 0) continue;

                var roh = Text(eintrag, "roh");
                var klein = eintrag.TryGetProperty("klein", out var k)
                            && k.ValueKind == JsonValueKind.True;
                var wert = klein
                    ? Payload.FromSmall(ErstesByte(roh))
                    : Payload.FromBytes(Bytes(roh));

                var zeit = DateTime.TryParse(Text(eintrag, "zeit"), CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var gelesen)
                    ? gelesen
                    : DateTime.Now;

                werte.Add(new Fernwert(adresse, wert, zeit));
            }
        }

        var sperren = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        if (wurzel.TryGetProperty("handsperren", out var hand)
            && hand.ValueKind == JsonValueKind.Object)
        {
            foreach (var eigenschaft in hand.EnumerateObject())
            {
                if (DateTime.TryParse(eigenschaft.Value.GetString(), CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var bis))
                {
                    sperren[eigenschaft.Name] = bis;
                }
            }
        }

        return new Fernzustand(werte, sperren, Text(wurzel, "version"), Text(wurzel, "anlage"));
    }

    private static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var wert) && wert.ValueKind == JsonValueKind.String
            ? wert.GetString() ?? ""
            : "";

    /// <summary>Aus „0a ff" die Bytes. Unlesbares wird übersprungen, nicht geworfen.</summary>
    public static byte[] Bytes(string hex)
    {
        var sauber = (hex ?? "").Replace(" ", "", StringComparison.Ordinal);
        if (sauber.Length < 2) return Array.Empty<byte>();

        var bytes = new List<byte>(sauber.Length / 2);
        for (var i = 0; i + 1 < sauber.Length; i += 2)
        {
            if (byte.TryParse(sauber.AsSpan(i, 2), NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture, out var b))
            {
                bytes.Add(b);
            }
        }
        return bytes.ToArray();
    }

    private static byte ErstesByte(string hex)
    {
        var bytes = Bytes(hex);
        return bytes.Length > 0 ? bytes[0] : (byte)0;
    }

    /// <summary>Bytes als Hexadezimaltext - so gehen sie über die Leitung.</summary>
    public static string Hex(Payload wert) =>
        wert.IsSmall
            ? wert.Small.ToString("x2", CultureInfo.InvariantCulture)
            : string.Concat(Array.ConvertAll(wert.Bytes,
                b => b.ToString("x2", CultureInfo.InvariantCulture)));

    public void Dispose() => _http.Dispose();
}
