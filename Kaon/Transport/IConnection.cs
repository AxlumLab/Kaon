using System.Net;

namespace Kaon.Transport;

/// <summary>
/// Represents one established transport connection.
/// This layer only owns transport lifetime and endpoint metadata; it does not know Frame or Packet.
/// </summary>
public interface IConnection : IAsyncDisposable
{
    EndPoint? LocalEndPoint { get; }

    EndPoint? RemoteEndPoint { get; }

    bool IsClosed { get; }

    ValueTask CloseAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Reliable byte stream connection, such as TCP or TLS over TCP.
/// Reads and writes are raw bytes; protocol framing belongs to higher layers.
/// </summary>
public interface IStreamConnection : IConnection
{
    ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default);

    ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default);

    ValueTask ShutdownWriteAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// One datagram received from a datagram transport.
/// </summary>
public readonly struct Datagram(ReadOnlyMemory<byte> data, EndPoint remoteEndPoint)
{
    public ReadOnlyMemory<byte> Data { get; } = data;

    public EndPoint RemoteEndPoint { get; } = remoteEndPoint;
}

/// <summary>
/// Datagram transport connection, such as UDP.
/// A datagram has message boundaries and an explicit remote endpoint.
/// </summary>
public interface IDatagramConnection : IConnection
{
    ValueTask<Datagram> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken = default);

    ValueTask SendAsync(ReadOnlyMemory<byte> data, EndPoint remoteEndPoint, CancellationToken cancellationToken = default);
}

/// <summary>
/// Actively creates outbound connections.
/// </summary>
public interface IConnector<TConnection>
    where TConnection : IConnection
{
    ValueTask<TConnection> ConnectAsync(EndPoint remoteEndPoint, CancellationToken cancellationToken = default);
}

/// <summary>
/// Passively accepts inbound connections.
/// </summary>
public interface IListener<TConnection> : IAsyncDisposable
    where TConnection : IConnection
{
    EndPoint LocalEndPoint { get; }

    ValueTask<TConnection> AcceptAsync(CancellationToken cancellationToken = default);

    ValueTask CloseAsync(CancellationToken cancellationToken = default);
}
