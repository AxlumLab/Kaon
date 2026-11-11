using NetStickShared.Logger;
using NetStickShared.Net.API;
using NetStickShared.Packets;

namespace NetStick;

public class NetClientListener : ISocketListener
{
    private readonly ILogger _logger = LogManager.GetLogger(typeof(NetClientListener));
    
    public override void OnPacketReceived(IPacket packet)
    {
        _logger.Debug("OnPacketReceived");
        _logger.Info($"Id {packet.PacketId}");
        if (packet is STestPacket pkt)
        {
            _logger.Info($"Received STestPacket {pkt.Text} {pkt.State}");
        }
        base.OnPacketReceived(packet);
    }

    public override void OnPacketSent(IPacket packet, IPacket info)
    {
        _logger.Debug($"OnPacketSent {packet.PacketId} {info.GetType().Name}"); //TODO remove in release
        base.OnPacketSent(packet, info);
    }

    public override void OnActive()
    {
        _logger.Info("OnActive");
        base.OnActive();
    }

    public override void OnClose()
    {
        _logger.Info("OnClose");
        base.OnClose();
    }
}