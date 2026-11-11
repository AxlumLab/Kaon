using Kaon.Protocol;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Kaon.Proxy;

/// <summary>
/// 处理 Tunnel 收到的 Frame
/// </summary>
/// <param name="streamManager"></param>
public delegate ValueTask ProxyFrameFallback(
    Frame frame,
    CancellationToken cancellationToken);

public sealed class ProxyFrameRouter(
    ProxyStreamManager streamManager,
    ProxyFrameFallback? fallback = null)
{
    private readonly ConcurrentDictionary<ulong, LocalStreamWriter> _writers = new();

    public async ValueTask RouteAsync(
        Frame frame,
        CancellationToken cancellationToken)
    {
        try
        {
            switch (frame.Type)
            {
                case FrameType.RawData:
                    await HandleRawDataAsync(frame, cancellationToken);
                    break;

                case FrameType.Close:
                    await HandleCloseAsync(frame, cancellationToken);
                    break;

                case FrameType.Reset:
                    await HandleResetAsync(frame, cancellationToken);
                    break;
            }
        }
        finally
        {
            frame.Dispose();
        }
    }

    private async ValueTask HandleRawDataAsync(
        Frame frame,
        CancellationToken cancellationToken)
    {
        if (!streamManager.TryGet(
                frame.StreamId,
                out var stream) ||
            stream is null)
        {
            if (fallback != null)
            {
                await fallback(frame, cancellationToken);
                return;
            }

            throw new InvalidDataException(
                $"Unknown stream: {frame.StreamId}");
        }

        if (!stream.IsWritable)
            return;

        var writer = _writers.GetOrAdd(
            frame.StreamId,
            _ => new LocalStreamWriter(stream, RemoveWriter));

        await writer.EnqueueAsync(
            new Frame(
                frame.Type,
                frame.StreamId,
                frame.DetachPayload(),
                frame.Flags,
                frame.OriginalLength,
                frame.BridgeId,
                frame.Direction,
                frame.StreamOffset),
            cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask HandleCloseAsync(
        Frame frame,
        CancellationToken cancellationToken)
    {
        var streamId = frame.StreamId;
        if (streamManager.TryGet(streamId, out var stream))
        {
            stream?.MarkRemoteClosed();
            if (stream != null)
            {
                var writer = _writers.GetOrAdd(
                    frame.StreamId,
                    _ => new LocalStreamWriter(stream, RemoveWriter));
                await writer.EnqueueAsync(
                    new Frame(
                        frame.Type,
                        frame.StreamId,
                        frame.DetachPayload(),
                        frame.Flags,
                        frame.OriginalLength,
                        frame.BridgeId,
                        frame.Direction,
                        frame.StreamOffset),
                    cancellationToken).ConfigureAwait(false);
                return;
            }
        }

        if (await streamManager.RemoveAsync(streamId))
        {
            if (_writers.TryRemove(streamId, out var writer))
                await writer.DisposeAsync().ConfigureAwait(false);
            return;
        }

        if (fallback != null)
            await fallback(frame, cancellationToken);
    }

    private async ValueTask HandleResetAsync(
        Frame frame,
        CancellationToken cancellationToken)
    {
        var streamId = frame.StreamId;
        if (streamManager.TryGet(streamId, out var stream))
            stream?.MarkReset();

        if (await streamManager.RemoveAsync(streamId))
        {
            if (_writers.TryRemove(streamId, out var writer))
                await writer.DisposeAsync().ConfigureAwait(false);
            return;
        }

        if (fallback != null)
            await fallback(frame, cancellationToken);
    }

    private void RemoveWriter(ulong streamId)
    {
        _writers.TryRemove(streamId, out _);
    }

    private sealed class LocalStreamWriter(
        ProxyStream stream,
        Action<ulong> remove) : IAsyncDisposable
    {
        private readonly Channel<Frame> _queue = Channel.CreateBounded<Frame>(new BoundedChannelOptions(256)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
        private readonly CancellationTokenSource _cts = new();
        private Task? _runTask;
        private readonly SortedDictionary<ulong, Frame> _reorder = new();
        private const int MaxReorderFrames = 512;
        private const long MaxReorderBytes = 8 * 1024 * 1024;
        private ulong _nextExpectedOffset;
        private long _bufferedBytes;

        public async ValueTask EnqueueAsync(Frame frame, CancellationToken cancellationToken)
        {
            EnsureStarted();
            try
            {
                await _queue.Writer.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                frame.Dispose();
                throw;
            }
        }

        private void EnsureStarted()
        {
            if (_runTask != null)
                return;

            var task = RunAsync(_cts.Token);
            if (Interlocked.CompareExchange(ref _runTask, task, null) == null)
                return;
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                await foreach (var frame in _queue.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
                {
                    await AcceptFrameAsync(frame, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            finally
            {
                while (_queue.Reader.TryRead(out var frame))
                    frame.Dispose();
                foreach (var frame in _reorder.Values)
                    frame.Dispose();
                _reorder.Clear();
                remove(stream.StreamId);
            }
        }

        private async ValueTask AcceptFrameAsync(Frame frame, CancellationToken cancellationToken)
        {
            if (frame.StreamOffset < _nextExpectedOffset)
            {
                frame.Dispose();
                return;
            }

            if (frame.StreamOffset == _nextExpectedOffset)
            {
                await WriteFrameAsync(frame, cancellationToken).ConfigureAwait(false);
                await DrainReadyFramesAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            if (_reorder.ContainsKey(frame.StreamOffset))
            {
                frame.Dispose();
                return;
            }

            _bufferedBytes += frame.PayloadLength;
            if (_reorder.Count >= MaxReorderFrames || _bufferedBytes > MaxReorderBytes)
            {
                frame.Dispose();
                stream.MarkReset();
                throw new InvalidDataException($"Reorder buffer exceeded for stream {stream.StreamId}.");
            }

            _reorder.Add(frame.StreamOffset, frame);
        }

        private async ValueTask DrainReadyFramesAsync(CancellationToken cancellationToken)
        {
            while (_reorder.Count > 0)
            {
                var first = _reorder.First();
                if (first.Key != _nextExpectedOffset)
                    return;

                _reorder.Remove(first.Key);
                _bufferedBytes -= first.Value.PayloadLength;
                await WriteFrameAsync(first.Value, cancellationToken).ConfigureAwait(false);
            }
        }

        private async ValueTask WriteFrameAsync(Frame frame, CancellationToken cancellationToken)
        {
            using (frame)
            {
                if (frame.Type == FrameType.Close)
                {
                    await stream.Connection.ShutdownWriteAsync(cancellationToken).ConfigureAwait(false);
                    return;
                }

                await stream.Connection.WriteAsync(frame.Payload, cancellationToken).ConfigureAwait(false);
                _nextExpectedOffset = checked(_nextExpectedOffset + (ulong)frame.PayloadLength);
            }
        }

        public async ValueTask DisposeAsync()
        {
            _queue.Writer.TryComplete();
            await _cts.CancelAsync().ConfigureAwait(false);
            if (_runTask != null)
            {
                try { await _runTask.ConfigureAwait(false); }
                catch (OperationCanceledException) {}
            }
            _cts.Dispose();
        }
    }
}
