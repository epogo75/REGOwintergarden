using System;
using System.Globalization;
using System.Reflection;

namespace REGOwintergarden.App;

/// <summary>
/// Wer das Programm ist und welchen Stand man vor sich hat.
///
/// Das ist keine Formsache. Auf dem Ablageordner liegen regelmaessig mehrere
/// Fassungen nebeneinander, weil sich eine laufende .exe nicht ueberschreiben
/// laesst - und dann ist die Frage, welche gerade offen ist, nicht mehr zu
/// beantworten. Deshalb steht die Nummer im Fenstertitel und nicht nur in
/// einem Feld, das man erst suchen muss.
/// </summary>
public static class Programmstand
{
    /// <summary>Etwa <c>1.0.280826</c> - die letzten sechs Stellen sind das Baudatum.</summary>
    public static string Version { get; } = Lies();

    public static string Name => "REGOwintergarden";

    public static string Urheber => "© 2026 Stephan Ruf";

    /// <summary>Titelzeile eines Fensters, mit Zusatz.</summary>
    public static string Titel(string? zusatz = null) => string.IsNullOrWhiteSpace(zusatz)
        ? $"{Name} {Version}"
        : $"{Name} {Version} — {zusatz}";

    private static string Lies()
    {
        var assembly = typeof(Programmstand).Assembly;

        // InformationalVersion ist eine freie Zeichenkette und traegt deshalb
        // die vollstaendige Nummer. Das SDK haengt bei eingeschaltetem
        // SourceLink noch die Quelltextkennung mit einem Pluszeichen an - die
        // gehoert nicht in einen Fenstertitel.
        var frei = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(frei))
        {
            var plus = frei.IndexOf('+');
            return plus > 0 ? frei.Substring(0, plus) : frei;
        }

        // Faellt das aus, bleibt die Assemblyversion. Besser eine grobe Nummer
        // als gar keine - ohne sie waere jede Fehlersuche wieder ein Raten.
        return assembly.GetName().Version?.ToString(3) ?? "unbekannt";
    }

    /// <summary>
    /// Die Buildnummer allein - die letzten sechs Stellen der Fassung, als
    /// Tag, Monat und Jahr.
    /// </summary>
    public static string Build
    {
        get
        {
            var punkt = Version.LastIndexOf('.');
            return punkt >= 0 && punkt + 1 < Version.Length ? Version.Substring(punkt + 1) : Version;
        }
    }

    /// <summary>
    /// Das Baudatum in lesbarer Form.
    ///
    /// Die Nummer ist ein Datum - nur eines, das man beim Ablesen erst
    /// zerlegen muss. Wer zwei Fassungen nebeneinander liegen hat, will nicht
    /// rechnen, sondern sehen, welche die neuere ist. Faellt das Zerlegen aus,
    /// gilt das Datum der Programmdatei; die luegt nicht, sie ist nur
    /// ungenauer.
    /// </summary>
    public static string Baudatum
    {
        get
        {
            var nummer = Build;
            if (nummer.Length == 6
                && int.TryParse(nummer.Substring(0, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var tag)
                && int.TryParse(nummer.Substring(2, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var monat)
                && int.TryParse(nummer.Substring(4, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var jahr)
                && monat is >= 1 and <= 12 && tag >= 1 && tag <= DateTime.DaysInMonth(2000 + jahr, monat))
            {
                return new DateTime(2000 + jahr, monat, tag).ToString("dd.MM.yyyy", CultureInfo.CurrentCulture);
            }

            try
            {
                var datei = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(datei) && System.IO.File.Exists(datei))
                {
                    return System.IO.File.GetLastWriteTime(datei)
                        .ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture);
                }
            }
            catch (System.IO.IOException) { }
            catch (UnauthorizedAccessException) { }

            return "";
        }
    }

    /// <summary>Die Laufzeit, auf der es gerade laeuft - fuer das Ueber-Feld.</summary>
    public static string Laufzeit => string.Format(
        CultureInfo.InvariantCulture, ".NET {0} auf {1}",
        Environment.Version, Environment.OSVersion.VersionString);
}
