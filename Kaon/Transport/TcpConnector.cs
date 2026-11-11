using System.Net;
using System.Net.Sockets;

namespace Kaon.Transport;

/// <summary>
/// Creates outbound TCP stream connections.
/// </summary>
public sealed class TcpConnector : IConnector<TcpConnection>
{
    public static TcpConnector Shared { get; } = new();

    public async ValueTask<TcpConnection> ConnectAsync(EndPoint remoteEndPoint, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(remoteEndPoint);

        var client = new TcpClient { NoDelay = true };

        try
        {
            switch (remoteEndPoint)
            {
                case IPEndPoint ipEndPoint:
                    await client.ConnectAsync(ipEndPoint, cancellationToken).ConfigureAwait(false);
                    break;
                case DnsEndPoint dnsEndPoint:
                    await client.ConnectAsync(dnsEndPoint.Host, dnsEndPoint.Port, cancellationToken).ConfigureAwait(false);
                    break;
                default:
                    throw new NotSupportedException($"Unsupported TCP endpoint: {remoteEndPoint.GetType().FullName}");
            }

            return new TcpConnection(client);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    public async ValueTask<TcpConnection> ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        return await ConnectAsync(new DnsEndPoint(host, port), cancellationToken).ConfigureAwait(false);
    }
}
