using System.Net;
using KcpSharp;

namespace Kaon.Transport;

public sealed class KcpUdpOptions
{
    public static KcpUdpOptions Default { get; } = new();

    public int ConversationId { get; init; } = 1;

    public int Mtu { get; init; } = 1200;

    public int SendWindow { get; init; } = 128;

    public int ReceiveWindow { get; init; } = 128;

    public int UpdateInterval { get; init; } = 10;

    public bool NoDelay { get; init; } = true;

    public int FastResend { get; init; } = 2;

    public bool DisableCongestionControl { get; init; } = true;

    internal KcpConversationOptions ToConversationOptions()
    {
        return new KcpConversationOptions
        {
            Mtu = Mtu,
            SendWindow = SendWindow,
            ReceiveWindow = ReceiveWindow,
            UpdateInterval = UpdateInterval,
            NoDelay = NoDelay,
            FastResend = FastResend,
            DisableCongestionControl = DisableCongestionControl,
            StreamMode = true
        };
    }
}

/// <summary>
/// Reliable stream connection over UDP powered by KcpSharp.
/// KCP owns packet ordering, acknowledgements, resend, congestion behavior, and stream reassembly.
/// </summary>
public sealed class KcpUdpConnection : IStreamConnection
{
    private readonly UdpConnection _udp;
    private readonly IKcpTransport<KcpConversation> _transport;
    private readonly KcpConversation _conversation;
    private readonly KcpStream _stream;
    private int _closed;

    public KcpUdpConnection(UdpConnection udp, EndPoint remoteEndPoint, KcpUdpOptions? options = null)
    {
        _udp = udp ?? throw new ArgumentNullException(nameof(udp));
        ArgumentNullException.ThrowIfNull(remoteEndPoint);

        var kcpOptions = options ?? KcpUdpOptions.Default;
        _udp.SetRemoteEndPoint(remoteEndPoint);
        _transport = KcpSocketTransport.CreateConversation(
            udp.Socket,
            remoteEndPoint,
            kcpOptions.ConversationId,
            kcpOptions.ToConversationOptions());
        _transport.Start();
        _conversation = _transport.Connection;
        _stream = new KcpStream(_conversation, ownsConversation: false);
    }

    public EndPoint? LocalEndPoint => _udp.LocalEndPoint;

    public EndPoint? RemoteEndPoint => _udp.RemoteEndPoint;

    public bool IsClosed => Volatile.Read(ref _closed) != 0;

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsClosed, this);
        return await _stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsClosed, this);
        await _stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask ShutdownWriteAsync(CancellationToken cancellationToken = default)
    {
        if (IsClosed)
            return;

        await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public ValueTask CloseAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0)
            return ValueTask.CompletedTask;

        _conversation.SetTransportClosed();
        _stream.Dispose();
        if (_transport is IDisposable disposableTransport)
            disposableTransport.Dispose();
        return _udp.CloseAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return CloseAsync();
    }
}
