using Kaon.Protocol;
using Kaon.Protocol.Packet;
using Kaon.Transport;

namespace Kaon.Proxy;

public sealed class TunnelSession(
    TunnelConnection connection,
    PacketCodec packetCodec,
    PacketDispatcher packetDispatcher,
    ProxyFrameRouter proxyRouter,
    PacketType? expectedPacketType = null)
{
    public async Task RunAsync(
        CancellationToken cancellationToken)
    {
        await foreach (var frame in connection.ReadAllAsync(
                           cancellationToken))
        {
            switch (frame.Type)
            {
                case FrameType.Packet:
                {
                    using (frame)
                    {
                        var packet = packetCodec.Decode(frame.Payload, expectedPacketType);

                        await packetDispatcher.DispatchAsync(
                            frame.StreamId,
                            packet,
                            cancellationToken);
                    }

                    break;
                }

                case FrameType.RawData:
                case FrameType.Close:
                case FrameType.Reset:
                {
                    await proxyRouter.RouteAsync(
                        frame,
                        cancellationToken);

                    break;
                }

                case FrameType.Ping:
                {
                    using (frame)
                    {
                        await connection.SendAsync(
                            new Frame(FrameType.Pong, 0, frame.DetachPayload()),
                            cancellationToken);
                    }

                    break;
                }

                case FrameType.Pong:
                    frame.Dispose();
                    break;

                default:
                    frame.Dispose();
                    break;
            }
        }
    }
}
