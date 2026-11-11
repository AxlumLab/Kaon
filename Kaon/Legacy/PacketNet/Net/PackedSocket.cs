using System.Buffers;
using System.Net.Sockets;
using NetStickShared.IO;
using NetStickShared.Logger;
using NetStickShared.Net.API;
using NetStickShared.Net.Utils;
using NetStickShared.Net.Utils.Encryption;

namespace NetStickShared.Net;

internal struct DataPacket : IStreamable
{
    public int Magic { get; set; }
    public int HeaderSize { get; set; }
    public byte[] Header { get; set; }
    public byte[] Body { get; set; }

    public const int MAGIC = 1399871571; // "NSSS"
    
    public void OnSerialize(HStream ss)
    {
        ss.WriteInt32(Magic);
        ss.WriteInt32(HeaderSize);
        ss.WriteBytes(Header);
        ss.WriteBytes(Body);
    }

    public void OnDeSerialize(HStream ss)
    {
        // Magic = ss.ReadInt32(); Not read this cause it read by receiving thread
        HeaderSize = ss.ReadInt32();
        Header = ss.ReadBytes(HeaderSize);
        Body = ss.ReadBytes(Body.Length);
    }
}

internal struct DataHeader : IStreamable
{
    public int PacketId { get; set; }
    public long Timestamp { get; set; }
    public long BodyLength { get; set; }
    public bool PacketCatagory { get; set; }
    public byte EncryptionMethod { get; set; }
    public bool IsCompressed { get; set; }
    
    public void OnSerialize(HStream ss)
    {
        ss.WriteInt32(PacketId);
        ss.WriteInt64(Timestamp);
        ss.WriteInt64(BodyLength);
        ss.WriteBoolean(PacketCatagory);
        ss.WriteByte(EncryptionMethod);
        ss.WriteBoolean(IsCompressed);
    }

    public void OnDeSerialize(HStream ss)
    {
        PacketId = ss.ReadInt32();
        Timestamp = ss.ReadInt64();
        BodyLength = ss.ReadInt64();
        PacketCatagory = ss.ReadBoolean();
        EncryptionMethod = ss.ReadByte();
        IsCompressed = ss.ReadBoolean();
    }

    public void Clear()
    {
        PacketId = -1;
        Timestamp = 0;
        BodyLength = 0;
        PacketCatagory = false;
        EncryptionMethod = 0;
        IsCompressed = false;
    }
}

public partial class PackedSocket
{
    internal PackedSocket(Socket socket, ISocketListener manger, PacketRegistry registry)
    {
        _registry = registry;
        _socket = socket;
        _manger = manger;
        _stm = new NetworkStream(_socket);
        _hStream = new HStream(_stm);

        _logger = LogManager.GetLogger(this);
        _packetSendingThread = new Thread(PacketSendingThread)
        {
            Priority = ThreadPriority.BelowNormal,
            IsBackground = true,
            Name = "PacketSendingThread"
        };
        _packetReadingThread = new Thread(PacketReadingThread)
        {
            Priority = ThreadPriority.AboveNormal,
            IsBackground = true,
            Name = "PacketReadingThread"
        };
    }

    public void StartSocket()
    {
        _packetReadingThread.Start();
        _packetSendingThread.Start();
        
        _manger.OnActive();
    }

    //TODO public void SetEncryption(IEncryption e) => _registry.SetEncryption(e);
    // public IEncryption GetEncryption() => _registry.GetEncryption();

    public PackedSocketInfo GetInfo() => new(_socket);
    

    public void StopSocket()
    {
        _requestStop = true;
        try
        {
            _socket.Disconnect(true);
            _socket.Close();
            _socket.Dispose();
        }
        catch(Exception e)
        {
            _logger.Error(e, "Socket stopping");
            //ignored
        }
    }


    ~PackedSocket() => StopSocket();

    private bool _requestStop;

    private readonly ISocketListener _manger;
    private readonly Thread _packetSendingThread;
    private readonly Thread _packetReadingThread;
    private readonly PacketRegistry _registry;
    private readonly Socket _socket;
    private readonly ILogger _logger;
    private readonly ArrayPool<byte> _pool = ArrayPool<byte>.Create();
}