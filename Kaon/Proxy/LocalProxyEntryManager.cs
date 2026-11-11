using System.Buffers;
using System.Net;
using Kaon.Transport;

namespace Kaon.Proxy;

public sealed record LocalProxyEntryOptions(
    ProxyProtocol Protocol,
    string Host,
    int Port,
    int BufferSize);

public delegate ValueTask LocalTcpConnectionHandler(
    IStreamConnection connection,
    CancellationToken cancellationToken);

public delegate ValueTask LocalUdpDatagramHandler(
    UdpConnection listener,
    OwnedDatagram datagram,
    CancellationToken cancellationToken);

public sealed class LocalProxyEntryManager(
    LocalProxyEntryOptions options,
    LocalTcpConnectionHandler tcpHandler,
    LocalUdpDatagramHandler udpHandler) : IAsyncDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private TcpConnectionListener? _tcpListener;
    private UdpConnection? _udpListener;
    private Task? _loopTask;

    public void Start()
    {
        var address = IPAddress.Parse(options.Host);
        if (options.Protocol == ProxyProtocol.Udp)
        {
            _udpListener = UdpConnection.Bind(new IPEndPoint(address, options.Port));
            _loopTask = Task.Run(ReceiveUdpLoopAsync);
            return;
        }

        _tcpListener = new TcpConnectionListener(address, options.Port);
        _tcpListener.Start();
        _loopTask = Task.Run(AcceptTcpLoopAsync);
    }

    private async Task AcceptTcpLoopAsync()
    {
        if (_tcpListener == null)
            return;

        while (!_cts.IsCancellationRequested)
        {
            try
            {
                var connection = await _tcpListener.AcceptAsync(_cts.Token).ConfigureAwait(false);
                _ = Task.Run(() => tcpHandler(connection, _cts.Token).AsTask(), _cts.Token);
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException) when (_cts.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task ReceiveUdpLoopAsync()
    {
        var listener = _udpListener;
        if (listener == null)
            return;

        var bufferSize = Math.Clamp(options.BufferSize, 4 * 1024, 1024 * 1024);
        while (!_cts.IsCancellationRequested)
        {
            Kaon.Protocol.OwnedBuffer? buffer = null;
            try
            {
                buffer = new Kaon.Protocol.OwnedBuffer(MemoryPool<byte>.Shared.Rent(bufferSize));
                var datagram = await listener.ReceiveAsync(buffer.WritableMemory[..bufferSize], _cts.Token).ConfigureAwait(false);
                buffer.SetLength(datagram.Data.Length);
                using var owned = new OwnedDatagram(buffer, datagram.RemoteEndPoint);
                buffer = null;
                await udpHandler(listener, owned, _cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException) when (_cts.IsCancellationRequested)
            {
                break;
            }
            finally
            {
                buffer?.Dispose();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);

        if (_tcpListener != null)
            await _tcpListener.DisposeAsync().ConfigureAwait(false);

        if (_udpListener != null)
            await _udpListener.DisposeAsync().ConfigureAwait(false);

        if (_loopTask != null)
        {
            try { await _loopTask.ConfigureAwait(false); }
            catch (OperationCanceledException) {}
        }

        _cts.Dispose();
    }
}
