using System.Net.Sockets;
using System.Net;

namespace Kaon.Transport;

/// <summary>
/// TCP implementation of <see cref="IStreamConnection"/>.
/// It intentionally exposes only raw byte stream operations; Frame handling is done by TunnelConnection.
/// </summary>
public sealed class TcpConnection : IStreamConnection, IDisposable
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private int _closed;

    internal TcpConnection(TcpClient client)
    {
        _client = client;
        _client.NoDelay = true;
        _stream = client.GetStream();
    }

    public EndPoint? LocalEndPoint => _client.Client.LocalEndPoint;

    public EndPoint? RemoteEndPoint => _client.Client.RemoteEndPoint;

    public bool IsClosed => Volatile.Read(ref _closed) != 0;

    public static async Task<TcpConnection> ConnectAsync(
        string host,
        int port,
        CancellationToken cancellationToken = default)
    {
        return await TcpConnector.Shared.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
    }

    public static TcpConnection FromAcceptedClient(TcpClient client)
    {
        return new TcpConnection(client);
    }

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsClosed, this);
        return await _stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsClosed, this);
        await _stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask ShutdownWriteAsync(CancellationToken cancellationToken = default)
    {
        if (IsClosed)
            return ValueTask.CompletedTask;

        try
        {
            _client.Client.Shutdown(SocketShutdown.Send);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (SocketException)
        {
            // The peer may already be gone; callers still own the connection close path.
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask CloseAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0)
            return ValueTask.CompletedTask;

        _stream.Dispose();
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        CloseAsync().AsTask().GetAwaiter().GetResult();
    }

    public ValueTask DisposeAsync()
    {
        return CloseAsync();
    }
}
