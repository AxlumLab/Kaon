using System.Collections.Concurrent;
using System.Diagnostics;
using NetStickShared.IO;
using NetStickShared.Net.API;
using NetStickShared.Utils;

namespace NetStickShared.Net;

public partial class PackedSocket
{
    public const int CompressThreshold = 1024;
    
    private void PacketSendingThread()
    {
        while (true)
        {
            if (_requestStop)
                break;
            var data = _packetQueue.TryDequeue(out var packet);
            if (!data)
            {
                _sendingBlock.WaitOne();
                continue;
            }
            if (packet == null)
                continue; // Skip invalid packet
            
            using var stream = new HStream();
            packet.OnWrite(stream);
            //TODO var outputData = GetEncryption().Encrypt(stream.GetBytes());
            
            var buffer = stream.GetBytes();
            var isCompressed = buffer.Length > CompressThreshold;
            if (isCompressed)
                buffer = GzipUtil.CompressBytes(buffer);
        
            using var stm = new HStream();
            var header = new DataHeader
            {
                PacketId = packet.PacketId,
                BodyLength = buffer.Length,
                Timestamp = DateTime.Now.Ticks,
                PacketCatagory = packet.PacketType == PacketType.kClient,
                IsCompressed = isCompressed,
                EncryptionMethod = 0 //TODO
            };
            stm.WriteComponent(header);

            var dataPkt = new DataPacket
            {
                Magic = DataPacket.MAGIC,
                Header = stm.GetBytes(),
                HeaderSize = (int)stm.Length,
                Body = buffer
            };
            using var stm2 = new HStream();
            stm2.WriteComponent(dataPkt);

            var dat = stm2.GetBytes();
            Debug.Assert(_socket.Send(dat) == stm2.Length);
        }
    }
    
    public void SendPacket(IPacket packet)
    {
        _packetQueue.Enqueue(packet);
        _ = _sendingBlock.Set();
    }

    private readonly AutoResetEvent _sendingBlock = new(false);
    private readonly ConcurrentQueue<IPacket> _packetQueue = new();
}