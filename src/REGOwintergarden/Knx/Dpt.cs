using System;

namespace REGOwintergarden.Knx;

/// <summary>
/// Umrechnung zwischen Werten und den Bytes der Datenpunkttypen, die dieser
/// Helfer beherrscht.
///
/// Die Rundungsregeln sind hier kein Geschmacksfrage. Sie sind gegen eine
/// echte xknx-Installation abgeglichen, und zwei der Faelle unterscheiden
/// sich nur an genau einem Bit — siehe die Bemerkungen bei
/// <see cref="Dpt5Encode"/> und <see cref="Dpt9Encode"/>. Wer hier
/// „aufraeumt" und ueberall dieselbe Rundung nimmt, verschiebt jeden
/// zweiten Sollwert um ein halbes Bit.
/// </summary>
public static class Dpt
{
    // ---- DPT 1.x: Schalten, ein Bit -------------------------------------

    public static Payload Dpt1Encode(bool value) => Payload.FromSmall(value ? (byte)1 : (byte)0);

    public static bool Dpt1Decode(Payload payload)
    {
        if (!payload.IsSmall) throw KnxException.Truncated();
        return payload.Small != 0;
    }

    // ---- DPT 3.007: Dimmen mit Schrittweite ------------------------------

    public static Payload Dpt3Encode(bool increase, int stepCode)
    {
        if (stepCode < 0 || stepCode > 7)
        {
            throw KnxException.ValueOutOfRange("DPT 3.007 Schrittweite ueber 7");
        }
        return Payload.FromSmall((byte)(((increase ? 1 : 0) << 3) | stepCode));
    }

    public static (bool Increase, int StepCode) Dpt3Decode(Payload payload)
    {
        if (!payload.IsSmall) throw KnxException.Truncated();
        return ((payload.Small & 0b1000) != 0, payload.Small & 0b0111);
    }

    // ---- DPT 5.001: Prozent, ein Byte ------------------------------------

    public static Payload Dpt5Encode(int percent)
    {
        if (percent < 0 || percent > 100)
        {
            throw KnxException.ValueOutOfRange("DPT 5.001 Prozentwert ueber 100");
        }
        // Rundung zur geraden Zahl, nicht vom Nullpunkt weg. 30 % und 70 %
        // landen genau auf .5 (76,5 und 178,5) — kaufmaennisch gerundet kaeme
        // 0x4d/0xb3 heraus, xknx liefert aber 0x4c/0xb2.
        var raw = MathF.Round(percent / 100f * 255f, MidpointRounding.ToEven);
        return Payload.FromBytes((byte)raw);
    }

    public static int Dpt5Decode(Payload payload)
    {
        var raw = SingleByte(payload);
        // Hier dagegen kaufmaennisch — das ist die Rundung, die xknx beim
        // Zurueckrechnen verwendet, und sie ist nicht dieselbe wie oben.
        return (int)MathF.Round(raw / 255f * 100f, MidpointRounding.AwayFromZero);
    }

    // ---- DPT 6.010: vorzeichenbehaftetes Byte ----------------------------

    public static Payload Dpt6Encode(sbyte value) => Payload.FromBytes(unchecked((byte)value));

    public static sbyte Dpt6Decode(Payload payload) => unchecked((sbyte)SingleByte(payload));

    // ---- DPT 9.x: Gleitkomma, zwei Byte ----------------------------------

    /// <summary>
    /// Groesster und kleinster Wert, den das Format aus 4 Bit Exponent und
    /// 11 Bit Zweierkomplement-Mantisse fassen kann, in Hundertsteln:
    /// 2047 &lt;&lt; 15 und -2048 &lt;&lt; 15. Alles darueber wird begrenzt,
    /// statt den Exponenten in das Vorzeichenbit ueberlaufen zu lassen.
    /// </summary>
    private const float Dpt9RawMax = 67_076_096f;
    private const float Dpt9RawMin = -67_108_864f;

