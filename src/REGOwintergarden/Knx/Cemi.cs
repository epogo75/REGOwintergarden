using System;
using System.Collections.Generic;

namespace REGOwintergarden.Knx;

public enum MessageCode
{
    /// <summary>Auftrag an den Bus — was der Helfer sendet.</summary>
    LDataReq,

    /// <summary>Bestaetigung des Gateways, dass es den Auftrag abgesetzt hat.</summary>
    LDataCon,

    /// <summary>Ein Telegramm vom Bus.</summary>
    LDataInd,
}

public enum ApciService
{
    GroupValueRead,
    GroupValueResponse,
    GroupValueWrite,
}

/// <summary>
/// Ein cEMI-Telegramm: der Inhalt, den ein TUNNELLING_REQUEST traegt.
/// </summary>
public sealed class CemiFrame
{
    public CemiFrame(
        MessageCode messageCode,
        IndividualAddress source,
        GroupAddress destination,
        ApciService service,
        Payload payload)
    {
        MessageCode = messageCode;
        Source = source;
        Destination = destination;
        Service = service;
        Payload = payload;
    }

    public MessageCode MessageCode { get; }
    public IndividualAddress Source { get; }
    public GroupAddress Destination { get; }
    public ApciService Service { get; }
    public Payload Payload { get; }

    // Standardrahmen, keine Wiederholung, Gruppen-Broadcast, niedrige
    // Prioritaet, keine Quittung, kein Fehler. Jeder Schreibvorgang dieses
    // Helfers nutzt diese beiden Bytes unveraendert; nur beim Lesen vom Bus
    // muss man andere Kombinationen vertragen — was hier dadurch geschieht,
    // dass die beiden Bytes beim Entschluesseln gar nicht erst uebernommen
    // werden. Ein entschluesselter Rahmen wird nie wieder verschluesselt.
    private const byte Cf1Standard = 0xbc;
    private const byte Cf2GroupHop6 = 0xe0;

    private static byte MessageCodeByte(MessageCode code) => code switch
    {
        MessageCode.LDataReq => 0x11,
        MessageCode.LDataCon => 0x2e,
        MessageCode.LDataInd => 0x29,
        _ => throw new ArgumentOutOfRangeException(nameof(code)),
    };

    private static MessageCode MessageCodeFrom(byte value) => value switch
    {
        0x11 => MessageCode.LDataReq,
        0x2e => MessageCode.LDataCon,
        0x29 => MessageCode.LDataInd,
        _ => throw KnxException.UnknownMessageCode(value),
    };

    private static int ApciValue(ApciService service) => service switch
    {
        ApciService.GroupValueRead => 0x000,
        ApciService.GroupValueResponse => 0x040,
        ApciService.GroupValueWrite => 0x080,
        _ => throw new ArgumentOutOfRangeException(nameof(service)),
    };

    private static ApciService ApciFrom(int value) => (value & 0x3ff) switch
    {
        0x000 => ApciService.GroupValueRead,
        0x040 => ApciService.GroupValueResponse,
        0x080 => ApciService.GroupValueWrite,
        _ => throw KnxException.UnknownApciService(value & 0x3ff),
    };

    public byte[] Encode()
    {
        var outBytes = new List<byte>(16)
        {
            MessageCodeByte(MessageCode),
            0x00, // Laenge der Zusatzinformationen
            Cf1Standard,
            Cf2GroupHop6,
        };
        outBytes.AddRange(Source.ToBytes());
        outBytes.AddRange(Destination.ToBytes());

        var service = ApciValue(Service);
        if (Payload.IsSmall)
        {
            outBytes.Add(0x01);                                     // NPDU-Laenge
            outBytes.Add(0x00);                                     // TPCI, dazu die oberen zwei APCI-Bit (bei diesen drei Diensten immer 0)
            outBytes.Add((byte)((service & 0xff) | (Payload.Small & 0x3f)));
        }
        else
        {
            outBytes.Add((byte)(1 + Payload.Bytes.Length));
            outBytes.Add(0x00);
            outBytes.Add((byte)(service & 0xff));
            outBytes.AddRange(Payload.Bytes);
        }
        return outBytes.ToArray();
    }

    public static CemiFrame Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 11) throw KnxException.Truncated();
        var messageCode = MessageCodeFrom(bytes[0]);
        var additionalInfoLength = bytes[1];
        var b = 2 + additionalInfoLength;
        if (bytes.Length < b + 9) throw KnxException.Truncated();

        // bytes[b] ist CF1 und wird bewusst nicht uebernommen. bytes[b+1]
        // (CF2) dagegen schon: Bit 7 sagt, ob das Ziel eine Gruppen- oder
        // eine physikalische Adresse ist. Ohne diese Pruefung wuerde ein
        // Punkt-zu-Punkt-Telegramm mit seinen zwei Zieladressbytes still als
        // Gruppenadresse gelesen — und koennte bei zufaellig gleicher Zahl
        // ein wartendes Lesen auf einer voellig anderen Gruppe beantworten.
        if ((bytes[b + 1] & 0x80) == 0) throw KnxException.Truncated();

        var source = IndividualAddress.FromBytes(bytes[b + 2], bytes[b + 3]);
        var destination = GroupAddress.FromBytes(bytes[b + 4], bytes[b + 5]);
        var npduLength = bytes[b + 6];

        // Die oberen sechs Bit sind TPCI. Alles, was dieser Helfer baut oder
        // erwartet, ist T_Data_Group, unnummeriert — TPCI also 0. Steht dort
        // etwas anderes, bedeuten die APCI-Bit etwas anderes, und sie als
        // GroupValue* zu lesen waere eine Fehldeutung, keine Antwort.
        if ((bytes[b + 7] & 0xfc) != 0) throw KnxException.Truncated();

        var apciHi = bytes[b + 7] & 0x03;
        var apciLo = bytes[b + 8];

        if (npduLength == 1)
        {
            var service = ApciFrom((apciHi << 8) | (apciLo & 0xc0));
            return new CemiFrame(messageCode, source, destination, service,
                Payload.FromSmall((byte)(apciLo & 0x3f)));
        }

        // NPDU-Laenge 0 ist fehlerhaft: das Paar aus TPCI- und APCI-Oktett,
        // hinter dem sie zurueckbleiben will, ist ohnehin Pflicht. Vor dem
        // Abzug abfangen — diese Bytes kommen direkt vom Netz.
        if (npduLength == 0) throw KnxException.Truncated();

        var longService = ApciFrom((apciHi << 8) | apciLo);
        var dataStart = b + 9;
        var dataLength = npduLength - 1;
        if (bytes.Length < dataStart + dataLength) throw KnxException.Truncated();
        return new CemiFrame(messageCode, source, destination, longService,
            Payload.FromBytes(bytes.Slice(dataStart, dataLength).ToArray()));
    }
}
