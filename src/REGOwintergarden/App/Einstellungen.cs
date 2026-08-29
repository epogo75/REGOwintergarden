using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using REGOwintergarden.Model;

namespace REGOwintergarden.App;

/// <summary>
/// Alles, was das Programm behaelt: die Anlage und der Weg zum Bus.
///
/// Eine Datei, lesbar, im Benutzerprofil. Wer sichern will, kopiert sie; wer
/// eine zweite Anlage einrichtet, legt sie daneben und setzt
/// <c>REGOWINTERGARDEN_HOME</c>.
/// </summary>
public sealed class Einstellungen
{
    [JsonPropertyName("gateway")]
    public string Gateway { get; set; } = "";

    [JsonPropertyName("anlage")]
    public Anlage Anlage { get; set; } = new();

    /// <summary>Pfad zu einer ETS-Projektdatei oder einem Gruppenadressexport.</summary>
    [JsonPropertyName("knx_projekt")]
    public string Projektdatei { get; set; } = "";

    /// <summary>Ob die Vorhersage aus dem Netz geholt wird.</summary>
    [JsonPropertyName("vorhersage_holen")]
    public bool VorhersageHolen { get; set; } = true;

    private static readonly JsonSerializerOptions Format = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public const string Dateiname = "einstellungen.json";

    /// <summary>
    /// Der Ordner, in dem alles liegt. Ueber <c>REGOWINTERGARDEN_HOME</c>
    /// umlenkbar - das braucht der Dienst, der unter einem anderen Konto
    /// laeuft und sonst in dessen Profil schriebe.
    /// </summary>
    public static string StandardOrdner
    {
        get
        {
            var eigen = Environment.GetEnvironmentVariable("REGOWINTERGARDEN_HOME");
            if (!string.IsNullOrWhiteSpace(eigen)) return eigen!;
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "REGOwintergarden");
        }
    }

    public static Einstellungen Laden(string ordner)
    {
        var pfad = Path.Combine(ordner, Dateiname);
        try
        {
            if (!File.Exists(pfad)) return Erststart();
            var text = File.ReadAllText(pfad);
            var gelesen = JsonSerializer.Deserialize<Einstellungen>(text, Format);
            return gelesen ?? Erststart();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Lieber mit Vorgaben starten als gar nicht. Die kaputte Datei
            // bleibt liegen - ueberschrieben wird sie erst beim naechsten
            // Speichern, und bis dahin kann man hineinsehen.
            return Erststart();
        }
    }

    /// <summary>Beim ersten Start steht schon ein Wintergarten da - nur ohne Adressen.</summary>
    private static Einstellungen Erststart() => new() { Anlage = Anlage.Beispiel() };

    public void Speichern(string ordner)
    {
        Directory.CreateDirectory(ordner);
        var pfad = Path.Combine(ordner, Dateiname);

        // Erst daneben schreiben, dann tauschen: ein Stromausfall mitten im
        // Speichern soll nicht die Anlage kosten.
        var vorlaeufig = pfad + ".neu";
        File.WriteAllText(vorlaeufig, JsonSerializer.Serialize(this, Format));
        File.Copy(vorlaeufig, pfad, overwrite: true);
        File.Delete(vorlaeufig);
    }
}
