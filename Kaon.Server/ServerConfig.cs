using Kaon.Config;

namespace Kaon.Server;

[Config("server.config.json")]
public partial class ServerConfig
{
    [ConfigNode("server.host")]
    public string IpAddress { get; set; } = "0.0.0.0";

    [ConfigNode("server.port")]
    public int Port { get; set; } = 7000;

    [ConfigNode("server.max.connections")]
    public int MaxTunnelConnections { get; set; } = 1024;

    [ConfigNode("tunnel.compression.enabled")]
    public bool TunnelCompressionEnabled { get; set; } = true;

    [ConfigNode("tunnel.compression.threshold")]
    public int TunnelCompressionThreshold { get; set; } = 2 * 1024 * 1024;

    [ConfigNode("tunnel.encryption.enabled")]
    public bool TunnelEncryptionEnabled { get; set; }

    [ConfigNode("tunnel.encryption.key")]
    public string TunnelEncryptionKey { get; set; } = string.Empty;
}
