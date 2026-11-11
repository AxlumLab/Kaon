using System.Collections.Concurrent;

namespace Kaon.Proxy;

public readonly record struct OpenStreamResult(bool Success, string? Message);

/// <summary>
/// Tracks server-side OpenStream requests until the client reports success or failure.
/// </summary>
public sealed class ProxyOpenStreamTracker
{
    private readonly ConcurrentDictionary<ulong, TaskCompletionSource<OpenStreamResult>> _pending = new();

    public bool TryBegin(ulong streamId)
    {
        return _pending.TryAdd(
            streamId,
            new TaskCompletionSource<OpenStreamResult>(TaskCreationOptions.RunContinuationsAsynchronously));
    }

    public bool TryComplete(ulong streamId, bool success, string? message)
    {
        if (!_pending.TryRemove(streamId, out var pending))
            return false;

        pending.TrySetResult(new OpenStreamResult(success, message));
        return true;
    }

    public async ValueTask<OpenStreamResult> WaitAsync(
        ulong streamId,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (!_pending.TryGetValue(streamId, out var pending))
            return new OpenStreamResult(false, "Open stream request was not registered.");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            return await pending.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _pending.TryRemove(streamId, out _);
            return new OpenStreamResult(false, "Open stream timed out.");
        }
    }
}
