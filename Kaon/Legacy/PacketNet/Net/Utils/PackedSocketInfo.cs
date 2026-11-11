using System.Net;
using System.Net.Sockets;

namespace NetStickShared.Net.Utils;

public struct PackedSocketInfo
{
    internal PackedSocketInfo(Socket socket) : this()
    {
        Ip = ((IPEndPoint)socket.RemoteEndPoint!).Address.ToString();
        Port = ((IPEndPoint)socket.RemoteEndPoint!).Port;
        LocalPort = ((IPEndPoint)socket.LocalEndPoint!).Port;
        Mode = ProtocolMode.Tcp;
    }

    public string Ip { get; }
    public int Port  { get; }
    public int LocalPort { get; }
    public ProtocolMode Mode { get; }
}