    public static Payload Dpt9Encode(float value)
    {
        // Zuerst begrenzen — das faengt nebenbei NaN und die Unendlichkeiten
        // ab, die die Halbierungsschleife weiter unten sonst ewig drehen
        // liessen (NaN/2 bleibt NaN, unendlich/2 bleibt unendlich).
        var clamped = Math.Clamp(value * 100f, Dpt9RawMin, Dpt9RawMax);
        var knxValue = float.IsNaN(clamped) ? 0f : clamped;

        // Der Nullfall braucht eine eigene Abzweigung: ohne ihn wuerde ein
        // winziger negativer Wert wie -0,001 zu 0x8000 — und das liest sich
        // als -20,48 zurueck. Bei einem Sollwert ist das kein Rundungsfehler,
        // sondern eine falsche Temperatur.
        if (MathF.Round(knxValue, MidpointRounding.ToEven) == 0f)
        {
            return Payload.FromBytes(0x00, 0x00);
        }

        var exponent = 0;
        while (knxValue < -2048f || knxValue > 2047f)
        {
            exponent++;
            knxValue /= 2f;
        }

        // Rundung zur geraden Zahl, und zwar genau einmal ganz am Schluss.
        // xknx halbiert in Gleitkomma und rundet erst am Ende; erst runden
        // und dann arithmetisch schieben sieht gleichwertig aus, ist es aber
        // nicht — das Schieben schneidet ab, und ueber rund 43 % des
        // Wertebereichs von DPT 9.001 kommt dabei ein halbes Bit zu wenig
        // heraus. Beispiel: 1000,0 wird sonst 0x361b statt 0x361a.
        var mantissa = unchecked((ushort)(int)MathF.Round(knxValue, MidpointRounding.ToEven)) & 0x07ff;
        var msb = (byte)(((exponent << 3) | (mantissa >> 8)) & 0xff);
        if (knxValue < 0f) msb |= 0x80;
        return Payload.FromBytes(msb, (byte)(mantissa & 0xff));
    }

    public static float Dpt9Decode(Payload payload)
    {
        var bytes = TwoBytes(payload);
        var raw = (bytes[0] << 8) | bytes[1];
        var exponent = (raw >> 11) & 0x0f;
        var significand = raw & 0x07ff;
        if ((raw >> 15) == 1) significand -= 2048;
        return (significand << exponent) / 100f;
    }

    // ---- DPT 20.102: HVAC-Betriebsart ------------------------------------

    public static Payload Dpt20Encode(byte mode) => Payload.FromBytes(mode);

    public static byte Dpt20Decode(Payload payload)
    {
        var raw = SingleByte(payload);
        // Das Verschluesseln prueft nicht, das Entschluesseln schon: ein
        // Helfer, der vom Bus eine 5 bekommt, soll sie zurueckweisen und
        // nicht eine unbestimmte Betriebsart durchreichen.
        if (raw > 4) throw KnxException.ValueOutOfRange("DPT 20.102 HVAC-Modus ueber 4");
        return raw;
    }

    // ---- DPT 7.x: zwei Byte ohne Vorzeichen -------------------------------

    /// <summary>
    /// Zwei Byte ohne Vorzeichen, in dieser Anwendung die Farbtemperatur nach
    /// DPT 7.600 in Kelvin. Anders als DPT 9 ist das keine Gleitkommazahl,
    /// sondern schlicht ein Zaehlwert - 3000 K sind die Bytes 0x0B 0xB8.
    /// </summary>
    public static Payload Dpt7Encode(int value)
    {
        if (value < 0 || value > 65535) throw KnxException.ValueOutOfRange("DPT 7 ausserhalb 0 bis 65535");
        return Payload.FromBytes((byte)(value >> 8), (byte)(value & 0xff));
    }

    public static int Dpt7Decode(Payload payload)
    {
        var bytes = TwoBytes(payload);
        return (bytes[0] << 8) | bytes[1];
    }

    // ---- DPT 232.600: drei Byte Farbe -------------------------------------

    /// <summary>Rot, Gruen, Blau als je ein Byte.</summary>
    public static Payload Dpt232Encode(byte r, byte g, byte b) => Payload.FromBytes(r, g, b);

    public static (byte R, byte G, byte B) Dpt232Decode(Payload payload)
    {
        if (payload.IsSmall || payload.Bytes.Length != 3) throw KnxException.Truncated();
        return (payload.Bytes[0], payload.Bytes[1], payload.Bytes[2]);
    }

