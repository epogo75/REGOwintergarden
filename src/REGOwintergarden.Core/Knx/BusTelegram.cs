using System;

namespace REGOwintergarden.Knx;

/// <summary>
/// Ein Gruppentelegramm, so wie es vom Bus hereinkommt.
///
/// Bewusst mit Absender: beim Suchen von Fehlern an einer fremden Anlage ist
/// „wer hat das geschickt" oft die eigentliche Frage — und der Simulator soll
/// spaeter im Protokoll zeigen koennen, ob ein Wert vom HomeServer, von einer
/// Fernbedienung oder von ihm selbst stammt.
/// </summary>
public sealed class BusTelegram
{
    public BusTelegram(
        IndividualAddress source, GroupAddress destination, ApciService service, Payload payload,
        bool isConfirmation = false)
    {
        Source = source;
        Destination = destination;
        Service = service;
        Payload = payload;
        IsConfirmation = isConfirmation;
        // Kein Zeitstempel aus dem Telegramm - der kommt vom Empfang hier,
        // denn auf der Leitung steht keiner.
        Received = DateTimeOffset.Now;
    }

    public IndividualAddress Source { get; }
    public GroupAddress Destination { get; }
    public ApciService Service { get; }
    public Payload Payload { get; }
    public DateTimeOffset Received { get; }

    /// <summary>
    /// Die Bestaetigung des Gateways zu einem selbst gesendeten Telegramm
    /// (L_Data.con) - der Beweis, dass es wirklich auf dem Bus war und nicht
    /// nur bis zum Gateway kam.
    ///
    /// Sie darf <b>nicht</b> wie ein empfangenes Telegramm behandelt werden:
    /// sie wiederholt den eigenen Wert, und ein Geraet, das darauf reagiert,
    /// beantwortet sich selbst.
    /// </summary>
    public bool IsConfirmation { get; }

    /// <summary>Eine Leseanfrage, die dieses Geraet beantworten muss.</summary>
    public bool IsRead => !IsConfirmation && Service == ApciService.GroupValueRead;

    /// <summary>Ein neuer Wert, den dieses Geraet uebernehmen muss.</summary>
    public bool IsWrite => !IsConfirmation && Service == ApciService.GroupValueWrite;

    public override string ToString()
    {
        var richtung = Service switch
        {
            ApciService.GroupValueRead => "lesen",
            ApciService.GroupValueResponse => "Antwort",
            _ => "schreiben",
        };
        var wert = Payload.IsSmall
            ? Payload.Small.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : string.Join(" ", Array.ConvertAll(Payload.Bytes, b => b.ToString("x2")));
        return $"{Source} -> {Destination}  {richtung}  {wert}";
    }
}
