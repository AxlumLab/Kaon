using Kaon.Logger;
using Kaon.App;
using Kaon.Packets;
using Kaon.Protocol.Packet;
using Kaon.Server.Proxy;
using Kaon.Server.Commands;
using Kaon.Command;

namespace Kaon.Server;

[HostingApp]
public class ServerApp : HostingApp
{
    private readonly ILogger _logger = LogManager.GetLogger(typeof(ServerApp));
    private readonly ServerConfig _config;
    public ServerProxyHost? ProxyHost => TryGetService<ServerProxyHost>(out var host) ? host : null;

    public readonly PacketCodec PacketCodec = new(PacketRegistry.Create(new PacketMapper()));

    public ServerApp(string[] args) : base(args)
    {
        _config = UseConfig<ServerConfig>();
        UseCommands(new CommonCommandMapper());
        UseCommands(new ServerCommandMapper());
    }

    public void Run()
    {
        StartServicesAsync().AsTask().GetAwaiter().GetResult();

        _logger.Info("Kaon proxy server started.");
        _logger.Info("Tunnel listening on {0}:{1}.", _config.IpAddress, _config.Port);
        _logger.Info("Input 'exit' to quit.");
        RunApp();
    }
}