    // ---- DPT 251.600: RGBW mit Gueltigkeitsmaske --------------------------

    /// <summary>
    /// Sechs Byte: R, G, B, W, ein reserviertes Byte und die Maske.
    ///
    /// Die Maske ist der Teil, den man uebersieht. In ihren unteren vier Bit
    /// steht, welche der vier Farben ueberhaupt gemeint sind - Bit 3 fuer Rot,
    /// Bit 2 fuer Gruen, Bit 1 fuer Blau, Bit 0 fuer Weiss. Wer sie auf null
    /// laesst, schickt ein vollstaendiges Telegramm, das nichts aendert, und
    /// sucht den Fehler danach im Aktor.
    /// </summary>
    public static Payload Dpt251Encode(byte r, byte g, byte b, byte w, int mask = 0x0f) =>
        Payload.FromBytes(r, g, b, w, 0x00, (byte)(mask & 0x0f));

    public static (byte R, byte G, byte B, byte W, int Mask) Dpt251Decode(Payload payload)
    {
        if (payload.IsSmall || payload.Bytes.Length != 6) throw KnxException.Truncated();
        var b = payload.Bytes;
        return (b[0], b[1], b[2], b[3], b[5] & 0x0f);
    }

    // ---- DPT 16.000: vierzehn Byte Text ----------------------------------

    /// <summary>
    /// Fest vierzehn Byte, mit Nullen aufgefuellt, ein Zeichen je Byte.
    ///
    /// Bewusst ISO-8859-1 und nicht UTF-8: ein Umlaut belegt hier genau ein
    /// Byte. In UTF-8 waeren es zwei, und dann passten statt vierzehn Zeichen
    /// nur noch dreizehn hinein - der Text waere je nach Inhalt
    /// unterschiedlich lang abgeschnitten.
    ///
    /// Laenger als vierzehn Zeichen wird abgeschnitten, nicht abgelehnt: so
    /// verhaelt sich das Format, und ein Fehler waere hier unangebracht.
    /// </summary>
    /// <summary>
    /// DPT 14: vier Byte Gleitkomma nach IEEE 754, in Netzreihenfolge.
    ///
    /// Wetterstationen melden darin, was nicht in die zwei Byte von DPT 9
    /// passt - Sonnenazimut und -elevation etwa, wo es auf Nachkommastellen
    /// ankommt und der Wertebereich ueber 670 760 hinausreicht.
    /// </summary>
    public static Payload Dpt14Encode(double value)
    {
        var bytes = BitConverter.GetBytes((float)value);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        return Payload.FromBytes(bytes);
    }

    public static float Dpt14Decode(Payload payload)
    {
        if (payload.IsSmall || payload.Bytes.Length != 4)
        {
            throw KnxException.Truncated();
        }
        var bytes = new byte[4];
        Array.Copy(payload.Bytes, bytes, 4);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        return BitConverter.ToSingle(bytes, 0);
    }

    public static Payload Dpt16Encode(string text)
    {
        var bytes = new byte[14];
        var quelle = System.Text.Encoding.Latin1.GetBytes(text ?? "");
        Array.Copy(quelle, bytes, Math.Min(quelle.Length, bytes.Length));
        return Payload.FromBytes(bytes);
    }

    public static string Dpt16Decode(Payload payload)
    {
        if (payload.IsSmall || payload.Bytes.Length != 14) throw KnxException.Truncated();
        var ende = Array.IndexOf(payload.Bytes, (byte)0);
        if (ende < 0) ende = payload.Bytes.Length;
        return System.Text.Encoding.Latin1.GetString(payload.Bytes, 0, ende);
    }

    // ---- Hilfen ----------------------------------------------------------

    private static byte SingleByte(Payload payload)
    {
        if (payload.IsSmall || payload.Bytes.Length != 1) throw KnxException.Truncated();
        return payload.Bytes[0];
    }

    private static byte[] TwoBytes(Payload payload)
    {
        if (payload.IsSmall || payload.Bytes.Length != 2) throw KnxException.Truncated();
        return payload.Bytes;
    }
}
