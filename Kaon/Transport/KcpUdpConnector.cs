using System.Net;

namespace Kaon.Transport;

/// <summary>
/// Creates outbound reliable UDP stream connections backed by KCP.
/// </summary>
public sealed class KcpUdpConnector(KcpUdpOptions? options = null) : IConnector<KcpUdpConnection>
{
    public static KcpUdpConnector Shared { get; } = new();

    public async ValueTask<KcpUdpConnection> ConnectAsync(EndPoint remoteEndPoint, CancellationToken cancellationToken = default)
    {
        var udp = await UdpConnector.Shared.ConnectAsync(remoteEndPoint, cancellationToken).ConfigureAwait(false);
        return new KcpUdpConnection(udp, udp.RemoteEndPoint ?? remoteEndPoint, options);
    }

    public async ValueTask<KcpUdpConnection> ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        var udp = await UdpConnector.Shared.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
        return new KcpUdpConnection(udp, udp.RemoteEndPoint!, options);
    }
}
