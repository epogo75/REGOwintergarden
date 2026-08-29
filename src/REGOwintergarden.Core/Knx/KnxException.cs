using System;

namespace REGOwintergarden.Knx;

/// <summary>
/// Woran ein KNX-Vorgang gescheitert ist. Der Aufrufer soll verzweigen
/// koennen, ohne Text zu vergleichen — deshalb eine Kennung neben der
/// Meldung, gleiche Bauart wie <c>ModbusFault</c> in REGOmodbus.
/// </summary>
public enum KnxFault
{
    /// <summary>Die Bytes ergeben keinen vollstaendigen Rahmen.</summary>
    Truncated,

    /// <summary>
    /// Ein baulich gueltiger Rahmen, aber dem falschen Entschluessler
    /// vorgelegt. Mehrere Antwortkoerper sind byteweise identisch und nur am
    /// Diensttyp im Kopf zu unterscheiden — CONNECT_RESPONSE,
    /// CONNECTIONSTATE_RESPONSE und DISCONNECT_RESPONSE teilen sich dieselbe
    /// zwei Byte lange Form. Das ist etwas anderes als <see cref="Truncated"/>:
    /// dort ergeben die Bytes ueberhaupt keinen gueltigen Rahmen.
    /// </summary>
    UnexpectedServiceType,

    /// <summary>Ein cEMI-Nachrichtencode ausserhalb von L_Data.req/con/ind.</summary>
    UnknownMessageCode,

    /// <summary>
    /// Ein cEMI-APCI-Dienst ausserhalb von GroupValueRead/Response/Write.
    /// Bewusst getrennt von <see cref="UnknownServiceType"/>: die Zahlenraeume
    /// aehneln sich, bedeuten aber voellig Verschiedenes.
    /// </summary>
    UnknownApciService,

    /// <summary>
    /// Eine Adresse liess sich nicht lesen: falsche Anzahl Bestandteile, ein
    /// nicht numerischer Teil, oder ein Teil ausserhalb seines Bereichs. Anders
    /// als <see cref="Truncated"/> kommt das nicht von der Leitung, sondern aus
    /// einer Eingabe.
    /// </summary>
    InvalidAddress,

    /// <summary>
    /// Ein Wert liegt ausserhalb dessen, was der Datenpunkttyp zulaesst — etwa
    /// ueber 100 % oder ein HVAC-Modus ueber 4. Die Form stimmt, der Wert nicht.
    /// </summary>
    ValueOutOfRange,

    /// <summary>
    /// Ein fuehrendes Laengenbyte (der 0x06 des KNXnet/IP-Kopfes, die 0x08
    /// eines HPAI) passt nicht. Gemeldet wird der Wert, den das Byte selbst
    /// trug — nicht die tatsaechliche Laenge, dafuer gibt es
    /// <see cref="Truncated"/>.
    /// </summary>
    InvalidLength,

    UnsupportedProtocolVersion,
    UnsupportedHostProtocol,
    UnknownServiceType,

    /// <summary>Das Gateway hat die Verbindung mit einem Statusbyte abgelehnt.</summary>
    ConnectRejected,

    /// <summary>Keine Antwort in der Zeit, die dieser Austausch zulaesst.</summary>
    Timeout,

    /// <summary>Fehler auf Socketebene.</summary>
    Io,

    /// <summary>
    /// Der Tunnel ist tot oder geschlossen. Er erholt sich nicht von selbst,
    /// es braucht eine neue Verbindung.
    /// </summary>
    NotConnected,
}

/// <summary>
/// Jeder Fehlschlag dieser Bibliothek. Die Entschluessler werfen statt einen
/// Ergebnistyp zurueckzugeben — die Empfangsschleifen fangen das und machen
/// mit dem naechsten Telegramm weiter. Bei den hier ueblichen Raten (einzelne
/// Rahmen je Sekunde) kostet das nichts und liest sich deutlich besser als ein
/// durchgereichtes Fehlerergebnis durch acht Schichten.
/// </summary>
public sealed class KnxException : Exception
{
    public KnxException(KnxFault fault, string message) : base(message)
    {
        Fault = fault;
    }

    public KnxFault Fault { get; }

    /// <summary>
    /// Gesetzt bei <see cref="KnxFault.ConnectRejected"/>: der Grund, den das
    /// Gateway genannt hat. So kann ein Aufrufer auf „keine freien Tunnel"
    /// verzweigen und hat trotzdem eine anzeigbare Meldung.
    /// </summary>
    public ConnectionErrorCode? ErrorCode { get; init; }

    public static KnxException Truncated() =>
        new(KnxFault.Truncated, "Rahmen unvollstaendig");

    public static KnxException UnexpectedServiceType(ServiceType expected, ServiceType got) =>
        new(KnxFault.UnexpectedServiceType, $"Unerwarteter Diensttyp: erwartet {expected}, war {got}");

    public static KnxException UnknownMessageCode(byte code) =>
        new(KnxFault.UnknownMessageCode, $"Unbekannter cEMI-Nachrichtencode: 0x{code:x2}");

    public static KnxException UnknownApciService(int service) =>
        new(KnxFault.UnknownApciService, $"Unbekannter cEMI-APCI-Dienst: 0x{service:x3}");

    public static KnxException InvalidAddress(string input) =>
        new(KnxFault.InvalidAddress, $"Ungueltige Adresse: \"{input}\"");

    public static KnxException ValueOutOfRange(string what) =>
        new(KnxFault.ValueOutOfRange, $"Wert ausserhalb des gueltigen Bereichs: {what}");

    public static KnxException InvalidLength(int expected, int declared) =>
        new(KnxFault.InvalidLength, $"Ungueltiges Laengenbyte: erwartet {expected}, angegeben {declared}");

    public static KnxException UnsupportedProtocolVersion(byte got) =>
        new(KnxFault.UnsupportedProtocolVersion, $"KNXnet/IP-Protokollversion nicht unterstuetzt: 0x{got:x2}");

    public static KnxException UnsupportedHostProtocol(byte got) =>
        new(KnxFault.UnsupportedHostProtocol, $"Host-Protokoll nicht unterstuetzt: 0x{got:x2}");

    public static KnxException UnknownServiceType(int value) =>
        new(KnxFault.UnknownServiceType, $"Unbekannter Diensttyp: 0x{value:x4}");

    public static KnxException ConnectRejected(ConnectionErrorCode code) =>
        new(KnxFault.ConnectRejected, $"Verbindung abgelehnt: {ConnectionError.Describe(code)}")
        {
            ErrorCode = code,
        };

    public static KnxException Timeout() =>
        new(KnxFault.Timeout, "Zeitueberschreitung: keine Antwort vom Gateway");

    public static KnxException Io(Exception inner) =>
        new(KnxFault.Io, $"Netzwerkfehler: {inner.Message}");

    public static KnxException NotConnected() =>
        new(KnxFault.NotConnected, "Keine Verbindung zum Gateway");
}
