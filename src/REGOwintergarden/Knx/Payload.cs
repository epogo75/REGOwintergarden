using System;
using System.Linq;

namespace REGOwintergarden.Knx;

/// <summary>
/// Der Nutzwert eines Gruppentelegramms, so wie er auf der Leitung liegt.
///
/// <see cref="IsSmall"/> heisst: die sechs Bit stecken im APCI-Byte selbst
/// (DPT 1.x, 3.007). Sonst folgen die Oktette dahinter (DPT 5.001, 6.010,
/// 9.x, 20.102). Das ist kein Darstellungsdetail — die beiden Formen ergeben
/// unterschiedliche Bytes auf dem Bus, und ein Geraet, das die kurze Form
/// erwartet, versteht die lange nicht.
/// </summary>
public sealed class Payload : IEquatable<Payload>
{
    private Payload(bool isSmall, byte small, byte[] bytes)
    {
        IsSmall = isSmall;
        Small = small;
        Bytes = bytes;
    }

    public bool IsSmall { get; }

    /// <summary>Nur gueltig, wenn <see cref="IsSmall"/>.</summary>
    public byte Small { get; }

    /// <summary>Leer, wenn <see cref="IsSmall"/>.</summary>
    public byte[] Bytes { get; }

    public static Payload FromSmall(byte value) => new(true, value, Array.Empty<byte>());

    public static Payload FromBytes(params byte[] bytes) =>
        new(false, 0, bytes ?? Array.Empty<byte>());

    public bool Equals(Payload? other)
    {
        if (other is null) return false;
        if (IsSmall != other.IsSmall) return false;
        return IsSmall ? Small == other.Small : Bytes.SequenceEqual(other.Bytes);
    }

    public override bool Equals(object? obj) => Equals(obj as Payload);

    public override int GetHashCode()
    {
        if (IsSmall) return Small;
        var hash = 17;
        foreach (var b in Bytes) hash = hash * 31 + b;
        return hash;
    }

    public override string ToString() =>
        IsSmall ? $"Small({Small})" : "Bytes(" + string.Join(" ", Bytes.Select(b => b.ToString("x2"))) + ")";
}
