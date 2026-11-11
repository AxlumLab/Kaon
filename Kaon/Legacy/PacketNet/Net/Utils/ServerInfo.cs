using System.Net;

namespace NetStickShared.Net.Utils;

public struct ServerInfo
{
    internal ServerInfo(NetServer utils) : this()
    {
        Port = ((IPEndPoint)utils.TcpListener.LocalEndpoint).Port;
        Status = utils.Status;
        ListenerMode = utils.Mode;
        GC.Collect();
    }

    public int Port { get; }
    public ServerStatus Status { get; }
    public ProtocolMode ListenerMode { get; }
}