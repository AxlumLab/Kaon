using System.Diagnostics;
using System.Net.Sockets;
using NetStickShared.IO;
using NetStickShared.Net.API;
using NetStickShared.Net.Utils.Encryption;
using NetStickShared.Utils;

namespace NetStickShared.Net;

public partial class PackedSocket
{
    private void PacketReadingThread()
    {
        var headerContent = new DataHeader();
        
        while (true)
        {
            if (!_socket.Connected)
            {
                // Close soket on connection closed.
                _manger.OnClose();
                _requestStop = true;
            }

            if (_requestStop)
            {
                _manger.OnClose();
                return;
            }
            
            try
            {
                var iMagic = _hStream.ReadInt32();
                if (iMagic != DataPacket.MAGIC)
                    throw new Exception("Failed to read magic. Invalid data iMagic != MAGIC");

                var headerSize = _hStream.ReadInt32();
                var header = _hStream.ReadBytes(headerSize);

                headerContent.Clear();
                using var stm = new HStream(header);
                headerContent = stm.ReadComponent(headerContent);
                
                var method = (EncryptionMethod)headerContent.EncryptionMethod;
                //TODO Encryption fix

                var body = _hStream.ReadBytes((int)headerContent.BodyLength);
                var content = body;
                if (headerContent.IsCompressed)
                {
                    content = GzipUtil.Decompress(body) ??
                              throw new ArgumentNullException(
                                  $"Decompress stream.readBytes {nameof(headerContent.BodyLength)}");
                    // var buffer = _factory.GetEncryption().DeCrypt(content);
                }

                if (content.Length != 0)
                {
                    using var pktStm = new HStream(content);
                    
                    var pkt = _registry.CreatePacket(headerContent.PacketCatagory ? PacketType.kClient : PacketType.kServer, headerContent.PacketId);
                    if (pkt == null)
                    {
                        _logger.Error($"Invalied packet received Id:{headerContent.PacketId} Type:{headerContent.PacketCatagory}");
                        continue;
                    }
                    pkt.OnRead(pktStm);

                    // TODO sort packet
                    _manger.OnPacketReceived(pkt);
                    _logger.Debug($"Received in {TimeSpan.FromTicks(DateTime.Now.Ticks - headerContent.Timestamp).Microseconds} microseconds");
                }

                // _pool.Return(body);
            }
            catch (Exception e)
            {
                _logger.Warn(e, "Internal error with socket receiving process");
                _manger.OnClose();
                _requestStop = true;
                throw;
            }
        }
    }

    private readonly NetworkStream _stm;
    private readonly HStream _hStream;
}