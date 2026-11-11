using System.Collections.Concurrent;

namespace Kaon.Proxy;

public sealed class ProxyStreamManager : IAsyncDisposable
{
    private readonly ConcurrentDictionary<ulong, ProxyStream> _streams = new();

    public int Count => _streams.Count;

    public bool TryAdd(ProxyStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return _streams.TryAdd(stream.StreamId, stream);
    }

    public bool TryGet(
        ulong streamId,
        out ProxyStream? stream)
    {
        return _streams.TryGetValue(streamId, out stream);
    }

    public async ValueTask<bool> RemoveAsync(ulong streamId)
    {
        if (!_streams.TryRemove(streamId, out var stream))
            return false;

        await stream.DisposeAsync();
        return true;
    }

    public async ValueTask CloseAllAsync()
    {
        var streams = _streams.ToArray();
        _streams.Clear();

        foreach (var pair in streams)
        {
            await pair.Value.DisposeAsync();
        }
    }

    public ValueTask DisposeAsync()
    {
        return CloseAllAsync();
    }
}
