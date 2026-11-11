using Kaon.Config;

namespace Kaon.Client;

[Config("client.config.json")]
public partial class ClientConfig
{
    [ConfigNode("client.name")]
    public string ClientName { get; set; } = Environment.MachineName;

    [ConfigNode("connection.ip")]
    public string ServerHost { get; set; } = "127.0.0.1";
    
    [ConfigNode("connection.port")]
    public int ServerPort { get; set; } = 7000;

    [ConfigNode("connection.tunnels")]
    public int TunnelConnections { get; set; } = 1;

    [ConfigNode("connection.tunnels.max")]
    public int MaxTunnelConnections { get; set; } = 4;

    [ConfigNode("connection.tunnels.scale.up.bytes")]
    public long TunnelScaleUpQueuedBytes { get; set; } = 8L * 1024 * 1024;

    [ConfigNode("proxy.service")]
    public string ServiceName { get; set; } = "test";

    [ConfigNode("proxy.target.service")]
    public string TargetServiceName { get; set; } = "test";

    [ConfigNode("proxy.local.host")]
    public string LocalHost { get; set; } = "127.0.0.1";

    [ConfigNode("proxy.local.port")]
    public int LocalPort { get; set; } = 8080;

    [ConfigNode("proxy.local.enabled")]
    public bool LocalEnabled { get; set; }

    [ConfigNode("proxy.listen.enabled")]
    public bool ListenEnabled { get; set; }

    [ConfigNode("proxy.listen.host")]
    public string ListenHost { get; set; } = "127.0.0.1";

    [ConfigNode("proxy.listen.port")]
    public int ListenPort { get; set; } = 9000;

    [ConfigNode("proxy.mode")]
    public string Mode { get; set; } = "entry";

    [ConfigNode("proxy.protocol")]
    public string Protocol { get; set; } = "tcp";

    [ConfigNode("proxy.buffer.size")]
    public int ProxyBufferSize { get; set; } = 64 * 1024;

    [ConfigNode("tunnel.compression.enabled")]
    public bool TunnelCompressionEnabled { get; set; } = true;

    [ConfigNode("tunnel.compression.threshold")]
    public int TunnelCompressionThreshold { get; set; } = 2 * 1024 * 1024;

    [ConfigNode("tunnel.encryption.enabled")]
    public bool TunnelEncryptionEnabled { get; set; }

    [ConfigNode("tunnel.encryption.key")]
    public string TunnelEncryptionKey { get; set; } = string.Empty;
}
