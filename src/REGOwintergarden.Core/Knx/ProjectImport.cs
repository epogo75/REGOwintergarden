using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace REGOwintergarden.Knx;

/// <summary>Eine Gruppenadresse aus einem KNX-Projekt.</summary>
public sealed class GroupAddressEntry
{
    public GroupAddressEntry(GroupAddress address, string name, string path, string datapoint)
    {
        Address = address;
        Name = name;
        Path = path;
        Datapoint = datapoint;
    }

    public GroupAddress Address { get; }

    /// <summary>Der Name der Adresse, etwa „Wohnen Decke schalten".</summary>
    public string Name { get; }

    /// <summary>Haupt- und Mittelgruppe, etwa „Licht / Erdgeschoss".</summary>
    public string Path { get; }

    /// <summary>Der Datenpunkttyp, wie er im Projekt steht - oder leer.</summary>
    public string Datapoint { get; }

    /// <summary>Alles in einer Zeile - fuer die Suche im Pool.</summary>
    public string Suchtext => $"{Address} {Name} {Path} {Datapoint}".ToLowerInvariant();

    public override string ToString() => $"{Address}  {Name}";
}

/// <summary>
/// Liest die Gruppenadressen aus einem KNX-Projekt.
///
/// Vier Formate, weil in der Praxis alle vier vorkommen: die
/// Projektdatei <c>.knxproj</c> selbst, der Gruppenadressexport der ETS als
/// XML oder CSV, und die alte OPC-Ausgabe <c>.esf</c>. Welches man vor sich
/// hat, entscheidet der Inhalt und nicht die Endung - eine umbenannte Datei
/// soll trotzdem funktionieren.
///
/// Bewusst ohne Fremdbibliothek: ZIP und XML bringt die Laufzeit mit, und
/// eine Abhaengigkeit fuer das Lesen von vier Textformaten waere ein
/// schlechter Tausch.
/// </summary>
public static class ProjectImport
{
    /// <summary>
    /// Liest eine Projektdatei. <paramref name="hinweis"/> sagt, was gelesen
    /// wurde oder warum nichts herauskam - eine leere Liste ohne Begruendung
    /// laesst den Anwender ratlos zurueck.
    /// </summary>
    public static IReadOnlyList<GroupAddressEntry> Load(string path, out string hinweis)
    {
        hinweis = "";
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            hinweis = "Datei nicht gefunden.";
            return Array.Empty<GroupAddressEntry>();
        }

