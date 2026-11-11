using System.Net;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Kaon.Protocol;

namespace Kaon.Transport;

/// <summary>
/// Kaon protocol wrapper over a reliable byte stream.
/// It owns Frame IO, while the wrapped transport owns TCP/TLS details.
/// </summary>
public sealed class TunnelConnection : IAsyncDisposable
{
    private readonly IStreamConnection _transport;
    private readonly FrameCodec _frameCodec;
    private readonly StreamConnectionStream _stream;
    private readonly Channel<Frame> _sendQueue;
    private readonly CancellationTokenSource _sendCts = new();
    private readonly Task _writerTask;
    private readonly long _maxQueuedBytes;
    private long _queuedBytes;

    public TunnelConnection(
        IStreamConnection transport,
        FrameCodec? frameCodec = null,
        int sendQueueCapacity = 1024,
        long maxQueuedBytes = 64L * 1024 * 1024)
    {
        ArgumentNullException.ThrowIfNull(transport);

        _transport = transport;
        _frameCodec = frameCodec ?? FrameCodec.Default;
        _stream = new StreamConnectionStream(transport);
        _maxQueuedBytes = Math.Max(1024 * 1024, maxQueuedBytes);
        _sendQueue = Channel.CreateBounded<Frame>(new BoundedChannelOptions(sendQueueCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
        _writerTask = Task.Run(() => RunWriterLoopAsync(_sendCts.Token));
    }

    public EndPoint? LocalEndPoint => _transport.LocalEndPoint;

    public EndPoint? RemoteEndPoint => _transport.RemoteEndPoint;

    public bool IsClosed => _transport.IsClosed;

    public long QueuedBytes => Math.Max(0, Volatile.Read(ref _queuedBytes));

    /// <summary>
    /// Enqueues one complete frame for the single tunnel writer. After this call succeeds or fails,
    /// TunnelConnection owns and eventually disposes the frame.
    /// </summary>
    public async ValueTask SendAsync(Frame frame, CancellationToken cancellationToken = default)
    {
        await QueueSendAsync(frame, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask QueueSendAsync(Frame frame, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frame);
        await WaitForByteBudgetAsync(frame.PayloadLength, cancellationToken).ConfigureAwait(false);

        try
        {
            await _sendQueue.Writer.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            ReleaseByteBudget(frame.PayloadLength);
            frame.Dispose();
            throw;
        }
    }

    public Task RunSendQueueAsync(CancellationToken cancellationToken = default)
    {
        return _writerTask.WaitAsync(cancellationToken);
    }

    /// <summary>
    /// Reads one complete frame from the tunnel stream.
    /// Protocol errors are intentionally propagated so the caller can close the connection.
    /// </summary>
    public ValueTask<Frame> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        return _frameCodec.ReadAsync(_stream, cancellationToken);
    }

    public async IAsyncEnumerable<Frame> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
            yield return await ReceiveAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask CloseAsync(CancellationToken cancellationToken = default)
    {
        _sendQueue.Writer.TryComplete();
        await WaitWriterAsync(cancellationToken).ConfigureAwait(false);
        await _transport.CloseAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        _sendQueue.Writer.TryComplete();
        await _sendCts.CancelAsync().ConfigureAwait(false);
        await WaitWriterAsync(CancellationToken.None).ConfigureAwait(false);
        _sendCts.Dispose();
        await _transport.DisposeAsync().ConfigureAwait(false);
    }

    private async Task RunWriterLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var frame in _sendQueue.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                var accountedBytes = frame.PayloadLength;
                try
                {
                    await _frameCodec.WriteAsync(_stream, frame, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    ReleaseByteBudget(accountedBytes);
                    frame.Dispose();
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            DrainSendQueue();
        }
    }

    private async ValueTask WaitWriterAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _writerTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            if (!_transport.IsClosed)
                throw;
        }
    }

    private void DrainSendQueue()
    {
        while (_sendQueue.Reader.TryRead(out var frame))
        {
            ReleaseByteBudget(frame.PayloadLength);
            frame.Dispose();
        }
    }

    private async ValueTask WaitForByteBudgetAsync(int bytes, CancellationToken cancellationToken)
    {
        var required = Math.Max(1, bytes);
        while (true)
        {
            var current = Volatile.Read(ref _queuedBytes);
            if (current + required <= _maxQueuedBytes &&
                Interlocked.CompareExchange(ref _queuedBytes, current + required, current) == current)
                return;

            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
        }
    }

    private void ReleaseByteBudget(int bytes)
    {
        Interlocked.Add(ref _queuedBytes, -Math.Max(1, bytes));
    }
}
