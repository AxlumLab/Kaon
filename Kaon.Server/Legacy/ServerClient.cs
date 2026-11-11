using NetStickShared.Logger;
using NetStickShared.Net.API;
using NetStickShared.Packets;

namespace NetStickServer;

public class ServerClient : ISocketListener
{
    private readonly ILogger _logger = LogManager.GetLogger();
    
    public override void OnPacketReceived(IPacket packet)
    {
        base.OnPacketReceived(packet);
        _logger.Info(packet.PacketId);
        if (packet is CTestPacket pkt)
        {
            _logger.Info($"Received STestPacket {pkt.Text} {pkt.Number}");
            Socket.SendPacket(new STestPacket
            {
                Text = "我收到了",
                State = true
            });
        }
    }

    public override void OnActive()
    {
        base.OnActive();
        _logger.Debug("OnActive");
    }

    public override void OnClose()
    {
        base.OnClose();
        _logger.Debug("OnClose");
    }
}