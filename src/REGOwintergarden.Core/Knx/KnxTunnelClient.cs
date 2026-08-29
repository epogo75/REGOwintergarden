using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace REGOwintergarden.Knx;

/// <summary>
/// Zeiten und Wiederholungsgrenzen einer Tunnelverbindung. Die Vorgaben sind
/// die aus der Protokolluntersuchung; einstellbar sind sie vor allem, damit
/// Tests einen 30-Sekunden-Fehlerweg auf Millisekunden zusammenschieben
/// koennen.
/// </summary>
public sealed class KnxTimings
{
    /// <summary>Abstand zwischen zwei Herzschlaegen im Leerlauf.</summary>
    public TimeSpan HeartbeatRate { get; init; } = TimeSpan.FromSeconds(70);

    /// <summary>Wie lange eine einzelne CONNECTIONSTATE_REQUEST offen bleiben darf.</summary>
    public TimeSpan ConnectionstateTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Nach wie vielen Herzschlaegen hintereinander der Tunnel als tot gilt.</summary>
    public int HeartbeatMaxRetries { get; init; } = 3;

    /// <summary>Wie lange ein TUNNELLING_REQUEST unquittiert bleiben darf.</summary>
    public TimeSpan TunnellingAckTimeout { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>Wie lange das Verbinden auf die CONNECT_RESPONSE wartet.</summary>
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Wie lange das Trennen auf die DISCONNECT_RESPONSE wartet, bevor es den
    /// Kanal einseitig aufgibt.
    /// </summary>
    public TimeSpan DisconnectTimeout { get; init; } = TimeSpan.FromSeconds(1);

    public static KnxTimings Default { get; } = new();
}

/// <summary>
/// Eine offene KNXnet/IP-Tunnelverbindung zu genau einem Gateway.
///
/// <para><b>Kein Wiederverbinden.</b> Stirbt der Tunnel — das Gateway
/// quittiert nicht mehr, der Herzschlag ist aufgebraucht, oder die Gegenseite
/// baut den Kanal selbst ab —, setzt der Client <see cref="IsConnected"/> auf
/// <c>false</c>, beendet seine Hintergrundaufgaben und bleibt so. Wann und wie
/// neu verbunden wird, entscheidet der Aufrufer; hier waere es eine
/// Entscheidung an der falschen Stelle.</para>
///
/// <para><b>Zum Aufraeumen <see cref="DisconnectAsync"/> aufrufen.</b>
/// <see cref="Dispose"/> beendet nur die eigenen Aufgaben — das Gateway
/// erfaehrt davon nichts und haelt den Tunnelplatz bis zu seiner eigenen,
/// langen Zeitgrenze besetzt.</para>
/// </summary>
public sealed class KnxTunnelClient : IDisposable
{
    /// <summary>
    /// Die Absenderadresse auf den Rahmen, die dieser Client sendet. Ein
    /// Tunnelgateway ersetzt sie durch die Adresse, die es dem Tunnel
    /// zugeteilt hat — 0.0.0 ist der uebliche Platzhalter, keine echte Adresse.
    /// </summary>
    private static readonly IndividualAddress UnsetSource = IndividualAddress.Zero;

    /// <summary>
    /// Wie oft ein TUNNELLING_REQUEST auf die Leitung geht, bevor das Senden
    /// als gescheitert gilt: der erste Versuch und eine Wiederholung.
    /// </summary>
    private const int TunnellingSendAttempts = 2;

    private readonly UdpTransport _transport;

    /// <summary>
    /// Der Steuerpunkt: dorthin ging die CONNECT_REQUEST, und dorthin gehen
    /// Herzschlag und Trennen.
    /// </summary>
    private readonly IPEndPoint _gatewayEndPoint;

    /// <summary>
    /// Der Datenpunkt: dorthin gehen TUNNELLING_REQUEST und -ACK. Viele
    /// Gateways antworten von derselben Adresse wie <see cref="_gatewayEndPoint"/>,
    /// die Norm erlaubt aber eine wirklich getrennte. Wer den Tunnelverkehr
    /// trotzdem an den Steuerpunkt schickt, sendet bei so einem Geraet still
    /// ins Leere.
    /// </summary>
    private readonly IPEndPoint _dataEndPoint;

    private readonly byte _channelId;
    private readonly KnxTimings _timings;
    private readonly CancellationTokenSource _shutdown = new();

    private readonly SequenceState _sequence = new();
    private readonly object _sequenceLock = new();

