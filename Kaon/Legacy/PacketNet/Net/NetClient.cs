using System;
using System.Net;
using System.Net.Sockets;
using NetStickShared.Logger;
using NetStickShared.Net.API;
using NetStickShared.Net.Utils;

namespace NetStickShared.Net;

public sealed class NetClient
{
    private readonly ILogger _logger;
    
    public NetClient(PacketRegistry registry, string code, ISocketListener manager)
    {
        _logger = LogManager.GetLogger(this);

        try
        {
            var address = ConnectionCode.Deserialize(code);

            var ip = IPAddress.Parse(address.ip);
            var port = address.port;
            _logger.Debug($"Trying to connect {ip}:{port}");

            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _socket.Connect(new IPEndPoint(ip, port));
            _registry = registry;
            _pSocket = new PackedSocket(_socket, manager, _registry);

            manager.OnListenerInit(_pSocket);
        }
        catch (Exception e)
        {
            _logger.Error($"Connect failed : {e.Message}");
#if DEBUG
            throw;
#endif
        }
    }
    
    public NetClient(PacketRegistry registry, string ipD, int port, ISocketListener manager)
    {
        _logger = LogManager.GetLogger(this);
        
        try
        {
            var ip = IPAddress.Parse(ipD);
            _logger.Debug($"Trying to connect {ip}:{port}");

            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _socket.Connect(new IPEndPoint(ip, port));
            _registry = registry;
            _pSocket = new PackedSocket(_socket, manager, _registry);

            manager.OnListenerInit(_pSocket);
        }
        catch (Exception e)
        {
            _logger.Error($"Connect failed : {e.Message}");
#if DEBUG
            throw;
#endif
        }
    }

    public void Start()
    {
        try
        {
            _pSocket.StartSocket();
        }
        catch (Exception e)
        {
            _logger.Error(e, "Failed to start connection");
            Stop();
        }
    }

    public void Stop()
    {
        _pSocket.StopSocket();
        try
        {
            _socket.Disconnect(true);
            _socket.Close();
            _socket.Dispose();
        }
        catch(Exception e)
        {
            _logger.Error(e, "Failed to dispose socket");
            throw new Exception("System internal error");
        }
    }

    public void Dispose()
    {
        Stop();
    }

    ~NetClient() => Dispose();

    private readonly PacketRegistry _registry;
    private readonly Socket _socket;
    private readonly PackedSocket _pSocket;
}