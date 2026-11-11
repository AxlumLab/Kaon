using NetStickShared.IO;

namespace NetStickShared.Net.API;


public enum PacketSupport
{
    Server = 0,
    Client = 1
}

public interface IPacket
{
    public int PacketId { get; }
    public PacketType PacketType { get; }

    public void OnWrite(HStream stream);
    public void OnRead(HStream stream);
}
