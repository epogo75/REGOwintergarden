using System;
using System.Collections.Generic;

namespace REGOwintergarden.Knx;

/// <summary>
/// TUNNELLING_REQUEST — der Rahmen, der ein cEMI-Telegramm durch den Tunnel
/// traegt.
/// </summary>
public sealed class TunnellingRequest
{
    /// <summary>
    /// Verbindungskopf eines Tunnelrahmens: Laengenbyte, Kanal, Folgenummer,
    /// reserviert. Laut Norm fest vier Byte, fuer Anfrage wie Quittung.
    /// </summary>
    public const int ConnectionHeaderLength = 4;

    public TunnellingRequest(byte channelId, byte sequence, byte[] cemi)
    {
        ChannelId = channelId;
        Sequence = sequence;
        Cemi = cemi;
    }

    public byte ChannelId { get; }
    public byte Sequence { get; }
    public byte[] Cemi { get; }

    public byte[] Encode()
    {
        var totalLength = KnxHeader.Length + ConnectionHeaderLength + Cemi.Length;
        var outBytes = new List<byte>(totalLength);
        outBytes.AddRange(new KnxHeader(ServiceType.TunnellingRequest, totalLength).Encode());
        outBytes.Add(ConnectionHeaderLength);
        outBytes.Add(ChannelId);
        outBytes.Add(Sequence);
        outBytes.Add(0x00); // reserviert
        outBytes.AddRange(Cemi);
        return outBytes.ToArray();
    }

    public static TunnellingRequest Decode(ReadOnlySpan<byte> bytes)
    {
        var header = KnxHeader.Decode(bytes, out var offset);
        if (header.ServiceType != ServiceType.TunnellingRequest)
        {
            throw KnxException.UnexpectedServiceType(ServiceType.TunnellingRequest, header.ServiceType);
        }

        // Das cEMI-Telegramm traegt keine eigene Laenge: es reicht vom Ende
        // des Verbindungskopfes bis zur angegebenen Gesamtlaenge. Beide Enden
        // kommen von der Leitung, also erst die angegebene Laenge gegen den
        // Puffer pruefen und dann schneiden — und die angegebene Laenge
        // nehmen, nicht die des Puffers, damit angehaengte Fuellbytes eines
        // zu grossen Lesevorgangs nie ins Telegramm rutschen.
        var declaredTotal = header.TotalLength;
        if (declaredTotal < offset + ConnectionHeaderLength || declaredTotal > bytes.Length)
        {
            throw KnxException.Truncated();
        }

        var connectionHeader = bytes.Slice(offset, ConnectionHeaderLength);
        if (connectionHeader[0] != ConnectionHeaderLength)
        {
            throw KnxException.InvalidLength(ConnectionHeaderLength, connectionHeader[0]);
        }

        var cemiStart = offset + ConnectionHeaderLength;
        return new TunnellingRequest(
            connectionHeader[1],
            connectionHeader[2],
            bytes.Slice(cemiStart, declaredTotal - cemiStart).ToArray());
    }
}

public sealed class TunnellingAck
{
    public TunnellingAck(byte channelId, byte sequence, byte status)
    {
        ChannelId = channelId;
        Sequence = sequence;
        Status = status;
    }

    public byte ChannelId { get; }
    public byte Sequence { get; }
    public byte Status { get; }

    public byte[] Encode()
    {
        var outBytes = new List<byte>(10);
        outBytes.AddRange(new KnxHeader(ServiceType.TunnellingAck, 10).Encode());
        outBytes.Add(TunnellingRequest.ConnectionHeaderLength);
        outBytes.Add(ChannelId);
        outBytes.Add(Sequence);
        outBytes.Add(Status);
        return outBytes.ToArray();
    }

    public static TunnellingAck Decode(ReadOnlySpan<byte> bytes)
    {
        var header = KnxHeader.Decode(bytes, out var offset);
        if (header.ServiceType != ServiceType.TunnellingAck)
        {
            throw KnxException.UnexpectedServiceType(ServiceType.TunnellingAck, header.ServiceType);
        }
        if (bytes.Length < offset + TunnellingRequest.ConnectionHeaderLength)
        {
            throw KnxException.Truncated();
        }
        var connectionHeader = bytes.Slice(offset, TunnellingRequest.ConnectionHeaderLength);
        if (connectionHeader[0] != TunnellingRequest.ConnectionHeaderLength)
        {
            throw KnxException.InvalidLength(
                TunnellingRequest.ConnectionHeaderLength, connectionHeader[0]);
        }
        return new TunnellingAck(connectionHeader[1], connectionHeader[2], connectionHeader[3]);
    }
}

public enum ReceiveOutcome
{
    /// <summary>Die erwartete Nummer — quittieren und auswerten.</summary>
    Accept,

    /// <summary>Die vorige Nummer noch einmal — quittieren, sonst nichts.</summary>
    DuplicateAck,

    /// <summary>Ausserhalb des Fensters — der Tunnel ist aus dem Takt.</summary>
    RejectAndReconnect,
}

/// <summary>
/// Die beiden Folgezaehler eines Tunnels, einer je Richtung. Beide fangen bei
/// jeder Sitzung wieder bei null an — eine neue Verbindung uebernimmt nichts
/// von der, die sie ersetzt.
/// </summary>
public sealed class SequenceState
{
    private byte _send;
    private byte _expectedReceive;

    /// <summary>
    /// Liefert die Nummer fuer einen neuen ausgehenden Rahmen und zaehlt
    /// weiter. Auch nach einem misslungenen Sendeversuch bleibt es bei einer
    /// Nummer je Rahmen: einmal aufrufen und den Wert fuer jede Wiederholung
    /// desselben Rahmens weiterverwenden.
    /// </summary>
    public byte NextSend()
    {
        var value = _send;
        _send = unchecked((byte)(_send + 1));
        return value;
    }

    public ReceiveOutcome OnReceived(byte received)
    {
        if (received == _expectedReceive)
        {
            _expectedReceive = unchecked((byte)(_expectedReceive + 1));
            return ReceiveOutcome.Accept;
        }
        if (received == unchecked((byte)(_expectedReceive - 1)))
        {
            return ReceiveOutcome.DuplicateAck;
        }
        return ReceiveOutcome.RejectAndReconnect;
    }
}
