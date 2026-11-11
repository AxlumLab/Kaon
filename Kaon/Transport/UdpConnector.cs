using System.Net;
using System.Net.Sockets;

namespace Kaon.Transport;

/// <summary>
/// Creates outbound UDP datagram connections.
/// </summary>
public sealed class UdpConnector : IConnector<UdpConnection>
{
    public static UdpConnector Shared { get; } = new();

    public async ValueTask<UdpConnection> ConnectAsync(EndPoint remoteEndPoint, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(remoteEndPoint);

        var resolved = await ResolveRemoteAsync(remoteEndPoint, cancellationToken).ConfigureAwait(false);
        var socket = new Socket(resolved.AddressFamily, SocketType.Dgram, ProtocolType.Udp);

        try
        {
            socket.Bind(new IPEndPoint(resolved.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, 0));
            return new UdpConnection(socket, resolved);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    public async ValueTask<UdpConnection> ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        return await ConnectAsync(new DnsEndPoint(host, port), cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<IPEndPoint> ResolveRemoteAsync(EndPoint remoteEndPoint, CancellationToken cancellationToken)
    {
        return remoteEndPoint switch
        {
            IPEndPoint ipEndPoint => ipEndPoint,
            DnsEndPoint dnsEndPoint => new IPEndPoint(
                (await Dns.GetHostAddressesAsync(dnsEndPoint.Host, cancellationToken).ConfigureAwait(false))[0],
                dnsEndPoint.Port),
            _ => throw new NotSupportedException($"Unsupported UDP endpoint: {remoteEndPoint.GetType().FullName}")
        };
    }
}
