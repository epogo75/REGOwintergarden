using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace REGOwintergarden.Knx;

/// <summary>
/// Der UDP-Socket, ueber den alles laeuft. Duenn genug, um im Test durch eine
/// Schleife ueber 127.0.0.1 ersetzbar zu sein.
/// </summary>
public sealed class UdpTransport : IDisposable
{
    /// <summary>
    /// Groesse jedes Empfangspuffers dieser Bibliothek.
    ///
    /// Ein UDP-Paket kommt ganz oder gar nicht an — aber eines, das groesser
    /// ist als der uebergebene Puffer, wird vom Betriebssystem still
    /// abgeschnitten: keine Fehlermeldung, kein Hinweis, der Rest ist weg. Die
    /// Entschluessler hier weisen unvollstaendige Rahmen sauber zurueck, ein zu
    /// kleiner Puffer wuerde also nichts zum Absturz bringen. Er taete etwas
    /// Schlimmeres: einen tadellosen Rahmen des Gateways als fehlerhaft melden.
    ///
    /// 512 liegt weit ueber allem, was hier ankommen kann: der groesste
    /// TUNNELLING_REQUEST eines KNX-Standardtelegramms bleibt deutlich unter
    /// 40 Byte, und der groesste Rahmen ueberhaupt — eine SEARCH_RESPONSE —
    /// bleibt auch bei einem gespraechigen Gateway unter etwa 200 Byte.
    /// </summary>
    public const int MaxFrameLength = 512;

    private readonly Socket _socket;

    private UdpTransport(Socket socket) => _socket = socket;

    public static UdpTransport Bind(IPEndPoint localEndPoint)
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        try
        {
            // Muss vor dem Binden gesetzt sein. Noetig, sobald sonst etwas auf
            // dem Rechner den Port 3671 schon haelt — eine laufende ETS zum
            // Beispiel.
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            DisableConnectionReset(socket);
            socket.Bind(localEndPoint);
            return new UdpTransport(socket);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Schaltet eine Windows-Eigenheit ab, die UDP sonst unbrauchbar macht:
    /// Schickt man ein Paket an einen Port, an dem niemand horcht, antwortet
    /// die Gegenstelle mit ICMP „Port nicht erreichbar" — und Windows laesst
    /// daraufhin den <em>naechsten</em> Empfangsaufruf auf diesem Socket mit
    /// Fehler 10054 scheitern, obwohl UDP gar keine Verbindung kennt.
    ///
    /// Ohne das hier stirbt die Empfangsschleife, sobald ein gesuchtes Gateway
    /// ausgeschaltet ist — mit der Meldung „Netzwerkfehler", die auf ein
    /// Problem der eigenen Leitung hindeutet statt auf ein stummes Geraet.
    /// </summary>
    private static void DisableConnectionReset(Socket socket)
    {
        const int SioUdpConnreset = unchecked((int)0x9800000C);
        try
        {
            socket.IOControl((IOControlCode)SioUdpConnreset, new byte[] { 0, 0, 0, 0 }, null);
        }
        catch (SocketException)
        {
            // Nicht ueberall vorhanden. Wo nicht, gibt es die Eigenheit auch
            // nicht — kein Grund, das Binden scheitern zu lassen.
        }
        catch (PlatformNotSupportedException)
        {
        }
    }

    public IPEndPoint LocalEndPoint => (IPEndPoint)_socket.LocalEndPoint!;

    public async Task SendToAsync(byte[] bytes, IPEndPoint destination, CancellationToken ct = default)
    {
        await _socket.SendToAsync(bytes, SocketFlags.None, destination, ct).ConfigureAwait(false);
    }

    public async Task<(int Length, IPEndPoint From)> ReceiveAsync(
        Memory<byte> buffer, CancellationToken ct = default)
    {
        var any = new IPEndPoint(IPAddress.Any, 0);
        var result = await _socket.ReceiveFromAsync(buffer, SocketFlags.None, any, ct)
            .ConfigureAwait(false);
        return (result.ReceivedBytes, (IPEndPoint)result.RemoteEndPoint);
    }

    /// <summary>
    /// Lebensdauer 2 entspricht der Einstellung von xknx: genug, um eine
    /// Router-Grenze zu ueberschreiten — etwa zwischen dem KNX-Netz eines
    /// Kunden und dem restlichen Haus —, ohne weiter zu streuen.
    /// </summary>
    public void JoinMulticast(IPAddress group, IPAddress localInterface)
    {
        _socket.SetSocketOption(
            SocketOptionLevel.IP, SocketOptionName.AddMembership,
            new MulticastOption(group, localInterface));
        _socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 2);
    }

    public void Dispose() => _socket.Dispose();
}
