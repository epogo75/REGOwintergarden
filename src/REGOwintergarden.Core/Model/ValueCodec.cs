using System;
using System.Globalization;
using REGOwintergarden.Knx;

namespace REGOwintergarden.Model;

/// <summary>
/// Setzt einen hingeschriebenen Wert in das um, was auf der Leitung steht -
/// anhand des Datenpunkttyps der Aktion.
///
/// Das ist die Stelle, an der eine Szene lesbar bleibt. In der Liste steht
/// „Licht Kueche · schalten · ein" und nicht „0x81 auf 1/0/1"; was daraus
/// wird, entscheidet der Datenpunkttyp, den REGOdeploy ohnehin mitliefert.
///
/// Angenommen wird grosszuegig, was ein Mensch tippt: <c>ein</c>, <c>an</c>,
/// <c>1</c>, <c>true</c> heissen dasselbe. Ausgegeben wird dagegen eng - eine
/// Szene, die je nach Schreibweise etwas anderes sendet, waere schlimmer als
/// eine, die eine Eingabe ablehnt.
/// </summary>
public static class ValueCodec
{
    /// <summary>
    /// Die Hauptnummer eines Datenpunkttyps: aus <c>9.001</c> wird 9, aus
    /// <c>DPST-9-1</c> ebenfalls 9. Null heisst: unbekannt.
    /// </summary>
    public static int MainNumber(string? dpt)
    {
        var lesbar = ProjectImport.Lesbar(dpt ?? "");
        if (lesbar.Length == 0) return 0;
        var punkt = lesbar.IndexOf('.');
        var kopf = punkt > 0 ? lesbar.Substring(0, punkt) : lesbar;
        return int.TryParse(kopf, NumberStyles.None, CultureInfo.InvariantCulture, out var haupt) ? haupt : 0;
    }

    /// <summary>
    /// Wandelt einen Text in die Bytes des Datenpunkttyps.
    /// <paramref name="fehler"/> nennt den Grund, wenn es nicht geht.
    /// </summary>
    public static Payload? Encode(string? dpt, string? text, out string fehler)
    {
        fehler = "";
        var wert = (text ?? "").Trim();
        var haupt = MainNumber(dpt);

        try
        {
            switch (haupt)
            {
                case 1:
                    if (!TryBool(wert, out var bit)) break;
                    return Dpt.Dpt1Encode(bit);

                case 3:
                    // Relatives Dimmen: „heller" und „dunkler" starten,
                    // „stopp" haelt an. Die Schrittweite 1 ist die volle
                    // Spanne, wie sie ein Taster sendet.
                    if (IstEines(wert, "stopp", "stop", "0")) return Dpt.Dpt3Encode(true, 0);
                    if (IstEines(wert, "heller", "auf", "+")) return Dpt.Dpt3Encode(true, 1);
                    if (IstEines(wert, "dunkler", "ab", "-")) return Dpt.Dpt3Encode(false, 1);
                    break;

                case 5:
                    if (!TryZahl(wert, out var zahl)) break;
                    // 5.001 zaehlt in Prozent, 5.010 in ganzen Schritten.
                    // Dasselbe Byte, zwei Lesarten - und wer sie verwechselt,
                    // schickt aus 50 Prozent eine 50 von 255.
                    return IstRoh(dpt)
                        ? Payload.FromBytes((byte)Math.Clamp(Math.Round(zahl), 0, 255))
                        : Dpt.Dpt5Encode((int)Math.Clamp(Math.Round(zahl), 0, 100));

                case 6:
                    if (!TryZahl(wert, out var vorzeichen)) break;
                    return Dpt.Dpt6Encode((sbyte)Math.Clamp(Math.Round(vorzeichen), -128, 127));

                case 7:
                    if (!TryZahl(wert, out var zwei)) break;
                    return Dpt.Dpt7Encode((int)Math.Clamp(Math.Round(zwei), 0, 65535));

                case 9:
                    if (!TryZahl(wert, out var gleit)) break;
                    return Dpt.Dpt9Encode((float)gleit);

                case 14:
                    if (!TryZahl(wert, out var gross)) break;
                    return Dpt.Dpt14Encode(gross);

                case 16:
                    return Dpt.Dpt16Encode(wert);

                case 17:
                    // Szenennummer, blank: keine Lernbits, nur die Zahl. Auf
                    // der Leitung ab null gezaehlt, auf dem Papier ab eins -
                    // hier steht die Nummer aus dem Projekt.
                    if (!TryZahl(wert, out var nummer17)) break;
                    return Payload.FromBytes((byte)((int)Math.Clamp(Math.Round(nummer17), 1, 64) - 1));

                case 18:
                    // Szenensteuerung: das oberste Bit heisst speichern, die
                    // unteren sechs tragen die Nummer. Gezaehlt wird auf der
                    // Leitung ab null, auf dem Papier ab eins - hier steht die
                    // Nummer, die im Projekt steht, und nicht die um eins
                    // kleinere.
                    var speichern = wert.IndexOf("speich", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!TryZahl(speichern ? Ziffern(wert) : wert, out var szene)) break;
                    var nummer = (int)Math.Clamp(Math.Round(szene), 1, 64) - 1;
                    return Payload.FromBytes((byte)(nummer | (speichern ? 0x80 : 0x00)));

                case 20:
                    if (!TryZahl(wert, out var art)) break;
                    return Payload.FromBytes((byte)Math.Clamp(Math.Round(art), 0, 255));

                case 232:
                    if (!TryFarbe(wert, out var r, out var g, out var b, out _)) break;
                    return Dpt.Dpt232Encode(r, g, b);

                case 251:
                    if (!TryFarbe(wert, out var r2, out var g2, out var b2, out var w2)) break;
                    return Dpt.Dpt251Encode(r2, g2, b2, w2);

                default:
                    fehler = dpt is null || dpt.Length == 0
                        ? "Fuer diese Aktion steht kein Datenpunkttyp fest."
                        : "Datenpunkttyp " + dpt + " wird hier nicht unterstuetzt.";
                    return null;
            }
        }
        catch (KnxException ex)
        {
            fehler = ex.Message;
            return null;
        }

        fehler = wert.Length == 0
            ? "Kein Wert angegeben."
            : "Mit " + wert + " kann ein Objekt vom Typ " + ProjectImport.Lesbar(dpt ?? "") + " nichts anfangen.";
        return null;
    }

