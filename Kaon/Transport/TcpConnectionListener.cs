using System.Net;
using System.Net.Sockets;

namespace Kaon.Transport;

/// <summary>
/// Accepts inbound TCP stream connections.
/// The accepted connection is still a raw stream; callers decide whether to wrap it as a tunnel or proxy endpoint.
/// </summary>
public sealed class TcpConnectionListener : IListener<TcpConnection>
{
    private readonly TcpListener _listener;
    private int _started;
    private int _closed;

    public TcpConnectionListener(IPEndPoint localEndPoint)
    {
        _listener = new TcpListener(localEndPoint);
    }

    public TcpConnectionListener(IPAddress address, int port)
        : this(new IPEndPoint(address, port))
    {
    }

    public EndPoint LocalEndPoint => _listener.LocalEndpoint;

    public void Start()
    {
        if (IsClosed)
            throw new ObjectDisposedException(nameof(TcpConnectionListener));

        if (Interlocked.Exchange(ref _started, 1) == 0)
            _listener.Start();
    }

    public bool IsClosed => Volatile.Read(ref _closed) != 0;

    public async ValueTask<TcpConnection> AcceptAsync(CancellationToken cancellationToken = default)
    {
        Start();

        var client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
        return TcpConnection.FromAcceptedClient(client);
    }

    public ValueTask CloseAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0)
            return ValueTask.CompletedTask;

        _listener.Stop();
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return CloseAsync();
    }
}
