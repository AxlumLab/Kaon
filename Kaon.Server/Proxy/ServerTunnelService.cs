using Kaon.App;
using Kaon.Proxy;
using Kaon.Transport;

namespace Kaon.Server.Proxy;

[HostingService]
public sealed class ServerTunnelService(ServerConfig config) : IAsyncDisposable
{
    private TunnelServerManager? _manager;

    public int ConnectionCount => _manager?.ConnectionCount ?? 0;

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
        TunnelAcceptedHandler accepted,
        CancellationToken cancellationToken = default)
    {
        _manager = new TunnelServerManager(new TunnelServerOptions(
            config.IpAddress,
            config.Port,
            config.MaxTunnelConnections,
            config.TunnelCompressionEnabled,
            config.TunnelCompressionThreshold,
            config.TunnelEncryptionEnabled,
            config.TunnelEncryptionKey));

        return _manager.StartAsync(accepted, cancellationToken);
    }

    public ValueTask RemoveAsync(TunnelConnection tunnel)
    {
        return _manager?.RemoveAsync(tunnel) ?? tunnel.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_manager != null)
            await _manager.DisposeAsync().ConfigureAwait(false);
    }
}