    /// <summary>
    /// Eine Tunnelverbindung erlaubt genau einen offenen TUNNELLING_REQUEST.
    /// Diese Sperre wird ueber den ganzen Sende-und-Quittier-Ablauf gehalten,
    /// damit gleichzeitige Schreib- und Lesevorgaenge sich einreihen, statt
    /// einander den Quittungsplatz zu ueberschreiben.
    /// </summary>
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private readonly object _pendingLock = new();
    private (byte Sequence, TaskCompletionSource<TunnellingAck> Completion)? _pendingAck;
    private readonly List<PendingRead> _pendingReads = new();
    private TaskCompletionSource<ConnectionstateResponse>? _pendingConnectionstate;
    private TaskCompletionSource<DisconnectResponse>? _pendingDisconnect;

    private volatile bool _connected;

    /// <summary>
    /// Getrennt von <see cref="_connected"/>: ein Abbau von innen — verbrauchte
    /// Wiederholungen, toter Herzschlag, aus dem Takt geratener Zaehler — hat
    /// <see cref="_connected"/> schon auf <c>false</c> gesetzt. Ohne dieses
    /// eigene Merkmal wuerde <see cref="DisconnectAsync"/> daraufhin denken, es
    /// sei nie verbunden gewesen, und die DISCONNECT_REQUEST gerade dort
    /// weglassen, wo sie am noetigsten ist — der Tunnelplatz am Gateway bliebe
    /// still belegt.
    /// </summary>
    private int _disconnectRequested;

    private Task _receiveTask = Task.CompletedTask;
    private Task _heartbeatTask = Task.CompletedTask;

    private sealed class PendingRead
    {
        public PendingRead(GroupAddress address)
        {
            Address = address;
            Completion = new TaskCompletionSource<Payload>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public GroupAddress Address { get; }
        public TaskCompletionSource<Payload> Completion { get; }

        /// <summary>Der Wartende hat aufgegeben — der Platz darf weg.</summary>
        public bool Abandoned { get; set; }
    }

    private KnxTunnelClient(
        UdpTransport transport,
        IPEndPoint gatewayEndPoint,
        IPEndPoint dataEndPoint,
        byte channelId,
        IndividualAddress individualAddress,
        KnxTimings timings)
    {
        _transport = transport;
        _gatewayEndPoint = gatewayEndPoint;
        _dataEndPoint = dataEndPoint;
        _channelId = channelId;
        IndividualAddress = individualAddress;
        _timings = timings;
        _connected = true;
    }

