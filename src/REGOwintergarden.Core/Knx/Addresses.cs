using System;
using System.Globalization;

namespace REGOwintergarden.Knx;

/// <summary>
/// Physikalische Adresse eines Geraets, Bereich.Linie.Teilnehmer.
/// </summary>
public readonly struct IndividualAddress : IEquatable<IndividualAddress>
{
    private readonly ushort _raw;

    private IndividualAddress(ushort raw) => _raw = raw;

    /// <summary>
    /// 0.0.0 — keine benutzbare Geraeteadresse, sondern der uebliche
    /// Platzhalter, den ein Tunnelclient in das Absenderfeld schreibt: das
    /// Gateway ersetzt ihn durch die Adresse, die es dem Tunnel zugeteilt hat.
    /// </summary>
    public static readonly IndividualAddress Zero = new(0);

    public static IndividualAddress Parse(string s)
    {
        var parts = (s ?? "").Split('.');
        if (parts.Length != 3) throw KnxException.InvalidAddress(s ?? "");
        if (!TryPart(parts[0], 15, out var area) ||
            !TryPart(parts[1], 15, out var main) ||
            !TryPart(parts[2], 255, out var line))
        {
            throw KnxException.InvalidAddress(s ?? "");
        }
        return new IndividualAddress((ushort)((area << 12) | (main << 8) | line));
    }

    internal static bool TryPart(string text, int max, out int value)
    {
        value = 0;
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)) return false;
        if (parsed < 0 || parsed > max) return false;
        value = parsed;
        return true;
    }

    /// <summary>
    /// Baut eine Adresse direkt aus ihren drei Bestandteilen. Der Aufrufer
    /// steht dafuer ein, dass sie in ihren Bereichen liegen — genau die
    /// Zusicherung, die <see cref="Parse"/> erzwingt und die
    /// <see cref="Area"/>/<see cref="Main"/>/<see cref="Line"/> geben.
    /// </summary>
    public static IndividualAddress FromParts(int area, int main, int line) =>
        new((ushort)((area << 12) | (main << 8) | line));

    public static IndividualAddress FromBytes(byte hi, byte lo) => new((ushort)((hi << 8) | lo));

    public byte[] ToBytes() => new[] { (byte)(_raw >> 8), (byte)(_raw & 0xff) };

    public int Area => (_raw >> 12) & 0x0f;
    public int Main => (_raw >> 8) & 0x0f;
    public int Line => _raw & 0xff;

    public override string ToString() =>
        string.Format(CultureInfo.InvariantCulture, "{0}.{1}.{2}", Area, Main, Line);

    public bool Equals(IndividualAddress other) => _raw == other._raw;
    public override bool Equals(object? obj) => obj is IndividualAddress other && Equals(other);
    public override int GetHashCode() => _raw;
    public static bool operator ==(IndividualAddress a, IndividualAddress b) => a.Equals(b);
    public static bool operator !=(IndividualAddress a, IndividualAddress b) => !a.Equals(b);
}

/// <summary>
/// Gruppenadresse. Auf der Leitung immer 16 Bit — die Schreibweise
/// (drei-, zweistufig oder frei) ist reine Darstellung und aendert am
/// uebertragenen Wert nichts.
/// </summary>
public readonly struct GroupAddress : IEquatable<GroupAddress>
{
    private readonly ushort _raw;

    private GroupAddress(ushort raw) => _raw = raw;

    public static GroupAddress Parse3Level(string s)
    {
        var parts = (s ?? "").Split('/');
        if (parts.Length != 3) throw KnxException.InvalidAddress(s ?? "");
        if (!IndividualAddress.TryPart(parts[0], 31, out var main) ||
            !IndividualAddress.TryPart(parts[1], 7, out var middle) ||
            !IndividualAddress.TryPart(parts[2], 255, out var sub))
        {
            throw KnxException.InvalidAddress(s ?? "");
        }
        return new GroupAddress((ushort)((main << 11) | (middle << 8) | sub));
    }

    public static GroupAddress Parse2Level(string s)
    {
        var parts = (s ?? "").Split('/');
        if (parts.Length != 2) throw KnxException.InvalidAddress(s ?? "");
        if (!IndividualAddress.TryPart(parts[0], 31, out var main) ||
            !IndividualAddress.TryPart(parts[1], 2047, out var sub))
        {
            throw KnxException.InvalidAddress(s ?? "");
        }
        return new GroupAddress((ushort)((main << 11) | sub));
    }

    public static GroupAddress ParseFree(string s)
    {
        if (!ushort.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out var raw))
        {
            throw KnxException.InvalidAddress(s ?? "");
        }
        return new GroupAddress(raw);
    }

    public static GroupAddress FromBytes(byte hi, byte lo) => new((ushort)((hi << 8) | lo));

    public byte[] ToBytes() => new[] { (byte)(_raw >> 8), (byte)(_raw & 0xff) };

    public override string ToString() => string.Format(
        CultureInfo.InvariantCulture, "{0}/{1}/{2}", (_raw >> 11) & 0x1f, (_raw >> 8) & 0x07, _raw & 0xff);

    public bool Equals(GroupAddress other) => _raw == other._raw;
    public override bool Equals(object? obj) => obj is GroupAddress other && Equals(other);
    public override int GetHashCode() => _raw;
    public static bool operator ==(GroupAddress a, GroupAddress b) => a.Equals(b);
    public static bool operator !=(GroupAddress a, GroupAddress b) => !a.Equals(b);
}
