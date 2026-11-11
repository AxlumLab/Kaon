using Kaon.Protocol;
using Kaon.Transport;

namespace Kaon.Proxy;

public interface IFrameSender
{
    ValueTask SendAsync(Frame frame, CancellationToken cancellationToken = default);
}

public sealed class TunnelFrameSender(TunnelConnection tunnel) : IFrameSender
{
    public ValueTask SendAsync(Frame frame, CancellationToken cancellationToken = default)
    {
        return tunnel.SendAsync(frame, cancellationToken);
    }
}