    /// <summary>
    /// Zurueck: aus den Bytes wieder ein lesbarer Wert.
    ///
    /// Gebraucht wird das fuer die Hausansicht - was auf einer
    /// Rueckmeldeadresse ankommt, soll dort als „ein" oder „21,5 °C" stehen
    /// und nicht als Bytefolge. <c>null</c> heisst: passt nicht zum Typ.
    /// </summary>
    public static string? Decode(string? dpt, Payload payload)
    {
        var k = CultureInfo.CurrentCulture;
        try
        {
            switch (MainNumber(dpt))
            {
                case 1: return Dpt.Dpt1Decode(payload) ? "ein" : "aus";
                case 3:
                    var (heller, schritt) = Dpt.Dpt3Decode(payload);
                    return schritt == 0 ? "stopp" : heller ? "heller" : "dunkler";
                case 5:
                    if (payload.IsSmall || payload.Bytes.Length != 1) return null;
                    return IstRoh(dpt)
                        ? payload.Bytes[0].ToString(k)
                        : Math.Round(payload.Bytes[0] / 255.0 * 100.0).ToString("0", k) + " %";
                case 6: return Dpt.Dpt6Decode(payload).ToString(k);
                case 7: return Dpt.Dpt7Decode(payload).ToString(k);
                case 9: return Dpt.Dpt9Decode(payload).ToString("0.0", k);
                case 14: return Dpt.Dpt14Decode(payload).ToString("0.##", k);
                case 16: return Dpt.Dpt16Decode(payload);
                case 17:
                    if (payload.IsSmall || payload.Bytes.Length != 1) return null;
                    return "Szene " + ((payload.Bytes[0] & 0x3F) + 1).ToString(k);

                case 18:
                    if (payload.IsSmall || payload.Bytes.Length != 1) return null;
                    var roh = payload.Bytes[0];
                    return "Szene " + ((roh & 0x3F) + 1).ToString(k)
                           + ((roh & 0x80) != 0 ? " speichern" : "");

                case 20:
                    if (payload.IsSmall || payload.Bytes.Length != 1) return null;
                    return payload.Bytes[0].ToString(k);
                case 232:
                    var (r, g, b) = Dpt.Dpt232Decode(payload);
                    return string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}", r, g, b);
                case 251:
                    var (r2, g2, b2, w2, _) = Dpt.Dpt251Decode(payload);
                    return string.Format(CultureInfo.InvariantCulture,
                        "#{0:X2}{1:X2}{2:X2}{3:X2}", r2, g2, b2, w2);
                default: return null;
            }
        }
        catch (KnxException)
        {
            return null;
        }
    }

    /// <summary>
    /// Der Zahlenwert eines Prozentobjekts, 0 bis 100 - fuer Regler in der
    /// Hausansicht. <c>null</c>, wenn es keiner ist.
    /// </summary>
    public static double? Percent(string? dpt, Payload payload)
    {
        if (MainNumber(dpt) != 5) return null;
        if (payload.IsSmall || payload.Bytes.Length != 1) return null;
        return IstRoh(dpt) ? payload.Bytes[0] : payload.Bytes[0] / 255.0 * 100.0;
    }

    /// <summary>
    /// Was man bei diesem Datenpunkttyp hinschreiben kann - als Hilfe im
    /// Eingabefeld und als Vorschlagsliste.
    /// </summary>
    public static string[] Suggestions(string? dpt) => MainNumber(dpt) switch
    {
        1 => new[] { "ein", "aus" },
        3 => new[] { "heller", "dunkler", "stopp" },
        5 => IstRoh(dpt) ? new[] { "0", "128", "255" } : new[] { "0", "50", "100" },
        7 => new[] { "2700", "4000", "6500" },
        9 => new[] { "21,0", "22,5" },
        16 => new[] { "Guten Morgen" },
        17 => new[] { "1", "2", "3" },
        18 => new[] { "1", "2", "3", "1 speichern" },
        20 => new[] { "1", "2", "3" },
        232 => new[] { "#FF8800", "#FFFFFF" },
        251 => new[] { "#FF8800FF" },
        _ => Array.Empty<string>(),
    };

    /// <summary>Ein kurzer Hinweis, was hier erwartet wird.</summary>
    public static string Hint(string? dpt) => MainNumber(dpt) switch
    {
        1 => "ein oder aus",
        3 => "heller, dunkler oder stopp",
        5 => IstRoh(dpt) ? "0 bis 255" : "0 bis 100 Prozent",
        6 => "-128 bis 127",
        7 => "0 bis 65535, bei Farbtemperatur in Kelvin",
        9 => "Zahl mit Komma, etwa 21,5",
        16 => "Text, bis vierzehn Zeichen",
        17 => "Szenennummer 1 bis 64",
        18 => "Szenennummer 1 bis 64, zum Ablegen ein „speichern\" dahinter",
        20 => "Betriebsart als Zahl",
        232 => "Farbe als #RRGGBB",
        251 => "Farbe als #RRGGBBWW",
        _ => "",
    };

    /// <summary>
    /// 5.010 ist ein Zaehlwert von 0 bis 255, 5.001 ein Prozentwert. Ohne
    /// Untertyp wird Prozent angenommen: das ist der haeufigere Fall, und ein
    /// Prozentwert auf einem Zaehlobjekt faellt sofort auf, umgekehrt nicht.
    /// </summary>
    private static bool IstRoh(string? dpt)
    {
        var lesbar = ProjectImport.Lesbar(dpt ?? "");
        return lesbar.StartsWith("5.010", StringComparison.Ordinal)
               || lesbar.StartsWith("5.005", StringComparison.Ordinal);
    }

    /// <summary>Nur die Ziffern - aus „3 speichern" wird „3".</summary>
    private static string Ziffern(string wert)
    {
        var gebaut = new System.Text.StringBuilder(wert.Length);
        foreach (var zeichen in wert)
        {
            if (char.IsDigit(zeichen)) gebaut.Append(zeichen);
        }
        return gebaut.ToString();
    }

    private static bool IstEines(string wert, params string[] worte)
    {
        foreach (var wort in worte)
        {
            if (string.Equals(wert, wort, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static bool TryBool(string wert, out bool ergebnis)
    {
        ergebnis = false;
        if (IstEines(wert, "ein", "an", "1", "true", "ja", "auf")) { ergebnis = true; return true; }
        if (IstEines(wert, "aus", "0", "false", "nein", "ab", "zu")) { ergebnis = false; return true; }
        return false;
    }

    /// <summary>
    /// Zahlen mit Komma und mit Punkt annehmen. Wer 21,5 tippt, meint
    /// einundzwanzigeinhalb - und wer 21.5 tippt, auch.
    /// </summary>
    private static bool TryZahl(string wert, out double zahl) =>
        double.TryParse(wert, NumberStyles.Float, CultureInfo.CurrentCulture, out zahl)
        || double.TryParse(wert.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out zahl);

    /// <summary>
    /// Eine Farbe als <c>#RRGGBB</c> oder <c>#RRGGBBWW</c>, mit und ohne
    /// Doppelkreuz.
    /// </summary>
    private static bool TryFarbe(string wert, out byte r, out byte g, out byte b, out byte w)
    {
        r = g = b = w = 0;
        var text = wert.TrimStart('#').Trim();
        if (text.Length != 6 && text.Length != 8) return false;

        static bool Hex(string s, out byte wert) =>
            byte.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out wert);

        if (!Hex(text.Substring(0, 2), out r)) return false;
        if (!Hex(text.Substring(2, 2), out g)) return false;
        if (!Hex(text.Substring(4, 2), out b)) return false;
        if (text.Length == 8 && !Hex(text.Substring(6, 2), out w)) return false;
        return true;
    }
}
