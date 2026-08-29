using System;

namespace REGOwintergarden.Knx;

/// <summary>
/// Die zwoelf KNXnet/IP-Dienste, die dieser Helfer kennt. Der Zahlenwert ist
/// der auf der Leitung.
/// </summary>
public enum ServiceType
{
    SearchRequest = 0x0201,
    SearchResponse = 0x0202,
    DescriptionRequest = 0x0203,
    DescriptionResponse = 0x0204,

    /// <summary>
    /// Erweiterte Suche aus KNXnet/IP Core v2. Sie bringt zusaetzliche
    /// Beschreibungsbloecke mit - unter anderem die Liste der Tunnelplaetze
    /// samt Belegung, die in der gewoehnlichen Suchantwort fehlt. Aeltere
    /// Gateways kennen sie nicht und schweigen darauf.
    /// </summary>
    SearchRequestExtended = 0x020B,
    SearchResponseExtended = 0x020C,
    ConnectRequest = 0x0205,
    ConnectResponse = 0x0206,
    ConnectionstateRequest = 0x0207,
    ConnectionstateResponse = 0x0208,
    DisconnectRequest = 0x0209,
    DisconnectResponse = 0x020A,
    TunnellingRequest = 0x0420,
    TunnellingAck = 0x0421,
}

/// <summary>
/// Der sechs Byte lange Kopf, mit dem jeder KNXnet/IP-Rahmen anfaengt:
/// Laenge des Kopfes, Protokollversion, Diensttyp, Gesamtlaenge.
/// </summary>
public readonly struct KnxHeader
{
    public KnxHeader(ServiceType serviceType, int totalLength)
    {
        ServiceType = serviceType;
        TotalLength = totalLength;
    }

    public const int Length = 6;

    public ServiceType ServiceType { get; }
    public int TotalLength { get; }

    public static ServiceType ServiceTypeFrom(int value) => value switch
    {
        0x0201 => ServiceType.SearchRequest,
        0x0202 => ServiceType.SearchResponse,
        0x0203 => ServiceType.DescriptionRequest,
        0x0204 => ServiceType.DescriptionResponse,
        0x020B => ServiceType.SearchRequestExtended,
        0x020C => ServiceType.SearchResponseExtended,
        0x0205 => ServiceType.ConnectRequest,
        0x0206 => ServiceType.ConnectResponse,
        0x0207 => ServiceType.ConnectionstateRequest,
        0x0208 => ServiceType.ConnectionstateResponse,
        0x0209 => ServiceType.DisconnectRequest,
        0x020A => ServiceType.DisconnectResponse,
        0x0420 => ServiceType.TunnellingRequest,
        0x0421 => ServiceType.TunnellingAck,
        _ => throw KnxException.UnknownServiceType(value),
    };

    public byte[] Encode()
    {
        var st = (int)ServiceType;
        return new[]
        {
            (byte)0x06,
            (byte)0x10,
            (byte)(st >> 8), (byte)(st & 0xff),
            (byte)(TotalLength >> 8), (byte)(TotalLength & 0xff),
        };
    }

    /// <summary>
    /// Liest den Kopf und liefert nebenbei, wo der Koerper anfaengt.
    /// </summary>
    public static KnxHeader Decode(ReadOnlySpan<byte> bytes, out int offset)
    {
        offset = 0;
        // Zu kurz schlaegt immer vor falschen Kennbytes: solange nicht alle
        // sechs da sind, weiss niemand, ob der Rahmen kaputt oder bloss noch
        // nicht ganz gelesen ist.
        if (bytes.Length < Length) throw KnxException.Truncated();
        if (bytes[0] != 0x06) throw KnxException.InvalidLength(Length, bytes[0]);
        if (bytes[1] != 0x10) throw KnxException.UnsupportedProtocolVersion(bytes[1]);
        var serviceType = ServiceTypeFrom((bytes[2] << 8) | bytes[3]);
        var totalLength = (bytes[4] << 8) | bytes[5];
        offset = Length;
        return new KnxHeader(serviceType, totalLength);
    }
}
