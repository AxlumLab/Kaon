using System.Net;

namespace Kaon.Transport;

/// <summary>
/// Binds a UDP socket. UDP has no real accept step; AcceptAsync returns the bound datagram connection once.
/// </summary>
public sealed class UdpConnectionListener : IListener<UdpConnection>
{
    private readonly UdpConnection _connection;
    private int _accepted;

    public UdpConnectionListener(IPEndPoint localEndPoint)
    {
        _connection = UdpConnection.Bind(localEndPoint);
    }

    public UdpConnectionListener(IPAddress address, int port)
        : this(new IPEndPoint(address, port))
    {
    }

    public EndPoint LocalEndPoint => _connection.LocalEndPoint!;

    public ValueTask<UdpConnection> AcceptAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _accepted, 1) != 0)
            throw new InvalidOperationException("A UDP listener exposes one bound datagram connection.");

        return ValueTask.FromResult(_connection);
    }

    public ValueTask CloseAsync(CancellationToken cancellationToken = default)
    {
        return _connection.CloseAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return CloseAsync();
    }
}
