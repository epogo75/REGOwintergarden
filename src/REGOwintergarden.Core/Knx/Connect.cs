using System;
using System.Collections.Generic;

namespace REGOwintergarden.Knx;

/// <summary>
/// Die Statuscodes, mit denen ein Gateway eine Verbindung ablehnt oder einen
/// Kanal fuer ungueltig erklaert. Dieselbe Tabelle gilt fuer
/// CONNECT_RESPONSE, CONNECTIONSTATE_RESPONSE und DISCONNECT_RESPONSE.
/// </summary>
public enum ConnectionErrorCode
{
    HostProtocolType = 0x01,
    VersionNotSupported = 0x02,
    SequenceNumber = 0x04,
    GenericError = 0x0f,
    ConnectionId = 0x21,
    ConnectionType = 0x22,
    ConnectionOption = 0x23,
    NoMoreConnections = 0x24,
    NoMoreUniqueConnections = 0x25,
    DataConnection = 0x26,
    KnxConnection = 0x27,
    AuthorisationError = 0x28,
    TunnellingLayer = 0x29,
    NoTunnellingAddress = 0x2d,
    ConnectionInUse = 0x2e,
}

public static class ConnectionError
{
    /// <summary>
    /// Jeder Code, auch ein unbekannter, kommt als <see cref="ConnectionErrorCode"/>
    /// zurueck — der Zahlenwert bleibt dabei erhalten, damit
    /// <see cref="Describe"/> ihn noch nennen kann.
    /// </summary>
    public static ConnectionErrorCode FromByte(byte value) => (ConnectionErrorCode)value;

    public static byte ToByte(ConnectionErrorCode code) => (byte)code;

    public static string Describe(ConnectionErrorCode code) => code switch
    {
        ConnectionErrorCode.HostProtocolType => "Nicht unterstuetztes Host-Protokoll",
        ConnectionErrorCode.VersionNotSupported => "KNXnet/IP-Version nicht unterstuetzt",
        ConnectionErrorCode.SequenceNumber => "Ungueltige Folgenummer",
        ConnectionErrorCode.GenericError => "Allgemeiner Fehler am Gateway",
        ConnectionErrorCode.ConnectionId => "Unbekannte Verbindungs-ID (Kanal verloren)",
        ConnectionErrorCode.ConnectionType => "Verbindungstyp nicht unterstuetzt",
        ConnectionErrorCode.ConnectionOption => "Verbindungsoption nicht unterstuetzt",
        ConnectionErrorCode.NoMoreConnections => "Keine freien Tunnel-Plaetze am Gateway",
        ConnectionErrorCode.NoMoreUniqueConnections => "Keine weiteren eindeutigen Verbindungen moeglich",
        ConnectionErrorCode.DataConnection => "Datenverbindung fehlgeschlagen",
        ConnectionErrorCode.KnxConnection => "KNX-Busverbindung fehlgeschlagen",
        ConnectionErrorCode.AuthorisationError => "Autorisierung fehlgeschlagen",
        ConnectionErrorCode.TunnellingLayer => "Tunneling-Ebene nicht unterstuetzt",
        ConnectionErrorCode.NoTunnellingAddress => "Keine physikalische Tunnel-Adresse verfuegbar",
        ConnectionErrorCode.ConnectionInUse => "Verbindung bereits in Verwendung",
        _ => $"Unbekannter Fehler (0x{(byte)code:x2})",
    };
}

/// <summary>
/// CONNECT_REQUEST fuer einen Tunnel. Jede andere Verbindungsart
/// (Geraeteverwaltung, Fernprotokoll) hat eine zwei Byte lange CRI und liegt
/// ausserhalb dessen, was dieser Helfer braucht.
/// </summary>
public sealed class ConnectRequest
{
    public const int TunnelCriLength = 4;
    private const byte ConnectionTypeTunnel = 0x04;
    private const byte KnxLayerDataLink = 0x02;

