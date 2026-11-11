using System.Buffers;

namespace NetStickShared.Net2;

public static class NetShared
{
    internal const int ReceiveBufferSize = 64 * 1024;
    
    internal static readonly MemoryPool<byte> MemPool = MemoryPool<byte>.Shared;
}