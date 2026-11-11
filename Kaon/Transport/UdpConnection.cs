using System.Net;
using System.Net.Sockets;

namespace Kaon.Transport;

/// <summary>
/// UDP implementation of IDatagramConnection.
/// UDP is message based and unreliable; reliability is added by ReliableUdpConnection when needed.
/// </summary>
public sealed class UdpConnection : IDatagramConnection
{
    private readonly Socket _socket;
    private int _closed;

    internal UdpConnection(Socket socket, EndPoint? remoteEndPoint = null)
    {
        _socket = socket;
        RemoteEndPoint = remoteEndPoint;
    }

    public EndPoint? LocalEndPoint => _socket.LocalEndPoint;

    public EndPoint? RemoteEndPoint { get; private set; }

    public bool IsClosed => Volatile.Read(ref _closed) != 0;

    internal Socket Socket => _socket;

    public static UdpConnection Bind(IPEndPoint localEndPoint)
    {
        var socket = new Socket(localEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(localEndPoint);
        return new UdpConnection(socket);
    }

    public async ValueTask<Datagram> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsClosed, this);

        EndPoint remote = new IPEndPoint(IPAddress.Any, 0);
        var result = await _socket.ReceiveFromAsync(
            buffer,
            SocketFlags.None,
            remote,
            cancellationToken).ConfigureAwait(false);

        return new Datagram(buffer[..result.ReceivedBytes], result.RemoteEndPoint);
    }

    public async ValueTask SendAsync(
        ReadOnlyMemory<byte> data,
        EndPoint remoteEndPoint,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsClosed, this);
        ArgumentNullException.ThrowIfNull(remoteEndPoint);

        await _socket.SendToAsync(data, SocketFlags.None, remoteEndPoint, cancellationToken).ConfigureAwait(false);
    }

    internal void SetRemoteEndPoint(EndPoint remoteEndPoint)
    {
        RemoteEndPoint ??= remoteEndPoint;
    }

    public ValueTask CloseAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0)
            return ValueTask.CompletedTask;

        _socket.Dispose();
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return CloseAsync();
    }
}
