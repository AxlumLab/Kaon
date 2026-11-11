namespace Kaon.Protocol.Packet;

public static class PacketFactory
{
    public static T Create<T>(Action<T>? configure = null)
        where T : IPacket, new()
    {
        var packet = new T();
        configure?.Invoke(packet);
        return packet;
    }
}