using System;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using REGOwintergarden.Model;

namespace REGOwintergarden.App;

/// <summary>
/// Holt eine Vorhersage aus dem Netz - von Open-Meteo.
///
/// <b>Warum ueberhaupt,</b> wo doch eine Wetterstation auf dem Dach steht:
/// die Station misst, was <b>ist</b>. Die Markise, die um zehn Uhr ausfaehrt,
/// obwohl um elf Boeen mit 15 m/s angesagt sind, faehrt zweimal umsonst und
/// einmal zu spaet. Die Vorhersage ergaenzt also, sie ersetzt nichts - faellt
/// das Netz aus, zaehlt weiter allein die Station.
///
/// Open-Meteo, weil es ohne Anmeldung und ohne Schluessel geht: ein Programm,
/// das beim Kunden laeuft, soll keine Zugangsdaten brauchen, die in zwei
/// Jahren ablaufen.
/// </summary>
public sealed class Wetterabruf : IDisposable
{
    private readonly HttpClient _client;

    public Wetterabruf(HttpClient? client = null)
    {
        _client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    }

    /// <summary>Die Adresse, die gefragt wird - auch fuer die Pruefung sichtbar.</summary>
    public static string Adresse(double breite, double laenge) =>
        "https://api.open-meteo.com/v1/forecast"
        + "?latitude=" + breite.ToString("0.####", CultureInfo.InvariantCulture)
        + "&longitude=" + laenge.ToString("0.####", CultureInfo.InvariantCulture)
        + "&hourly=wind_gusts_10m,precipitation_probability,temperature_2m"
        + "&forecast_hours=12&wind_speed_unit=ms&timezone=auto";

    public async Task<Vorhersage?> HolenAsync(double breite, double laenge, DateTime jetzt,
        CancellationToken ct = default)
    {
        try
        {
            var text = await _client.GetStringAsync(Adresse(breite, laenge), ct).ConfigureAwait(false);
            return Lesen(text, jetzt);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Kein Netz ist kein Fehler. Die Anlage laeuft ohne Vorhersage
            // weiter, sie ist dann nur nicht vorgewarnt.
            return null;
        }
    }

    /// <summary>
    /// Liest die Antwort. Getrennt vom Abruf, damit sich das Lesen ohne Netz
    /// pruefen laesst - eine Vorhersage, die falsch gelesen wird, faehrt die
    /// Markise grundlos ein.
    /// </summary>
    public static Vorhersage? Lesen(string json, DateTime jetzt)
    {
        using var papier = JsonDocument.Parse(json);
        if (!papier.RootElement.TryGetProperty("hourly", out var stunden)) return null;

        var vorhersage = new Vorhersage { Stand = jetzt, Quelle = "Open-Meteo" };
        vorhersage.WindSpitze = Hoechster(stunden, "wind_gusts_10m");
        vorhersage.Regenwahrscheinlichkeit = Hoechster(stunden, "precipitation_probability");
        vorhersage.Hoechsttemperatur = Hoechster(stunden, "temperature_2m");

        return vorhersage.WindSpitze is null && vorhersage.Regenwahrscheinlichkeit is null
                                             && vorhersage.Hoechsttemperatur is null
            ? null
            : vorhersage;
    }

    /// <summary>
    /// Der hoechste Wert einer Reihe.
    ///
    /// Bewusst das Maximum und nicht der Mittelwert: fuer die Frage, ob die
    /// Markise draussen bleiben darf, zaehlt die staerkste Boe der naechsten
    /// Stunden, nicht der Durchschnitt eines ruhigen Nachmittags.
    /// </summary>
    private static double? Hoechster(JsonElement stunden, string name)
    {
        if (!stunden.TryGetProperty(name, out var reihe)) return null;
        if (reihe.ValueKind != JsonValueKind.Array) return null;

        double? hoechster = null;
        foreach (var eintrag in reihe.EnumerateArray())
        {
            if (eintrag.ValueKind != JsonValueKind.Number) continue;
            var wert = eintrag.GetDouble();
            if (hoechster is null || wert > hoechster) hoechster = wert;
        }
        return hoechster;
    }

    public void Dispose() => _client.Dispose();
}
