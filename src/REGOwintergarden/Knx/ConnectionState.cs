using System;
using System.Collections.Generic;

namespace REGOwintergarden.Knx;

/// <summary>
/// Der Herzschlag: „lebt der Kanal noch?"
/// </summary>
public sealed class ConnectionstateRequest
{
    public ConnectionstateRequest(byte channelId, Hpai controlHpai)
    {
        ChannelId = channelId;
        ControlHpai = controlHpai;
    }

    public byte ChannelId { get; }
    public Hpai ControlHpai { get; }

    public byte[] Encode() =>
        ChannelFrames.EncodeChannelPlusHpai(ServiceType.ConnectionstateRequest, ChannelId, ControlHpai);
}

public sealed class ConnectionstateResponse
{
    public ConnectionstateResponse(byte channelId, ConnectionErrorCode? error)
    {
        ChannelId = channelId;
        Error = error;
    }

    public byte ChannelId { get; }
    public ConnectionErrorCode? Error { get; }

    public static ConnectionstateResponse Decode(ReadOnlySpan<byte> bytes)
    {
        var (channelId, error) = ChannelFrames.DecodeChannelPlusStatus(
            bytes, ServiceType.ConnectionstateResponse);
        return new ConnectionstateResponse(channelId, error);
    }
}

public sealed class DisconnectRequest
{
    public DisconnectRequest(byte channelId, Hpai controlHpai)
    {
        ChannelId = channelId;
        ControlHpai = controlHpai;
    }

    public byte ChannelId { get; }
    public Hpai ControlHpai { get; }

    public byte[] Encode() =>
        ChannelFrames.EncodeChannelPlusHpai(ServiceType.DisconnectRequest, ChannelId, ControlHpai);

    /// <summary>
    /// Ein Gateway darf den Kanal von sich aus abbauen — deshalb muss dieser
    /// Rahmen nicht nur gebaut, sondern auch gelesen werden koennen. Der
    /// Client antwortet darauf mit einer <see cref="DisconnectResponse"/> und
    /// betrachtet den Kanal danach als weg.
    /// </summary>
    public static DisconnectRequest Decode(ReadOnlySpan<byte> bytes)
    {
        var header = KnxHeader.Decode(bytes, out var offset);
        // CONNECTIONSTATE_REQUEST hat einen byteweise identischen Koerper —
        // nur der Diensttyp unterscheidet die beiden.
        if (header.ServiceType != ServiceType.DisconnectRequest)
        {
            throw KnxException.UnexpectedServiceType(ServiceType.DisconnectRequest, header.ServiceType);
        }
        if (bytes.Length < offset + 2) throw KnxException.Truncated();
        var channelId = bytes[offset];
        // bytes[offset + 1] ist reserviert.
        return new DisconnectRequest(channelId, Hpai.Decode(bytes.Slice(offset + 2)));
    }
}

public sealed class DisconnectResponse
{
    public DisconnectResponse(byte channelId, ConnectionErrorCode? error)
    {
        ChannelId = channelId;
        Error = error;
    }

    public byte ChannelId { get; }
    public ConnectionErrorCode? Error { get; }

    public byte[] Encode()
    {
        // Blosser Zwei-Byte-Koerper, kein HPAI — deshalb geht das nicht ueber
        // EncodeChannelPlusHpai.
        var outBytes = new List<byte>(8);
        outBytes.AddRange(new KnxHeader(ServiceType.DisconnectResponse, 8).Encode());
        outBytes.Add(ChannelId);
        outBytes.Add(Error is null ? (byte)0x00 : ConnectionError.ToByte(Error.Value));
        return outBytes.ToArray();
    }

    public static DisconnectResponse Decode(ReadOnlySpan<byte> bytes)
    {
        var (channelId, error) = ChannelFrames.DecodeChannelPlusStatus(
            bytes, ServiceType.DisconnectResponse);
        return new DisconnectResponse(channelId, error);
    }
}

internal static class ChannelFrames
{
    public static byte[] EncodeChannelPlusHpai(ServiceType serviceType, byte channelId, Hpai hpai)
    {
        var hpaiBytes = hpai.Encode();
        var totalLength = KnxHeader.Length + 2 + hpaiBytes.Length;
        var outBytes = new List<byte>(totalLength);
        outBytes.AddRange(new KnxHeader(serviceType, totalLength).Encode());
        outBytes.Add(channelId);
        outBytes.Add(0x00); // reserviert
        outBytes.AddRange(hpaiBytes);
        return outBytes.ToArray();
    }

    public static (byte ChannelId, ConnectionErrorCode? Error) DecodeChannelPlusStatus(
        ReadOnlySpan<byte> bytes, ServiceType expected)
    {
        var header = KnxHeader.Decode(bytes, out var offset);
        // CONNECT_RESPONSE, CONNECTIONSTATE_RESPONSE und DISCONNECT_RESPONSE
        // tragen alle drei diesen identischen Zwei-Byte-Koerper und treffen auf
        // demselben Socket ein — nur der Diensttyp im Kopf trennt sie.
        if (header.ServiceType != expected)
        {
            throw KnxException.UnexpectedServiceType(expected, header.ServiceType);
        }
        if (bytes.Length < offset + 2) throw KnxException.Truncated();
        var status = bytes[offset + 1];
        return (bytes[offset], status == 0 ? null : ConnectionError.FromByte(status));
    }
}