    /// <summary>
    /// Oeffnet einen Tunnel: CONNECT_REQUEST von <paramref name="localEndPoint"/>
    /// an <paramref name="gatewayEndPoint"/>, danach laufen Empfang und
    /// Herzschlag im Hintergrund.
    ///
    /// Port 0 in <paramref name="localEndPoint"/> ueberlaesst die Wahl dem
    /// Betriebssystem.
    /// </summary>
    public static async Task<KnxTunnelClient> ConnectAsync(
        IPEndPoint gatewayEndPoint,
        IPEndPoint localEndPoint,
        KnxTimings? timings = null,
        CancellationToken ct = default)
    {
        timings ??= KnxTimings.Default;
        UdpTransport transport;
        try
        {
            transport = UdpTransport.Bind(localEndPoint);
        }
        catch (SocketException ex)
        {
            throw KnxException.Io(ex);
        }

        try
        {
            var bound = transport.LocalEndPoint;
            var hpai = new Hpai(HostProtocol.Udp, bound.Address, bound.Port);
            var request = new ConnectRequest(hpai, hpai).Encode();
            await SendOrThrowAsync(transport, request, gatewayEndPoint, ct).ConfigureAwait(false);

            var response = await AwaitConnectResponseAsync(
                transport, gatewayEndPoint, timings.ConnectTimeout, ct).ConfigureAwait(false);
            if (response.Error is { } code) throw KnxException.ConnectRejected(code);

            // 0.0.0.0 im angekuendigten Datenpunkt heisst „schick es dorthin
            // zurueck, wo die CONNECT_REQUEST herkam" — nur die Adresse ist
            // dieses Zeichen, der Port bleibt bedeutsam. Alles andere ist ein
            // wirklich eigener Datenpunkt, den der Tunnelverkehr treffen muss.
            var dataEndPoint = gatewayEndPoint;
            if (response.DataEndpoint is { } data && !data.Address.Equals(IPAddress.Any))
            {
                dataEndPoint = new IPEndPoint(data.Address, data.Port);
            }

            var client = new KnxTunnelClient(
                transport, gatewayEndPoint, dataEndPoint, response.ChannelId,
                response.AssignedAddress ?? UnsetSource, timings);
            client.Start();
            return client;
        }
        catch
        {
            transport.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Baut einen Client um eine Kanalnummer, die <em>nicht</em> aus einem
    /// Handschlag stammt.
    ///
    /// Nur fuer Tests, die den Sende-, Quittungs- und Herzschlagweg gegen eine
    /// Gegenstelle pruefen, die nie eine CONNECT_REQUEST beantwortet.
    /// </summary>
    internal static KnxTunnelClient ConnectWithChannel(
        IPEndPoint gatewayEndPoint,
        IPEndPoint localEndPoint,
        byte channelId,
        IndividualAddress individualAddress,
        KnxTimings? timings = null)
    {
        var transport = UdpTransport.Bind(localEndPoint);
        // Ohne echten Handschlag gibt es keine CONNECT_RESPONSE, aus der sich
        // ein eigener Datenpunkt ableiten liesse — dieselbe Adresse fuer
        // beides, wie bei jedem Gateway, das sie nicht trennt.
        var client = new KnxTunnelClient(
            transport, gatewayEndPoint, gatewayEndPoint, channelId,
            individualAddress, timings ?? KnxTimings.Default);
        client.Start();
        return client;
    }

    private void Start()
    {
        _receiveTask = Task.Run(ReceiveLoopAsync);
        _heartbeatTask = Task.Run(HeartbeatLoopAsync);
    }

    /// <summary>
    /// <c>false</c>, sobald der Tunnel gestorben oder geschlossen ist. Wird nie
    /// wieder <c>true</c> — neu verbinden heisst einen neuen Client bauen.
    /// </summary>
    public bool IsConnected => _connected;

    /// <summary>
    /// Die physikalische Adresse, die das Gateway diesem Tunnel zugeteilt hat.
    /// </summary>
    public IndividualAddress IndividualAddress { get; }

    /// <summary>
    /// Schickt einen GroupValueWrite an <paramref name="groupAddress"/>.
    ///
    /// Erfolg heisst hier: <b>das Gateway hat den Rahmen zur Uebertragung
    /// angenommen</b> — es hat den TUNNELLING_REQUEST quittiert. Er heisst
    /// nicht, dass das Telegramm auf dem KNX-Bus bestaetigt wurde; diese
    /// Bestaetigung kommt spaeter als L_Data.con, dessen Erfolgsbit hier weder
    /// abgewartet noch ausgewertet wird. Ein abgezogenes, unprogrammiertes
    /// oder den Wert verweigerndes Geraet liefert trotzdem Erfolg.
    /// </summary>
    public Task WriteAsync(GroupAddress groupAddress, Payload payload, CancellationToken ct = default)
    {
        var cemi = new CemiFrame(
            MessageCode.LDataReq, UnsetSource, groupAddress, ApciService.GroupValueWrite, payload);
        return SendWithRetryAsync(cemi.Encode(), ct);
    }

    /// <summary>
    /// Antwortet auf eine Leseanfrage vom Bus (GroupValueResponse).
    ///
    /// Der Unterschied zu <see cref="WriteAsync"/> ist keine Feinheit: ein
    /// Schreiben teilt einen neuen Wert mit, eine Antwort beantwortet eine
    /// gestellte Frage. Wer auf ein GroupValueRead mit einem Schreiben
    /// antwortet, loest bei allen anderen Teilnehmern eine Zustandsaenderung
    /// aus, statt nur dem Fragenden Auskunft zu geben.
    ///
    /// Fuer den Helfer war das nie noetig - er fragt, er wird nicht gefragt.
    /// Ein Geraet auf dem Bus wird gefragt.
    /// </summary>
    /// <summary>
    /// Schickt eine Leseanfrage, ohne auf die Antwort zu warten.
    ///
    /// Fuer eine Bedienoberflaeche ist genau das richtig: sie fragt beim
    /// Oeffnen ein paar Dutzend Adressen ab, und die Antworten kommen ueber
    /// <see cref="TelegramReceived"/> herein, wann immer sie kommen. Auf jede
    /// einzeln zu warten dauerte bei zwanzig Adressen und einer Sekunde
    /// Frist zwanzig Sekunden - und wer eine Ansicht oeffnet, wartet keine
    /// zwanzig Sekunden.
    /// </summary>
    public Task SendReadAsync(GroupAddress groupAddress, CancellationToken ct = default)
    {
        var cemi = new CemiFrame(
            MessageCode.LDataReq, UnsetSource, groupAddress, ApciService.GroupValueRead,
            Payload.FromSmall(0));
        return SendWithRetryAsync(cemi.Encode(), ct);
    }

    public Task RespondAsync(GroupAddress groupAddress, Payload payload, CancellationToken ct = default)
    {
        var cemi = new CemiFrame(
            MessageCode.LDataReq, UnsetSource, groupAddress, ApciService.GroupValueResponse, payload);
        return SendWithRetryAsync(cemi.Encode(), ct);
    }

    /// <summary>
    /// Jedes Gruppentelegramm vom Bus - Schreiben, Antworten und Lesen.
    ///
    /// Der Helfer brauchte nur die Antwort auf seine eigene Frage; ein Geraet
    /// muss alles mithoeren, was auf seinen Adressen passiert, und auf
    /// Leseanfragen antworten. Deshalb meldet die Empfangsschleife hier alles
    /// weiter, bevor sie es an ein wartendes Lesen zustellt.
    /// </summary>
    public event Action<BusTelegram>? TelegramReceived;

    /// <summary>
    /// Schickt einen GroupValueRead und wartet bis zu
    /// <paramref name="readTimeout"/> auf Antwort.
    ///
    /// Als Antwort zaehlen beide: eine GroupValueResponse und ein
    /// unaufgeforderter GroupValueWrite auf dieselbe Gruppenadresse. Aktoren im
    /// Feld melden ihren Zustand tatsaechlich ueber Letzteres.
    ///
    /// <c>null</c> heisst: nichts hat rechtzeitig geantwortet — ein
    /// gewoehnlicher Ausgang bei einer Adresse, auf die niemand hoert, kein
    /// Fehler.
    /// </summary>
    public async Task<Payload?> ReadAsync(
        GroupAddress groupAddress, TimeSpan readTimeout, CancellationToken ct = default)
    {
        var pending = new PendingRead(groupAddress);
        // Vor dem Absenden eingetragen: eine Antwort kann schneller da sein
        // als die Quittung des Gateways.
        lock (_pendingLock) _pendingReads.Add(pending);

        var cemi = new CemiFrame(
            MessageCode.LDataReq, UnsetSource, groupAddress,
            ApciService.GroupValueRead, Payload.FromSmall(0));
        try
        {
            await SendWithRetryAsync(cemi.Encode(), ct).ConfigureAwait(false);
        }
        catch
        {
            Abandon(pending);
            throw;
        }

        try
        {
            return await pending.Completion.Task.WaitAsync(readTimeout, ct).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            Abandon(pending);
            return null;
        }
        catch (OperationCanceledException)
        {
            Abandon(pending);
            throw;
        }
    }

    private void Abandon(PendingRead pending)
    {
        lock (_pendingLock)
        {
            pending.Abandoned = true;
            _pendingReads.Remove(pending);
        }
    }

    /// <summary>
    /// Schliesst den Tunnel: DISCONNECT_REQUEST an das Gateway, damit es den
    /// Platz freigibt, danach der Abbau hier.
    ///
    /// Ein Gateway, das nicht antwortet, ist kein Fehler — der Kanal ist von
    /// dieser Seite ohnehin weg. Gemeldet wird nur ein misslungenes Senden.
    /// </summary>
    public async Task DisconnectAsync()
    {
        _connected = false;
        // Die Weiche ist „haben wir es dem Gateway schon gesagt", nicht der
        // laufende Verbindungszustand: ein Abbau von innen hat den schon auf
        // false gesetzt, ohne das Gateway zu benachrichtigen — und genau dann
        // muss hier trotzdem genau eine DISCONNECT_REQUEST hinaus.
        var alreadyRequested = Interlocked.Exchange(ref _disconnectRequested, 1) == 1;
        try
        {
            if (!alreadyRequested) await RequestDisconnectAsync().ConfigureAwait(false);
        }
        finally
        {
            _shutdown.Cancel();
        }
    }

    private async Task RequestDisconnectAsync()
    {
        var request = new DisconnectRequest(_channelId, LocalHpai()).Encode();
        var completion = new TaskCompletionSource<DisconnectResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_pendingLock) _pendingDisconnect = completion;

        try
        {
            await SendOrThrowAsync(_transport, request, _gatewayEndPoint, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch
        {
            lock (_pendingLock) _pendingDisconnect = null;
            throw;
        }

        try
        {
            await completion.Task.WaitAsync(_timings.DisconnectTimeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // Ohne Antwort ist der Kanal von hier aus trotzdem weg.
        }
        finally
        {
            lock (_pendingLock) _pendingDisconnect = null;
        }
    }

    private async Task SendWithRetryAsync(byte[] cemi, CancellationToken ct)
    {
        await _sendLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Unter der Sendesperre geprueft: ein wartender Aufruf soll nicht
            // seinen ganzen Wiederholungsvorrat an einem Kanal verbrauchen,
            // den der Aufruf vor ihm gerade verloren hat.
            if (!_connected) throw KnxException.NotConnected();

            // Eine Folgenummer je Rahmen: die Wiederholung schickt dieselben
            // Bytes noch einmal, Zaehler eingeschlossen.
            byte sequence;
            lock (_sequenceLock) sequence = _sequence.NextSend();
            var frame = new TunnellingRequest(_channelId, sequence, cemi).Encode();

            for (var attempt = 0; attempt < TunnellingSendAttempts; attempt++)
            {
                var completion = new TaskCompletionSource<TunnellingAck>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                lock (_pendingLock) _pendingAck = (sequence, completion);

                try
                {
                    await SendOrThrowAsync(_transport, frame, _dataEndPoint, ct).ConfigureAwait(false);
                }
                catch
                {
                    lock (_pendingLock) _pendingAck = null;
                    throw;
                }

                try
                {
                    // Eine Quittung mit Fehlerstatus zaehlt wie eine
                    // ausgebliebene: kommt binnen einer Sekunde keine
                    // Quittung ODER meldet sie einen Fehler, wird der Rahmen
                    // einmal wiederholt — mit derselben Folgenummer.
                    var ack = await completion.Task
                        .WaitAsync(_timings.TunnellingAckTimeout, ct).ConfigureAwait(false);
                    if (ack.Status == 0) return;
                }
                catch (TimeoutException)
                {
                }

                lock (_pendingLock) _pendingAck = null;
            }

            await NotifyGatewayOfTeardownAsync().ConfigureAwait(false);
            MarkDisconnected();
            throw KnxException.Timeout();
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private Hpai LocalHpai()
    {
        var bound = _transport.LocalEndPoint;
        return new Hpai(HostProtocol.Udp, bound.Address, bound.Port);
    }

    /// <summary>
    /// Die eine Stelle, an der der Tunnel fuer tot erklaert wird: setzt das
    /// Merkmal, das Aufrufer abfragen, und weckt beide Hintergrundaufgaben,
    /// damit sie einen toten Kanal nicht weiter anfassen.
    /// </summary>
    private void MarkDisconnected()
    {
        _connected = false;
        try { _shutdown.Cancel(); }
        catch (ObjectDisposedException) { }
    }

    /// <summary>
    /// DISCONNECT_REQUEST ohne auf Antwort zu warten: das Gateway soll
    /// erfahren, dass der Kanal weg ist, damit es den Platz freigibt. Warten
    /// kann hier niemand — der Aufrufer ist womoeglich gerade die
    /// Hintergrundaufgabe, die den Socket nicht mehr liest.
    ///
    /// Genau einmal je Sitzung: ein spaeteres <see cref="DisconnectAsync"/>
    /// sieht das Merkmal gesetzt und schickt nichts nach. Ein selbst
    /// erkannter Fehlschlag muss die Verbindung ordentlich beenden und nicht
    /// bloss verstummen — sonst bleibt der Tunnelplatz reserviert.
    /// </summary>
    private async Task NotifyGatewayOfTeardownAsync()
    {
        if (Interlocked.Exchange(ref _disconnectRequested, 1) == 1) return;
        try
        {
            var request = new DisconnectRequest(_channelId, LocalHpai()).Encode();
            await _transport.SendToAsync(request, _gatewayEndPoint).ConfigureAwait(false);
        }
        catch (SocketException) { }
        catch (ObjectDisposedException) { }
        catch (KnxException) { }
    }

    private static async Task SendOrThrowAsync(
        UdpTransport transport, byte[] bytes, IPEndPoint destination, CancellationToken ct)
    {
        try
        {
            await transport.SendToAsync(bytes, destination, ct).ConfigureAwait(false);
        }
        catch (SocketException ex)
        {
            throw KnxException.Io(ex);
        }
        catch (ObjectDisposedException)
        {
            throw KnxException.NotConnected();
        }
    }

    /// <summary>
    /// Liest, bis die CONNECT_RESPONSE des Gateways auftaucht oder die Zeit
    /// abgelaufen ist.
    ///
    /// Alles andere wird verworfen: Pakete von anderen Rechnern, und Pakete,
    /// die keine CONNECT_RESPONSE sind — auf einem gemeinsam genutzten Netz
    /// gehoert dazu regelmaessig das Multicast-Geplauder fremder KNX-Router.
    /// </summary>
    private static async Task<ConnectResponse> AwaitConnectResponseAsync(
        UdpTransport transport, IPEndPoint gatewayEndPoint, TimeSpan limit, CancellationToken ct)
    {
        var buffer = new byte[UdpTransport.MaxFrameLength];
        var clock = Stopwatch.StartNew();

        while (true)
        {
            var remaining = limit - clock.Elapsed;
            if (remaining <= TimeSpan.Zero) throw KnxException.Timeout();

            int length;
            IPEndPoint from;
            try
            {
                using var window = CancellationTokenSource.CreateLinkedTokenSource(ct);
                window.CancelAfter(remaining);
                (length, from) = await transport.ReceiveAsync(buffer, window.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw KnxException.Timeout();
            }
            catch (SocketException ex)
            {
                throw KnxException.Io(ex);
            }

            if (!from.Equals(gatewayEndPoint)) continue;
            try
            {
                return ConnectResponse.Decode(buffer.AsSpan(0, length));
            }
            catch (KnxException)
            {
                // Kein CONNECT_RESPONSE — weiterhoeren.
            }
        }
    }

    /// <summary>
    /// Der einzige Leser des Sockets, solange die Verbindung steht.
    ///
    /// Jedes Paket wird zweimal gefiltert, bevor es irgendeinen Zustand
    /// beruehren kann: nach Absenderadresse, und danach, ob es sich ueberhaupt
    /// lesen laesst. Ein Lesefehler — ein unbekannter Diensttyp wie die
    /// ROUTING_INDICATION eines Routers, ein abgeschnittener Rahmen, ein
    /// absichtlich verbogener — wird verworfen und die Schleife laeuft weiter.
    /// Nichts, was auf diesem Socket ankommt, kann die Verbindung beenden
    /// ausser einem Gateway, das ausdruecklich darum bittet.
    /// </summary>
    private async Task ReceiveLoopAsync()
    {
        var buffer = new byte[UdpTransport.MaxFrameLength];
        while (!_shutdown.IsCancellationRequested)
        {
            int length;
            IPEndPoint from;
            try
            {
                (length, from) = await _transport.ReceiveAsync(buffer, _shutdown.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException)
            {
                // Ein echter Socketfehler, kein geordneter Weg. Ohne diesen
                // Zweig wuerde der Tunnel still taub: IsConnected meldete
                // weiter „gesund", und dem Herzschlag saegt niemand ab.
                await NotifyGatewayOfTeardownAsync().ConfigureAwait(false);
                MarkDisconnected();
                return;
            }

            // Der Socket ist ungebunden, das Betriebssystem reicht also Pakete
            // von jedem Rechner im Netz herein. Ohne diese Pruefung koennte
            // jeder davon eine Quittung faelschen, den Folgezaehler treiben
            // oder ein Lesen beantworten. Zwei Adressen sind zulaessig: der
            // Steuerpunkt und der Datenpunkt.
            if (!from.Equals(_gatewayEndPoint) && !from.Equals(_dataEndPoint)) continue;

            // Alles Zerlegen geschieht hier, vor jedem Warten: eine Span darf
            // ein await nicht ueberleben, und die Rahmen liegen in genau
            // diesem einen, wiederverwendeten Puffer.
            var decoded = TryDecode(buffer.AsSpan(0, length));
            switch (decoded)
            {
                case TunnellingAck ack:
                    HandleAck(ack);
                    break;
                case TunnellingRequest request:
                    if (!await HandleTunnellingRequestAsync(request).ConfigureAwait(false)) return;
                    break;
                case ConnectionstateResponse response:
                    HandleConnectionstateResponse(response);
                    break;
                case DisconnectRequest request:
                    if (await HandleDisconnectRequestAsync(request).ConfigureAwait(false)) return;
                    break;
                case DisconnectResponse response:
                    if (HandleDisconnectResponse(response)) return;
                    break;
                default:
                    break;
            }
        }
    }

    /// <summary>
    /// Liest einen Rahmen des Sockets in sein passendes Objekt, oder gibt
    /// <c>null</c> zurueck, wenn er keiner der hier erwarteten Dienste ist oder
    /// sich nicht lesen laesst.
    /// </summary>
    private static object? TryDecode(ReadOnlySpan<byte> frame)
    {
        try
        {
            var header = KnxHeader.Decode(frame, out _);
            return header.ServiceType switch
            {
                ServiceType.TunnellingAck => TunnellingAck.Decode(frame),
                ServiceType.TunnellingRequest => TunnellingRequest.Decode(frame),
                ServiceType.ConnectionstateResponse => ConnectionstateResponse.Decode(frame),
                ServiceType.DisconnectRequest => DisconnectRequest.Decode(frame),
                ServiceType.DisconnectResponse => DisconnectResponse.Decode(frame),
                _ => null,
            };
        }
        catch (KnxException)
        {
            return null;
        }
    }

    private void HandleAck(TunnellingAck ack)
    {
        if (ack.ChannelId != _channelId) return;

        TaskCompletionSource<TunnellingAck>? completion = null;
        lock (_pendingLock)
        {
            if (_pendingAck is { } pending && pending.Sequence == ack.Sequence)
            {
                completion = pending.Completion;
                _pendingAck = null;
            }
        }
        completion?.TrySetResult(ack);
    }

    /// <returns><c>false</c>, wenn die Empfangsschleife enden soll.</returns>
    private async Task<bool> HandleTunnellingRequestAsync(TunnellingRequest request)
    {
        if (request.ChannelId != _channelId) return true;

        ReceiveOutcome outcome;
        lock (_sequenceLock) outcome = _sequence.OnReceived(request.Sequence);

        if (outcome is ReceiveOutcome.Accept or ReceiveOutcome.DuplicateAck)
        {
            var ack = new TunnellingAck(_channelId, request.Sequence, 0).Encode();
            try { await _transport.SendToAsync(ack, _dataEndPoint).ConfigureAwait(false); }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }
        }

        if (outcome == ReceiveOutcome.RejectAndReconnect)
        {
            // Eine Folgenummer ausserhalb des Fensters heisst fast immer, dass
            // der Zaehler des Gateways an unserem vorbeigezogen ist: es hat N
            // geschickt, wir haben nicht rechtzeitig quittiert, es ist zu N+1
            // weiter. Verwerfen und weitermachen liesse unsere Erwartung fuer
            // immer auf N stehen — der Tunnel waere taub fuer jedes weitere
            // Telegramm, waehrend IsConnected weiter „gesund" meldet. Also
            // abbauen: ein sichtbarer Fehlschlag ist genau das, was hier
            // gebraucht wird, statt einen Taktverlust zu ueberkleben.
            await NotifyGatewayOfTeardownAsync().ConfigureAwait(false);
            MarkDisconnected();
            return false;
        }

        if (outcome != ReceiveOutcome.Accept) return true; // schon quittiert, sonst nichts zu tun

        DispatchBusTelegram(request.Cemi);
        return true;
    }

    private void HandleConnectionstateResponse(ConnectionstateResponse response)
    {
        if (response.ChannelId != _channelId) return;

        TaskCompletionSource<ConnectionstateResponse>? completion;
        lock (_pendingLock)
        {
            completion = _pendingConnectionstate;
            _pendingConnectionstate = null;
        }
        completion?.TrySetResult(response);
    }

    /// <returns><c>true</c>, wenn die Empfangsschleife enden soll.</returns>
    private async Task<bool> HandleDisconnectRequestAsync(DisconnectRequest request)
    {
        // Das Gateway baut den Kanal von seiner Seite ab: antworten und den
        // Tunnel danach als weg betrachten.
        if (request.ChannelId != _channelId) return false;

        var response = new DisconnectResponse(_channelId, null).Encode();
        try { await _transport.SendToAsync(response, _gatewayEndPoint).ConfigureAwait(false); }
        catch (SocketException) { }
        catch (ObjectDisposedException) { }
        MarkDisconnected();
        return true;
    }

    /// <returns><c>true</c>, wenn die Empfangsschleife enden soll.</returns>
    private bool HandleDisconnectResponse(DisconnectResponse response)
    {
        if (response.ChannelId != _channelId) return false;

        TaskCompletionSource<DisconnectResponse>? completion;
        lock (_pendingLock)
        {
            completion = _pendingDisconnect;
            _pendingDisconnect = null;
        }
        completion?.TrySetResult(response);
        // MarkDisconnected auch dann, wenn niemand gewartet hat: eine
        // unaufgeforderte DISCONNECT_RESPONSE muss den Abbau ebenfalls
        // ausloesen, sonst klopft der Herzschlag weiter an einen Kanal, den
        // die Empfangsschleife gerade verlaesst.
        MarkDisconnected();
        return true;
    }

    /// <summary>
    /// Reicht ein gelesenes Bustelegramm an das Lesen weiter, das auf diese
    /// Gruppenadresse wartet — falls eines wartet.
    /// </summary>
    private void DispatchBusTelegram(byte[] cemiBytes)
    {
        CemiFrame cemi;
        try { cemi = CemiFrame.Decode(cemiBytes); }
        catch (KnxException) { return; }

        // Nur L_Data.ind: ein echtes Gateway schickt zu jedem L_Data.req auch
        // ein L_Data.con zurueck und wiederholt darin dessen Nutzwert. Ohne
        // diese Pruefung koennte ein Lesen, das neben einem Schreiben auf
        // derselben Gruppenadresse laeuft, sich aus der eigenen Bestaetigung
        // beantworten — und der Aufrufer haette keine Moeglichkeit, den
        // Unterschied zu bemerken.
        if (cemi.MessageCode is not (MessageCode.LDataInd or MessageCode.LDataCon)) return;

        // Erst melden, dann zustellen. Ein Geraet auf dem Bus muss auch
        // Leseanfragen sehen - die faellt der Filter darunter heraus, weil
        // ein GroupValueRead nie die Antwort auf ein eigenes Lesen ist.
        //
        // Gemeldet wird auch die Bestaetigung des Gateways zu einem selbst
        // gesendeten Telegramm (L_Data.con). Fuer einen Busmonitor ist sie die
        // halbe Wahrheit: sie belegt, dass das Telegramm wirklich auf dem Bus
        // war. Zugestellt wird sie unten trotzdem nicht - sie wiederholt den
        // eigenen Wert, und ein wartendes Lesen wuerde sich sonst aus der
        // eigenen Bestaetigung beantworten.
        var listeners = TelegramReceived;
        if (listeners is not null)
        {
            listeners(new BusTelegram(cemi.Source, cemi.Destination, cemi.Service, cemi.Payload,
                isConfirmation: cemi.MessageCode == MessageCode.LDataCon));
        }

        if (cemi.MessageCode != MessageCode.LDataInd) return;

        if (cemi.Service is not (ApciService.GroupValueResponse or ApciService.GroupValueWrite)) return;

        PendingRead? waiting = null;
        lock (_pendingLock)
        {
            for (var i = 0; i < _pendingReads.Count; i++)
            {
                if (_pendingReads[i].Address == cemi.Destination && !_pendingReads[i].Abandoned)
                {
                    waiting = _pendingReads[i];
                    _pendingReads.RemoveAt(i);
                    break;
                }
            }
        }
        waiting?.Completion.TrySetResult(cemi.Payload);
    }

    /// <summary>Ein Herzschlag. Erfolg heisst: der Kanal steht noch.</summary>
    private async Task ProbeConnectionstateAsync()
    {
        var request = new ConnectionstateRequest(_channelId, LocalHpai()).Encode();
        var completion = new TaskCompletionSource<ConnectionstateResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_pendingLock) _pendingConnectionstate = completion;

        try
        {
            await SendOrThrowAsync(_transport, request, _gatewayEndPoint, _shutdown.Token)
                .ConfigureAwait(false);
        }
        catch
        {
            lock (_pendingLock) _pendingConnectionstate = null;
            throw;
        }

        ConnectionstateResponse response;
        try
        {
            response = await completion.Task
                .WaitAsync(_timings.ConnectionstateTimeout, _shutdown.Token).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            lock (_pendingLock) _pendingConnectionstate = null;
            throw KnxException.Timeout();
        }

        if (response.Error is { } code) throw KnxException.ConnectRejected(code);
    }

    /// <summary>
    /// Klopft alle <see cref="KnxTimings.HeartbeatRate"/> an und erklaert den
    /// Tunnel nach <see cref="KnxTimings.HeartbeatMaxRetries"/> vergeblichen
    /// Versuchen hintereinander fuer tot.
    /// </summary>
    private async Task HeartbeatLoopAsync()
    {
        while (true)
        {
            try
            {
                await Task.Delay(_timings.HeartbeatRate, _shutdown.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            var failures = 0;
            while (failures < _timings.HeartbeatMaxRetries)
            {
                try
                {
                    await ProbeConnectionstateAsync().ConfigureAwait(false);
                    break;
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (KnxException)
                {
                    failures++;
                }
            }

            if (failures >= _timings.HeartbeatMaxRetries)
            {
                await NotifyGatewayOfTeardownAsync().ConfigureAwait(false);
                MarkDisconnected();
                return;
            }
        }
    }

    public void Dispose()
    {
        _connected = false;
        try { _shutdown.Cancel(); }
        catch (ObjectDisposedException) { }
        // Auf die Hintergrundaufgaben wird bewusst nicht gewartet: Dispose darf
        // nicht blockieren, und beide enden von selbst, sobald das Abbruchzeichen
        // steht. Der Socket bleibt so lange offen — sie wuerden sonst mitten im
        // Empfangen auf einen geschlossenen Socket greifen.
        _ = Task.WhenAll(_receiveTask, _heartbeatTask).ContinueWith(
            _ =>
            {
                _transport.Dispose();
                _shutdown.Dispose();
                _sendLock.Dispose();
            },
            TaskScheduler.Default);
    }
}
