using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace REGOwintergarden.Knx;

/// <summary>
/// Der Geraeteblock einer SEARCH_RESPONSE: was das Gateway ueber sich selbst
/// erzaehlt.
/// </summary>
public sealed class DeviceInfo
{
    public const byte DibType = 0x01;
    private const int DeviceInfoLength = 54;

    public DeviceInfo(
        byte medium,
        bool programmingMode,
        IndividualAddress individualAddress,
        byte[] serial,
        IPAddress routingMulticast,
        byte[] mac,
        string friendlyName)
    {
        Medium = medium;
        ProgrammingMode = programmingMode;
        IndividualAddress = individualAddress;
        Serial = serial;
        RoutingMulticast = routingMulticast;
        Mac = mac;
        FriendlyName = friendlyName;
    }

    public byte Medium { get; }
    public bool ProgrammingMode { get; }
    public IndividualAddress IndividualAddress { get; }
    public byte[] Serial { get; }
    public IPAddress RoutingMulticast { get; }
    public byte[] Mac { get; }
    public string FriendlyName { get; }

    public static DeviceInfo Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < DeviceInfoLength) throw KnxException.Truncated();

        var nameBytes = bytes.Slice(24, DeviceInfoLength - 24);
        var nameEnd = nameBytes.IndexOf((byte)0);
        if (nameEnd < 0) nameEnd = nameBytes.Length;

        // Der Anzeigename ist ISO-8859-1 und mit Nullbytes aufgefuellt — Byte
        // fuer Byte umsetzen, nicht als UTF-8 lesen. Ein Gateway, das
        // „Verteilung Süd" heisst, wuerde sonst als kaputt gelten.
        var friendlyName = Encoding.Latin1.GetString(nameBytes.Slice(0, nameEnd));

        return new DeviceInfo(
            bytes[2],
            (bytes[3] & 0x01) != 0,
            IndividualAddress.FromBytes(bytes[4], bytes[5]),
            bytes.Slice(8, 6).ToArray(),
            new IPAddress(new[] { bytes[14], bytes[15], bytes[16], bytes[17] }),
            bytes.Slice(18, 6).ToArray(),
            friendlyName);
    }
}

/// <summary>
/// Welche Dienstfamilien das Gateway beherrscht. Interessant ist genau eine:
/// ob Tunneling dabei ist.
/// </summary>
public sealed class SupportedServiceFamilies
{
    public const byte DibType = 0x02;
    private const byte FamilyTunneling = 0x04;

    public SupportedServiceFamilies(IReadOnlyList<(byte Id, byte Version)> families)
    {
        Families = families;
    }

    public IReadOnlyList<(byte Id, byte Version)> Families { get; }

    public bool SupportsTunneling
    {
        get
        {
            foreach (var family in Families)
            {
                if (family.Id == FamilyTunneling) return true;
            }
            return false;
        }
    }

    public static SupportedServiceFamilies Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 2) throw KnxException.Truncated();
        // Das Laengenbyte kommt von der Leitung und bestimmt die Schnittgrenze
        // darunter: unter 2 laege der Anfang hinter dem Ende, ueber dem Puffer
        // liefe es hinaus. Beides vorher abfangen.
        var declared = bytes[0];
        if (declared < 2 || declared > bytes.Length) throw KnxException.Truncated();

        var families = new List<(byte, byte)>();
        for (var i = 2; i + 1 < declared; i += 2)
        {
            families.Add((bytes[i], bytes[i + 1]));
        }
        return new SupportedServiceFamilies(families);
    }
}

public sealed class SearchRequest
{
    public SearchRequest(Hpai discoveryEndpoint, bool extended = false)
    {
        DiscoveryEndpoint = discoveryEndpoint;
        Extended = extended;
    }

    public Hpai DiscoveryEndpoint { get; }

