using System;
using System.Collections.Generic;

namespace REGOwintergarden.Knx;

/// <summary>
/// Ein Tunnelplatz eines Gateways: welche physikalische Adresse er vergibt und
/// ob er gerade zu haben ist.
/// </summary>
public sealed class TunnelSlot
{
    public TunnelSlot(IndividualAddress address, ushort status)
    {
        Address = address;
        Status = status;
    }

    public IndividualAddress Address { get; }

    /// <summary>Das rohe Statuswort — siehe die Bemerkung bei den drei Merkmalen.</summary>
    public ushort Status { get; }

    // Die Bedeutung der Bits stammt aus KNXnet/IP Core v2 (Bit 0 nutzbar,
    // Bit 1 berechtigt, Bit 2 frei). Das rohe Wort bleibt daneben stehen:
    // wenn ein Gateway es anders auslegt, sieht man es an der Zahl, statt
    // einer falschen Ausgabe zu glauben.
    public bool Usable => (Status & 0x01) != 0;
    public bool Authorised => (Status & 0x02) != 0;
    public bool Free => (Status & 0x04) != 0;

    /// <summary>Ein Platz, den man jetzt bekommen kann.</summary>
    public bool Available => Usable && Free;

    public override string ToString() =>
        $"{Address} {(Available ? "frei" : Free ? "nicht nutzbar" : "belegt")}";
}

/// <summary>
/// Der Beschreibungsblock 0x07 aus der erweiterten Suchantwort. Er zaehlt die
/// Tunnelplaetze auf — genau die Angabe, die in der gewoehnlichen Suchantwort
/// fehlt und die man braucht, bevor man sich an ein Gateway haengt, das
/// vielleicht schon voll ist.
/// </summary>
public sealed class TunnelingInfo
{
    public const byte DibType = 0x07;

    public TunnelingInfo(int maxApduLength, IReadOnlyList<TunnelSlot> slots)
    {
        MaxApduLength = maxApduLength;
        Slots = slots;
    }

    public int MaxApduLength { get; }
    public IReadOnlyList<TunnelSlot> Slots { get; }

    public int FreeCount
    {
        get
        {
            var n = 0;
            foreach (var s in Slots) { if (s.Available) n++; }
            return n;
        }
    }

    public static TunnelingInfo Decode(ReadOnlySpan<byte> bytes)
    {
        // Laenge, Typ, dann zwei Byte groesste APDU-Laenge - darunter passt
        // kein Platz mehr hinein.
        if (bytes.Length < 4) throw KnxException.Truncated();
        var declared = bytes[0];
        if (declared < 4 || declared > bytes.Length) throw KnxException.Truncated();

        var maxApdu = (bytes[2] << 8) | bytes[3];
        var slots = new List<TunnelSlot>();
        // Je Platz vier Byte: zwei fuer die Adresse, zwei fuer den Status.
        for (var i = 4; i + 3 < declared; i += 4)
        {
            slots.Add(new TunnelSlot(
                IndividualAddress.FromBytes(bytes[i], bytes[i + 1]),
                (ushort)((bytes[i + 2] << 8) | bytes[i + 3])));
        }
        return new TunnelingInfo(maxApdu, slots);
    }
}
