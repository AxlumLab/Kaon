using System.Net;
using System.Net.Sockets;
using NetStickShared.Logger;
using NetStickShared.Net.API;
using NetStickShared.Net.Exceptions;
using NetStickShared.Net.Utils;

namespace NetStickShared.Net;

public sealed class NetServer
{
    public NetServer(PacketRegistry registry, ProtocolMode mode, IServerListener serverListener, IPAddress address, int port = 16500)
    {
        Mode = mode;
        _serverListener = serverListener;
        TcpListener = new TcpListener(address, port);
        _registry = registry;

        _logger = LogManager.GetLogger(this);
    }

    public void Start()
    {
        TcpListener.Start();
        Status = ServerStatus.Active;
        _serverListener.OnActive();
        _ = Task.Run(ListenerTask);
    }

    private async Task ListenerTask()
    {
        try
        {
            _logger.Debug("Waiting for new connection");
            var clientSocket = await TcpListener.AcceptSocketAsync();
            var manger = _serverListener.PreConnect();
            
            if (manger is null)
                throw new ServerUnknownAction(ServerAction.PRECONNECT, "Empty PacketManger instance");

            var manager = new PackedSocket(clientSocket, manger, _registry);
            manager.StartSocket();
            manger.OnListenerInit(manager);
        }
        catch (Exception e)
        {
            _logger.Warn(e, "On listening connection");
            throw new SocketDealException(Status, e);
        }

        if (_isCallDead)
        {
            Status = ServerStatus.Close;
            _serverListener.OnStop();
            KillListener();
        }
        else
            _ = Task.Run(ListenerTask);
    }

    private void KillListener()
    {
        TcpListener.Stop();
        Status = ServerStatus.Dead;
        _serverListener.OnDead();
            
    }

    public static IPAddress GetPublicIP()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        var ippaddress =  host
            .AddressList
            .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
        return ippaddress ?? IPAddress.None;
    }
    
    public ServerInfo GetInfo() => new(this);
    public void MarkDead() => _isCallDead = true;

    ~NetServer() => KillListener();

    private bool _isCallDead;
    internal ServerStatus Status = ServerStatus.Init;

    private readonly PacketRegistry _registry;
    private readonly IServerListener _serverListener;
    internal readonly TcpListener TcpListener;
    internal readonly ProtocolMode Mode;
    private readonly ILogger _logger;
}

public enum ProtocolMode
{
    Tcp = 0,
    Udp = 1
}

public enum ServerStatus
{
    Init    = 0,
    Active  = 1,
    Close   = 2,
    Dead    = 3
}