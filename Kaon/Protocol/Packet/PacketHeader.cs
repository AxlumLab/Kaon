using System.Buffers.Binary;

namespace Kaon.Protocol.Packet;

/*
 Packet Payload:
  [packetId:   u16]
  [version:    u16]
  [flags:      u16]
  [reserved:   u16]
  [requestId:  u64]
  [body:       remaining bytes]
  
  PacketId      int32   4 bytes
  PacketType    byte    1 byte
  Version       byte    1 byte
  Flags         ushort  2 bytes
  RequestId     ulong   8 bytes
  -----------------------------
  总计                  16 bytes
 */

/// <summary>
/// Packet 内部头部。
///
/// 一个 FrameType.Packet 的 Payload 结构为：
/// PacketHeader + Packet Body。
/// </summary>
public readonly struct PacketHeader(
    int packetId,
    PacketType packetType,
    byte version,
    PacketFlags flags,
    ulong requestId)
{
    public const int Size =
        sizeof(int) + // PacketId
        sizeof(byte) + // Version
        sizeof(PacketType) + // Packet Type
        sizeof(ushort) + // Flags
        sizeof(ushort) + // Reserved
        sizeof(ulong);   // RequestId

    /// <summary>
    /// Packet 类型编号。
    /// </summary>
    public int PacketId { get; init; } = packetId;

    /// <summary>
    /// Packet 类型
    /// </summary>
    public PacketType Type { get; init; } = packetType;

    /// <summary>
    /// 当前 Packet 数据结构版本。
    /// </summary>
    public byte Version { get; init; } = version;

    /// <summary>
    /// Packet 业务语义标志。
    /// </summary>
    public PacketFlags Flags { get; init; } = flags;

    /// <summary>
    /// 请求和响应关联编号。
    ///
    /// Request、Response、Ack 应使用非零值。
    /// OneWay 通常使用 0。
    /// </summary>
    public ulong RequestId { get; init; } = requestId;
    
    /// <summary>
    /// 将PacketHeader转换为byte数组
    /// </summary>
    /// <param name="destination">目标数组</param>
    /// <exception cref="ArgumentException">当span不可读写时候出现的错误</exception>
    public void WriteTo(Span<byte> destination)
    {
        if (destination.Length < Size)
        {
            throw new ArgumentException(
                "Packet header buffer is too small.",
                nameof(destination));
        }

        BinaryPrimitives.WriteInt32BigEndian(
            destination[..4],
            PacketId);

        destination[4] = (byte)Type;
        destination[5] = Version;

        BinaryPrimitives.WriteUInt16BigEndian(
            destination[6..8],
            (ushort)Flags);

        BinaryPrimitives.WriteUInt64BigEndian(
            destination[8..16],
            RequestId);
    }

    /// <summary>
    /// 从byte数组读取反序列化Packet header
    /// </summary>
    /// <param name="source">源数组</param>
    /// <returns>Packet Header</returns>
    /// <exception cref="ArgumentException">错误不可读写的数组</exception>
    public static PacketHeader ReadFrom(ReadOnlySpan<byte> source)
    {
        if (source.Length < Size)
        {
            throw new ArgumentException(
                "Packet header buffer is too small.",
                nameof(source));
        }

        var packetId =
            BinaryPrimitives.ReadInt32BigEndian(source[0..4]);

        var packetType = (PacketType)source[4];
        var version = source[5];

        var flags = (PacketFlags)
            BinaryPrimitives.ReadUInt16BigEndian(source[6..8]);

        var requestId =
            BinaryPrimitives.ReadUInt64BigEndian(source[8..16]);

        return new PacketHeader(
            packetId,
            packetType,
            version,
            flags,
            requestId);
    }
}