    public ConnectRequest(Hpai controlHpai, Hpai dataHpai)
    {
        ControlHpai = controlHpai;
        DataHpai = dataHpai;
    }

    public Hpai ControlHpai { get; }
    public Hpai DataHpai { get; }

    public byte[] Encode()
    {
        var control = ControlHpai.Encode();
        var data = DataHpai.Encode();
        var cri = new byte[] { TunnelCriLength, ConnectionTypeTunnel, KnxLayerDataLink, 0x00 };
        var totalLength = KnxHeader.Length + control.Length + data.Length + cri.Length;

        var outBytes = new List<byte>(totalLength);
        outBytes.AddRange(new KnxHeader(ServiceType.ConnectRequest, totalLength).Encode());
        outBytes.AddRange(control);
        outBytes.AddRange(data);
        outBytes.AddRange(cri);
        return outBytes.ToArray();
    }
}

public sealed class ConnectResponse
{
    public ConnectResponse(
        byte channelId,
        ConnectionErrorCode? error,
        Hpai? dataEndpoint,
        IndividualAddress? assignedAddress)
    {
        ChannelId = channelId;
        Error = error;
        DataEndpoint = dataEndpoint;
        AssignedAddress = assignedAddress;
    }

    public byte ChannelId { get; }
    public ConnectionErrorCode? Error { get; }
    public Hpai? DataEndpoint { get; }
    public IndividualAddress? AssignedAddress { get; }

    public static ConnectResponse Decode(ReadOnlySpan<byte> bytes)
    {
        var header = KnxHeader.Decode(bytes, out var offset);
        // CONNECTIONSTATE_RESPONSE und DISCONNECT_RESPONSE haben genau diesen
        // zwei Byte langen Koerper und treffen auf demselben Socket ein —
        // ohne diese Pruefung wuerden sie als plausibles Verbindungsergebnis
        // durchgehen.
        if (header.ServiceType != ServiceType.ConnectResponse)
        {
            throw KnxException.UnexpectedServiceType(ServiceType.ConnectResponse, header.ServiceType);
        }
        if (bytes.Length < offset + 2) throw KnxException.Truncated();

        var channelId = bytes[offset];
        var status = bytes[offset + 1];
        if (status != 0)
        {
            // Ist der Status ungleich null, endet der Koerper hier: es folgen
            // weder HPAI noch CRD.
            return new ConnectResponse(channelId, ConnectionError.FromByte(status), null, null);
        }

        // Status null verspricht acht Byte HPAI und vier Byte CRD. Den ganzen
        // langen Koerper vorab absichern: ein Rahmen mit der kurzen Form, aber
        // einem Nullstatus, wuerde sonst hier ueber das Ende hinauslesen.
        if (bytes.Length < offset + 2 + Hpai.Length + ConnectRequest.TunnelCriLength)
        {
            throw KnxException.Truncated();
        }

        var dataEndpoint = Hpai.Decode(bytes.Slice(offset + 2));
        var crd = bytes.Slice(offset + 10, ConnectRequest.TunnelCriLength);

        // Das Laengenbyte der CRD kommt von der Leitung. Es wird nur
        // verglichen, nie als Schnittgrenze benutzt — und eine CRD, die nicht
        // die vier Byte lange Tunnelform hat, traegt an crd[2..4] eben nicht
        // die zugeteilte Adresse. Sie trotzdem zu lesen hiesse, dem Aufrufer
        // eine still falsche Absenderadresse fuer jeden weiteren Rahmen zu
        // geben.
        if (crd[0] != ConnectRequest.TunnelCriLength)
        {
            throw KnxException.InvalidLength(ConnectRequest.TunnelCriLength, crd[0]);
        }

        return new ConnectResponse(
            channelId, null, dataEndpoint, IndividualAddress.FromBytes(crd[2], crd[3]));
    }
}
