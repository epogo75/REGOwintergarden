using System;
using System.Net;

namespace REGOwintergarden.Knx;

public enum HostProtocol
{
    Udp,
    Tcp,
}

/// <summary>
/// Host Protocol Address Information — acht Byte: Laenge, Protokollcode,
/// IPv4-Adresse, Port. Die Angabe, wohin das Gateway antworten soll.
/// </summary>
public readonly struct Hpai : IEquatable<Hpai>
{
    public Hpai(HostProtocol protocol, IPAddress address, int port)
    {
        Protocol = protocol;
        Address = address;
        Port = port;
    }

    public const int Length = 8;

    public HostProtocol Protocol { get; }
    public IPAddress Address { get; }
    public int Port { get; }

    /// <summary>
    /// 0.0.0.0:0 — die Bitte an das Gateway, an die Adresse zurueckzuschicken,
    /// von der die Anfrage kam. Noetig hinter NAT und bei mehreren Netzkarten.
    /// </summary>
    public static Hpai RouteBack() => new(HostProtocol.Udp, IPAddress.Any, 0);

    public byte[] Encode()
    {
        var octets = Address.GetAddressBytes();
        return new[]
        {
            (byte)Length,
            (byte)(Protocol == HostProtocol.Udp ? 0x01 : 0x02),
            octets[0], octets[1], octets[2], octets[3],
            (byte)(Port >> 8), (byte)(Port & 0xff),
        };
    }

    public static Hpai Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < Length) throw KnxException.Truncated();
        if (bytes[0] != Length) throw KnxException.InvalidLength(Length, bytes[0]);
        var protocol = bytes[1] switch
        {
            0x01 => HostProtocol.Udp,
            0x02 => HostProtocol.Tcp,
            _ => throw KnxException.UnsupportedHostProtocol(bytes[1]),
        };
        var address = new IPAddress(new[] { bytes[2], bytes[3], bytes[4], bytes[5] });
        var port = (bytes[6] << 8) | bytes[7];
        return new Hpai(protocol, address, port);
    }

    public bool Equals(Hpai other) =>
        Protocol == other.Protocol && Port == other.Port && Address.Equals(other.Address);

    public override bool Equals(object? obj) => obj is Hpai other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Protocol, Address, Port);
    public override string ToString() => $"{Address}:{Port}";
}
