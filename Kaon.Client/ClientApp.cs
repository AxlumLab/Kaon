using Kaon.Client.Commands;
using Kaon.Client.Proxy;
using Kaon.App;
using Kaon.Command;
using Kaon.Logger;
using Kaon.Packets;
using Kaon.Protocol.Packet;

namespace Kaon.Client;

[HostingApp]
public class ClientApp : HostingApp
{
    private readonly ILogger _logger = LogManager.GetLogger(typeof(ClientApp));
    private readonly ClientConfig _config;
    public ClientProxyHost? ProxyHost => TryGetService<ClientProxyHost>(out var host) ? host : null;

    public readonly PacketCodec PacketCodec = new(PacketRegistry.Create(new PacketMapper()));

    public ClientApp(string[] args) : base(args)
    {
        _config = UseConfig<ClientConfig>();
        UseCommands(new CommonCommandMapper());
        UseCommands(new ClientCommandMapper());
    }

    public void Run()
    {
        StartServicesAsync().AsTask().GetAwaiter().GetResult();

        _logger.Info("Proxy client started. Input 'exit' to quit.");
        RunApp();
    }

}

[HostingHandler]
public sealed class ClientAppHandler : IHostingAppHandler<ClientApp>
{
    public void PreMain(ClientApp app, string[] args)
    {
        // main 前逻辑
    }

    public void PostMain(ClientApp app, string[] args)
    {
        // main 后逻辑
    }
}
