using NetStickShared.Logger;
using NetStickShared.Net.Utils;

namespace NetStickShared.Net.API;

public abstract class ISocketListener
{
    private PackedSocket? _socket;
    public PackedSocket Socket => _socket ?? throw new Exception("Current listener are not init!");

    public virtual void OnPacketReceived(IPacket packet) {}
    public virtual void OnPacketSent(IPacket packet, IPacket info) {}

    public virtual void OnCollectAdd(PacketManagerList collect, int id) {}
    public virtual void OnCollectDelete(PacketManagerList collect) {}

    public virtual void OnListenerInit(PackedSocket socket) 
        => _socket = socket;
    public virtual void OnActive() {}
    public virtual void OnClose() {}

    public virtual void OnError(Exception e, bool handled = true)
    {
        LogManager.GetLogger().Error(e, "ISocketListener error!");
    }
}