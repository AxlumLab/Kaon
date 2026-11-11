using Kaon.App;
using Kaon.Protocol;
using Kaon.Protocol.Packet;
using Kaon.Proxy;

namespace Kaon.Client.Proxy;

[HostingService]
public sealed class ClientTunnelService(
    ClientConfig config,
    PacketCodec packetCodec) : IAsyncDisposable, IFrameSender
{
    private TunnelClientManager? _manager;

    public int Count => _manager?.Count ?? 0;

    public bool CompressionEnabled
    {
        get => _manager?.CompressionEnabled ?? config.TunnelCompressionEnabled;
        set
        {
            config.TunnelCompressionEnabled = value;
            _manager?.CompressionEnabled = value;
        }
    }

    public int CompressionThreshold
    {
        get => _manager?.CompressionThreshold ?? config.TunnelCompressionThreshold;
        set
        {
            config.TunnelCompressionThreshold = value;
            _manager?.CompressionThreshold = value;
        }
    }

    public ValueTask StartAsync(
        ProxyFrameRouter proxyRouter,
        ConfigureTunnelClientLane configureLane,
        CancellationToken cancellationToken = default)
    {
        _manager = new TunnelClientManager(
            new TunnelClientOptions(
                config.ServerHost,
                config.ServerPort,
                config.TunnelConnections,
                config.MaxTunnelConnections,
                config.TunnelScaleUpQueuedBytes,
                config.TunnelCompressionEnabled,
                config.TunnelCompressionThreshold,
                config.TunnelEncryptionEnabled,
                config.TunnelEncryptionKey,
                config.ProxyBufferSize),
            packetCodec,
            proxyRouter,
            PacketType.kServer);

        return _manager.StartAsync(configureLane, cancellationToken);
    }

    public ProxyTunnelLane? SelectLane()
    {
        return _manager?.SelectLane();
    }

    public bool TryGetLane(int laneIndex, out ProxyTunnelLane? lane)
    {
        if (_manager != null) 
            return _manager.TryGetLane(laneIndex, out lane);
        lane = null;
        return false;

    }

    public ValueTask SendAsync(Frame frame, CancellationToken cancellationToken = default)
    {
        if (_manager == null)
        {
            frame.Dispose();
            throw new InvalidOperationException("Tunnel manager is not started.");
        }

        return _manager.SendAsync(frame, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_manager != null)
            await _manager.DisposeAsync().ConfigureAwait(false);
    }
}
