namespace Kaon.Proxy;

public sealed class StreamIdAllocator
{
    private long _nextId;

    public ulong Next()
    {
        return checked((ulong)Interlocked.Increment(ref _nextId));
    }
}