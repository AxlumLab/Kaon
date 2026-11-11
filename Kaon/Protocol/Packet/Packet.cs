using Kaon.IO;

namespace Kaon.Protocol.Packet;

public enum PacketSupport
{
    Server = 0,
    Client = 1
}

public interface IPacket
{
    /// <summary>
    /// Packet 在对应 PacketType 下的唯一编号。
    /// </summary>
    int PacketId { get; }

    /// <summary>
    /// Packet 的方向/分类。
    /// 当前用于区分客户端发出的包和服务端发出的包。
    /// </summary>
    PacketType PacketType { get; }

    /// <summary>
    /// Packet Body 的数据结构版本。
    /// </summary>
    byte PacketVersion => 1;

    /// <summary>
    /// 由 Source Generator 自动生成，负责写入 Packet Body。
    /// 不写入 PacketHeader。
    /// </summary>
    void OnWrite(PackedStream stream);

    /// <summary>
    /// 由 Source Generator 自动生成，负责读取 Packet Body。
    /// 不读取 PacketHeader。
    /// </summary>
    void OnRead(PackedStream stream);
}