using System.Net;
using Kaon.Protocol;

namespace Kaon.Proxy;

public sealed class OwnedDatagram(OwnedBuffer payload, EndPoint remoteEndPoint) : IDisposable
{
    private OwnedBuffer? _payload = payload;

    public ReadOnlyMemory<byte> Data => _payload?.Memory ?? ReadOnlyMemory<byte>.Empty;

    public EndPoint RemoteEndPoint { get; } = remoteEndPoint;

    public OwnedBuffer? DetachPayload()
    {
        return Interlocked.Exchange(ref _payload, null);
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _payload, null)?.Dispose();
    }
}