    /// <summary>
    /// Erweiterte Suche nach KNXnet/IP Core v2. Ohne zusaetzliche
    /// Suchparameter ist der Rahmen derselbe wie bei der gewoehnlichen Suche -
    /// nur der Diensttyp unterscheidet sich. Wer sie schickt, bekommt von
    /// neueren Gateways die Liste der Tunnelplaetze mit; aeltere schweigen
    /// darauf, deshalb wird sie nie allein verschickt.
    /// </summary>
    public bool Extended { get; }

    public byte[] Encode()
    {
        var hpai = DiscoveryEndpoint.Encode();
        var outBytes = new List<byte>(KnxHeader.Length + hpai.Length);
        outBytes.AddRange(new KnxHeader(
            Extended ? ServiceType.SearchRequestExtended : ServiceType.SearchRequest,
            KnxHeader.Length + hpai.Length).Encode());
        outBytes.AddRange(hpai);
        return outBytes.ToArray();
    }
}

public sealed class SearchResponse
{
    public SearchResponse(
        Hpai controlEndpoint,
        DeviceInfo? deviceInfo,
        SupportedServiceFamilies? serviceFamilies,
        TunnelingInfo? tunneling)
    {
        ControlEndpoint = controlEndpoint;
        DeviceInfo = deviceInfo;
        ServiceFamilies = serviceFamilies;
        Tunneling = tunneling;
    }

    public Hpai ControlEndpoint { get; }
    public DeviceInfo? DeviceInfo { get; }
    public SupportedServiceFamilies? ServiceFamilies { get; }

    /// <summary>Nur bei einer erweiterten Suchantwort belegt.</summary>
    public TunnelingInfo? Tunneling { get; }

    public static SearchResponse Decode(ReadOnlySpan<byte> bytes)
    {
        var header = KnxHeader.Decode(bytes, out var offset);
        // Beide Antwortarten haben denselben Aufbau; die erweiterte bringt
        // nur zusaetzliche Bloecke mit, und unbekannte Bloecke ueberspringt
        // der Durchlauf unten ohnehin.
        if (header.ServiceType != ServiceType.SearchResponse
            && header.ServiceType != ServiceType.SearchResponseExtended)
        {
            throw KnxException.UnexpectedServiceType(ServiceType.SearchResponse, header.ServiceType);
        }

        var controlEndpoint = Hpai.Decode(bytes.Slice(offset));
        var cursor = offset + Hpai.Length;

        DeviceInfo? deviceInfo = null;
        SupportedServiceFamilies? serviceFamilies = null;
        TunnelingInfo? tunneling = null;

        while (cursor < bytes.Length)
        {
            // Auch hier kommt das Laengenbyte von der Leitung: unter 2 wuerde
            // der Durchlauf stehenbleiben, ueber dem Rest liefe er hinaus.
            var dibLength = bytes[cursor];
            if (dibLength < 2 || dibLength > bytes.Length - cursor) break;

            var dibType = bytes[cursor + 1];
            var dibBody = bytes.Slice(cursor, dibLength);
            // Nur bei Erfolg zuweisen: ein spaeterer, fehlerhafter Block
            // desselben Typs darf einen bereits gelesenen, gueltigen nicht
            // wieder loeschen.
            if (dibType == DeviceInfo.DibType)
            {
                try { deviceInfo = DeviceInfo.Decode(dibBody); }
                catch (KnxException) { /* unbrauchbarer Block — den gueltigen behalten */ }
            }
            else if (dibType == SupportedServiceFamilies.DibType)
            {
                try { serviceFamilies = SupportedServiceFamilies.Decode(dibBody); }
                catch (KnxException) { /* dito */ }
            }
            else if (dibType == TunnelingInfo.DibType)
            {
                try { tunneling = TunnelingInfo.Decode(dibBody); }
                catch (KnxException) { /* dito */ }
            }
            // Unbekannte Blocktypen ueberspringt das Laengenbyte.
            cursor += dibLength;
        }

        return new SearchResponse(controlEndpoint, deviceInfo, serviceFamilies, tunneling);
    }
}
