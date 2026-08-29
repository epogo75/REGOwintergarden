using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace REGOwintergarden.Knx;

public sealed class DiscoveredGateway
{
    public DiscoveredGateway(
        string name, IPAddress host, int port, IndividualAddress? individualAddress,
        bool supportsTunneling, TunnelingInfo? tunneling)
    {
        Name = name;
        Host = host;
        Port = port;
        IndividualAddress = individualAddress;
        SupportsTunneling = supportsTunneling;
        Tunneling = tunneling;
    }

    /// <summary>
    /// Die Tunnelplaetze, falls das Gateway die erweiterte Suche beantwortet
    /// hat. <c>null</c> heisst nicht "keine Plaetze", sondern "keine Auskunft" -
    /// aeltere Gateways kennen die Frage nicht. Der Unterschied gehoert in die
    /// Anzeige, sonst liest sich Schweigen wie "voll".
    /// </summary>
    public TunnelingInfo? Tunneling { get; }

    /// <summary>Freie Tunnelplaetze als Text, oder ein Strich bei fehlender Auskunft.</summary>
    public string FreeTunnelsText => Tunneling is null
        ? "—"
        : $"{Tunneling.FreeCount} von {Tunneling.Slots.Count}";

    public string Name { get; }
    public IPAddress Host { get; }
    public int Port { get; }
    public IndividualAddress? IndividualAddress { get; }
    public bool SupportsTunneling { get; }

    public string Address => $"{Host}:{Port}";

    public override string ToString() =>
        string.IsNullOrWhiteSpace(Name) ? Address : $"{Name} ({Address})";
}

/// <summary>
/// Gatewaysuche ueber Multicast: eine SEARCH_REQUEST hinausschicken und
/// einsammeln, was innerhalb der Wartezeit antwortet.
///
/// Bewusst schlichter als die Suche in REGOdeploy selbst — ohne Aufzaehlung
/// der Tunnelplaetze. Die stammt dort aus einer geraetespezifischen Abfrage,
/// nicht aus der SEARCH_RESPONSE, und die Oberflaeche des Helfers braucht sie
/// nicht.
/// </summary>
public static class Discovery
{
    private static readonly IPAddress MulticastAddress = IPAddress.Parse("224.0.23.12");
    private const int KnxPort = 3671;

    public static async Task<IReadOnlyList<DiscoveredGateway>> DiscoverAsync(
        IPAddress localInterface, TimeSpan searchTimeout, CancellationToken ct = default)
    {
        UdpTransport transport;
        try
        {
            transport = UdpTransport.Bind(new IPEndPoint(localInterface, 0));
            transport.JoinMulticast(MulticastAddress, localInterface);
        }
        catch (SocketException ex)
        {
            throw KnxException.Io(ex);
        }

        using (transport)
        {
            return await CollectAsync(
                transport, new IPEndPoint(MulticastAddress, KnxPort), searchTimeout, ct)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Fuer Tests: schickt die Suche als Einzelpaket an eine Adresse statt per
    /// Multicast, damit ein nachgebildetes Gateway auf 127.0.0.1 die Auswertung
    /// pruefen kann, ohne dass die Maschine multicastfaehig sein muss.
    /// </summary>
    public static async Task<IReadOnlyList<DiscoveredGateway>> DiscoverUnicastAsync(
        IPEndPoint target, TimeSpan searchTimeout, CancellationToken ct = default)
    {
        using var transport = UdpTransport.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return await CollectAsync(transport, target, searchTimeout, ct).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<DiscoveredGateway>> CollectAsync(
        UdpTransport transport, IPEndPoint target, TimeSpan searchTimeout, CancellationToken ct)
    {
        var local = transport.LocalEndPoint;
        var hpai = new Hpai(HostProtocol.Udp, local.Address, local.Port);

        // Beide Fragen hinausschicken, nicht nur eine: die erweiterte bringt
        // die Tunnelplaetze mit, aber aeltere Gateways kennen sie nicht und
        // schweigen darauf. Wer nur erweitert fragt, findet solche Geraete
        // gar nicht; wer nur gewoehnlich fragt, erfaehrt nie, ob noch ein
        // Platz frei ist. Antworten beider Arten laufen unten zusammen.
        foreach (var request in new[]
                 {
                     new SearchRequest(hpai).Encode(),
                     new SearchRequest(hpai, extended: true).Encode(),
                 })
        {
            try
            {
                await transport.SendToAsync(request, target, ct).ConfigureAwait(false);
            }
            catch (SocketException ex)
            {
                throw KnxException.Io(ex);
            }
        }

        var results = new List<DiscoveredGateway>();
        var buffer = new byte[UdpTransport.MaxFrameLength];
        var clock = Stopwatch.StartNew();

        while (true)
        {
            var remaining = searchTimeout - clock.Elapsed;
            if (remaining <= TimeSpan.Zero) break;

            int length;
            try
            {
                using var window = CancellationTokenSource.CreateLinkedTokenSource(ct);
                window.CancelAfter(remaining);
                (length, _) = await transport.ReceiveAsync(buffer, window.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break; // Wartezeit abgelaufen — fertig eingesammelt.
            }
            catch (SocketException)
            {
                break;
            }

            SearchResponse response;
            try
            {
                response = SearchResponse.Decode(buffer.AsSpan(0, length));
            }
            catch (KnxException)
            {
                continue; // Auf dem Multicast liegt auch Verkehr, der uns nichts angeht.
            }

            var gateway = new DiscoveredGateway(
                response.DeviceInfo?.FriendlyName ?? "",
                response.ControlEndpoint.Address,
                response.ControlEndpoint.Port,
                response.DeviceInfo?.IndividualAddress,
                response.ServiceFamilies?.SupportsTunneling ?? false,
                response.Tunneling);

            // Jedes Gateway antwortet zweimal - einmal je Frage. Die Antwort
            // mit Tunnelauskunft gewinnt, sonst haengt es vom Zufall der
            // Reihenfolge ab, ob die Plaetze angezeigt werden.
            var vorhanden = results.FindIndex(
                g => g.Host.Equals(gateway.Host) && g.Port == gateway.Port);
            if (vorhanden < 0) results.Add(gateway);
            else if (results[vorhanden].Tunneling is null && gateway.Tunneling is not null)
            {
                results[vorhanden] = gateway;
            }
        }

        return results;
    }
}
