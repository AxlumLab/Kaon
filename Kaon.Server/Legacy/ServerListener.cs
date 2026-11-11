using NetStickShared.Logger;
using NetStickShared.Net.API;
using NetStickShared.Net.Utils;

namespace NetStickServer;

public class ServerListener : IServerListener
{
    private readonly ILogger _logger = LogManager.GetLogger();
    
    public ISocketListener PreConnect()
    {
        _logger.Debug("New client connect");
        var ct = new ServerClient();
        _clients.Add(ct);
        return ct;
    }

    public void OnStop()
    {
        foreach (var ct in _clients)
        {
            ct.Socket.StopSocket();
        }
        _clients.Clear();
    }

    public void OnActive()
    {
    }

    public void OnDead()
    {
    }
    
    private readonly List<ServerClient> _clients = [];
}