        try
        {
            if (IstZip(path)) return AusProjekt(path, out hinweis);

            var text = Lesen(path);
            if (text.TrimStart().StartsWith("<", StringComparison.Ordinal))
            {
                var ausXml = AusXml(XDocument.Parse(text));
                hinweis = Bericht(ausXml.Count, "XML-Export");
                return ausXml;
            }

            var ausText = AusText(text);
            hinweis = Bericht(ausText.Count, "Textexport");
            return ausText;
        }
        catch (IOException ex)
        {
            hinweis = "Nicht lesbar: " + ex.Message;
        }
        catch (System.Xml.XmlException ex)
        {
            hinweis = "Kein gueltiges XML: " + ex.Message;
        }
        catch (InvalidDataException ex)
        {
            hinweis = "Kein gueltiges Archiv: " + ex.Message;
        }
        catch (UnauthorizedAccessException ex)
        {
            hinweis = "Kein Zugriff: " + ex.Message;
        }
        return Array.Empty<GroupAddressEntry>();
    }

    private static string Bericht(int anzahl, string art) => anzahl == 0
        ? $"Im {art} standen keine Gruppenadressen."
        : $"{anzahl.ToString(CultureInfo.CurrentCulture)} Gruppenadressen aus dem {art} gelesen.";

    private static bool IstZip(string path)
    {
        using var strom = File.OpenRead(path);
        return strom.ReadByte() == 'P' && strom.ReadByte() == 'K';
    }

    /// <summary>
    /// Text mit der Kodierung lesen, die tatsaechlich drinsteht. Die alten
    /// ESF-Dateien sind ISO-8859-1; wer sie als UTF-8 liest, bekommt aus
    /// jedem Umlaut ein Fragezeichen - und sucht die Adresse danach unter dem
    /// falschen Namen.
    /// </summary>
    private static string Lesen(string path)
    {
        var rohdaten = File.ReadAllBytes(path);
        if (rohdaten.Length >= 3 && rohdaten[0] == 0xEF && rohdaten[1] == 0xBB && rohdaten[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(rohdaten, 3, rohdaten.Length - 3);
        }

        var streng = new UTF8Encoding(false, throwOnInvalidBytes: true);
        try { return streng.GetString(rohdaten); }
        catch (DecoderFallbackException) { return Encoding.Latin1.GetString(rohdaten); }
    }

    // ---- .knxproj ---------------------------------------------------------

    /// <summary>
    /// Die Projektdatei ist ein ZIP. Darin liegt je Projekt ein Ordner
    /// <c>P-xxxx</c> mit <c>0.xml</c>, und dort stehen die Gruppenadressen -
    /// allerdings als reine Zahl, nicht dreistufig.
    ///
    /// Ist das Projekt mit einem Kennwort geschuetzt, liegt statt des Ordners
    /// ein verschluesseltes ZIP darin. Das laesst sich nicht oeffnen, und das
    /// gehoert gesagt statt einer leeren Liste.
    /// </summary>
    private static IReadOnlyList<GroupAddressEntry> AusProjekt(string path, out string hinweis)
    {
        using var archiv = ZipFile.OpenRead(path);

        var alle = new List<GroupAddressEntry>();
        var geschuetzt = false;

        foreach (var eintrag in archiv.Entries)
        {
            var name = eintrag.FullName;
            if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) geschuetzt = true;
            if (!name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) continue;
            // knxmaster und Kataloge enthalten keine Gruppenadressen; sie zu
            // lesen kostet bei grossen Projekten spuerbar Zeit.
            if (name.IndexOf("0.xml", StringComparison.OrdinalIgnoreCase) < 0
                && name.IndexOf("project.xml", StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            using var strom = eintrag.Open();
            XDocument dokument;
            try { dokument = XDocument.Load(strom); }
            catch (System.Xml.XmlException) { continue; }
            alle.AddRange(AusXml(dokument));
        }

        var einmalig = Ordnen(alle);
        hinweis = einmalig.Count > 0
            ? Bericht(einmalig.Count, "Projekt")
            : geschuetzt
                ? "Das Projekt ist mit einem Kennwort geschuetzt und laesst sich nicht lesen. "
                  + "In der ETS ohne Kennwort erneut sichern, oder die Gruppenadressen exportieren."
                : "In der Projektdatei standen keine Gruppenadressen.";
        return einmalig;
    }

    // ---- XML --------------------------------------------------------------

    /// <summary>
    /// Sowohl die Projektdatei als auch der Gruppenadressexport benutzen
    /// <c>GroupRange</c> und <c>GroupAddress</c>. Der Unterschied steckt im
    /// Adressattribut: im Export steht dort <c>1/2/3</c>, in der Projektdatei
    /// die nackte Zahl. Beides wird hier angenommen.
    /// </summary>
    private static List<GroupAddressEntry> AusXml(XDocument dokument)
    {
        var alle = new List<GroupAddressEntry>();
        var wurzel = dokument.Root;
        if (wurzel is null) return alle;

        void Gehen(XElement element, string pfad)
        {
            foreach (var kind in element.Elements())
            {
                var kurz = kind.Name.LocalName;
                if (string.Equals(kurz, "GroupRange", StringComparison.Ordinal))
                {
                    var name = Attribut(kind, "Name");
                    Gehen(kind, string.IsNullOrEmpty(pfad) ? name : pfad + " / " + name);
                    continue;
                }
                if (string.Equals(kurz, "GroupAddress", StringComparison.Ordinal))
                {
                    var adresse = Adresse(Attribut(kind, "Address"));
                    if (adresse is null) continue;
                    alle.Add(new GroupAddressEntry(
                        adresse.Value,
                        Attribut(kind, "Name"),
                        pfad,
                        Datenpunkt(kind)));
                    continue;
                }
                Gehen(kind, pfad);
            }
        }

        Gehen(wurzel, "");
        return alle;
    }

    private static string Attribut(XElement element, string name) =>
        element.Attribute(name)?.Value ?? "";

    private static string Datenpunkt(XElement element)
    {
        var roh = Attribut(element, "DatapointType");
        if (roh.Length == 0) roh = Attribut(element, "DPTs");
        return Lesbar(roh);
    }

    /// <summary>
    /// Aus <c>DPST-9-1</c> wird 9.001, aus <c>DPT-1</c> wird 1.*. So steht es
    /// in jedem Handbuch, und so sucht man danach.
    /// </summary>
    public static string Lesbar(string roh)
    {
        if (string.IsNullOrWhiteSpace(roh)) return "";
        var teile = roh.Trim().Split(new[] { '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (teile.Length >= 3 && teile[0].StartsWith("DPST", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(teile[1], NumberStyles.None, CultureInfo.InvariantCulture, out var haupt)
            && int.TryParse(teile[2], NumberStyles.None, CultureInfo.InvariantCulture, out var unter))
        {
            return haupt.ToString(CultureInfo.InvariantCulture) + "."
                   + unter.ToString("000", CultureInfo.InvariantCulture);
        }
        if (teile.Length >= 2 && teile[0].StartsWith("DPT", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(teile[1], NumberStyles.None, CultureInfo.InvariantCulture, out var nur))
        {
            return nur.ToString(CultureInfo.InvariantCulture) + ".*";
        }
        return roh.Trim();
    }

    // ---- ESF und CSV ------------------------------------------------------

    /// <summary>
    /// Fuer die beiden Textformate wird nicht auf Spalten gezaehlt, sondern
    /// nach einer Adresse gesucht: in jeder Zeile das Feld, das wie
    /// <c>1/2/3</c> aussieht, und als Name das laengste der uebrigen Felder.
    ///
    /// Das klingt grob und ist es auch - aber die ETS schreibt CSV je nach
    /// Einstellung mit Semikolon, Komma oder Tabulator, mit und ohne
    /// Anfuehrungszeichen, mit wechselnder Spaltenzahl und in mehreren
    /// Sprachen. Auf die Spaltenreihenfolge zu bauen hiesse, sich auf eine
    /// Ausgabe festzulegen, die der naechste Anwender anders eingestellt hat.
    /// </summary>
    private static List<GroupAddressEntry> AusText(string text)
    {
        var alle = new List<GroupAddressEntry>();
        foreach (var zeile in text.Split('\n'))
        {
            var felder = zeile.Split('\t', ';', ',');
            if (felder.Length < 2) continue;

            GroupAddress? adresse = null;
            var index = -1;
            for (var i = 0; i < felder.Length; i++)
            {
                var kandidat = Adresse(Saeubern(felder[i]));
                if (kandidat is null) continue;
                adresse = kandidat;
                index = i;
                break;
            }
            if (adresse is null) continue;

            // Der Name ist das laengste Textfeld, das keine Adresse ist. Der
            // Datenpunkttyp steht meist dahinter und faengt mit einer Ziffer
            // oder mit DPT an.
            var name = "";
            var dpt = "";
            for (var i = 0; i < felder.Length; i++)
            {
                if (i == index) continue;
                var wert = Saeubern(felder[i]);
                if (wert.Length == 0) continue;
                if (SiehtNachDatenpunktAus(wert)) { if (dpt.Length == 0) dpt = Lesbar(wert); continue; }
                if (wert.Length > name.Length) name = wert;
            }
            alle.Add(new GroupAddressEntry(adresse.Value, name, "", dpt));
        }
        return Ordnen(alle);
    }

    private static string Saeubern(string feld) => feld.Trim().Trim('"').Trim();

    private static bool SiehtNachDatenpunktAus(string wert)
    {
        if (wert.StartsWith("DPT", StringComparison.OrdinalIgnoreCase)) return true;
        if (wert.StartsWith("EIS", StringComparison.OrdinalIgnoreCase)) return true;
        var punkt = wert.IndexOf('.');
        if (punkt <= 0 || punkt == wert.Length - 1) return false;
        return int.TryParse(wert.AsSpan(0, punkt), NumberStyles.None, CultureInfo.InvariantCulture, out _)
               && int.TryParse(wert.AsSpan(punkt + 1), NumberStyles.None, CultureInfo.InvariantCulture, out _);
    }

    // ---- Hilfen -----------------------------------------------------------

    /// <summary>
    /// Eine Adresse aus <c>1/2/3</c>, <c>1/2</c> oder der nackten Zahl.
    /// Bereichszeilen wie <c>1/-/-</c> ergeben nichts - das sind Ordner und
    /// keine Adressen.
    /// </summary>
    private static GroupAddress? Adresse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var wert = text.Trim();
        if (wert.Contains('-')) return null;

        try
        {
            var teile = wert.Split('/');
            return teile.Length switch
            {
                3 => GroupAddress.Parse3Level(wert),
                2 => GroupAddress.Parse2Level(wert),
                1 => GroupAddress.ParseFree(wert),
                _ => null,
            };
        }
        catch (KnxException)
        {
            return null;
        }
    }

    /// <summary>
    /// Nach Adresse sortiert und ohne Dubletten. Ein Projekt bringt dieselbe
    /// Adresse gern mehrfach mit, wenn mehrere Dateien darin liegen.
    /// </summary>
    private static List<GroupAddressEntry> Ordnen(IEnumerable<GroupAddressEntry> alle)
    {
        var gesehen = new HashSet<string>(StringComparer.Ordinal);
        var ergebnis = new List<GroupAddressEntry>();
        foreach (var eintrag in alle)
        {
            if (!gesehen.Add(eintrag.Address.ToString())) continue;
            ergebnis.Add(eintrag);
        }
        ergebnis.Sort((a, b) => string.CompareOrdinal(Sortierbar(a.Address), Sortierbar(b.Address)));
        return ergebnis;
    }

    /// <summary>
    /// Zum Sortieren die Teile auf feste Breite bringen, sonst steht 1/1/10
    /// vor 1/1/2.
    /// </summary>
    private static string Sortierbar(GroupAddress adresse)
    {
        var teile = adresse.ToString().Split('/');
        return string.Join("/", teile.Select(t =>
            int.Parse(t, CultureInfo.InvariantCulture).ToString("000", CultureInfo.InvariantCulture)));
    }
}
