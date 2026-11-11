using Kaon.IO;

namespace Kaon.Protocol.Packet;

public sealed record DecodedPacket(
    PacketHeader Header,
    IPacket Packet);

public sealed class PacketCodecOptions
{
    public static PacketCodecOptions Default { get; } = new();

    /// <summary>
    /// 单个 Packet Body 最大尺寸。
    ///
    /// Packet 通常只是控制消息，不应该承载大量代理数据。
    /// </summary>
    public int MaxBodyLength { get; init; } = 1024 * 1024;

    /// <summary>
    /// 是否要求 OnRead 完整消费 Packet Body。
    ///
    /// 开启后可以发现版本不匹配、生成器遗漏字段等问题。
    /// </summary>
    public bool RequireFullyConsumedBody { get; init; } = true;

    /// <summary>
    /// 是否严格校验解码后的 Packet 元数据与 Header 一致。
    /// </summary>
    public bool ValidatePacketMetadata { get; init; } = true;
}

/// <summary>
/// 负责 PacketHeader 和 Packet Body 的编码、解码。
/// Packet Body 的具体字段序列化由 Source Generator 生成的
/// IPacket.OnWrite / IPacket.OnRead 完成。
/// </summary>
public class PacketCodec(PacketRegistry reg, PacketCodecOptions? options = null)
{
    private const PacketFlags KnownFlags =
        PacketFlags.Request |
        PacketFlags.Response |
        PacketFlags.OneWay |
        PacketFlags.Error |
        PacketFlags.AckRequired |
        PacketFlags.Ack |
        PacketFlags.Idempotent |
        PacketFlags.HighPriority |
        PacketFlags.NoRetry;
    
    private readonly PacketCodecOptions _options = options ?? PacketCodecOptions.Default;

    /// <summary>
    /// 将 IPacket 编码成一个 FrameType.Packet 的 Payload。
    ///
    /// 输出结构：
    /// [PacketHeader][Generated Packet Body]
    /// </summary>
    public byte[] Encode(
        IPacket packet,
        PacketFlags flags,
        ulong requestId = 0)
    {
        ArgumentNullException.ThrowIfNull(packet);

        ValidateMetadata(
            packet.PacketId,
            packet.PacketType,
            packet.PacketVersion);

        ValidateFlags(flags, requestId);

        var header = new PacketHeader(
            packet.PacketId,
            packet.PacketType,
            packet.PacketVersion,
            flags,
            requestId);

        using var output = new PackedStream();

        Span<byte> headerBuffer =
            stackalloc byte[PacketHeader.Size];

        header.WriteTo(headerBuffer);
        output.Write(headerBuffer);

        // 具体 Body 序列化由生成器生成的方法完成。
        packet.OnWrite(output);

        var bodyLength =
            checked((int)output.Length - PacketHeader.Size);

        ValidateBodyLength(bodyLength);

        return output.GetBytes();
    }

    public Kaon.Protocol.OwnedBuffer EncodeOwned(
        IPacket packet,
        PacketFlags flags,
        ulong requestId = 0)
    {
        return Kaon.Protocol.FrameFactory.CopyToOwnedBuffer(Encode(packet, flags, requestId));
    }

    /// <summary>
    /// 从 FrameType.Packet 的 Payload 解码 Packet。
    ///
    /// expectedType 用于限制当前连接允许接收的 Packet 方向：
    /// 服务端通常传 kClient，客户端通常传 kServer。
    /// </summary>
    public DecodedPacket Decode(
        ReadOnlyMemory<byte> payload,
        PacketType? expectedType = null)
    {
        if (payload.Length < PacketHeader.Size)
        {
            throw new InvalidDataException(
                $"Packet payload is too small: {payload.Length} bytes.");
        }

        var header = PacketHeader.ReadFrom(
            payload.Span[..PacketHeader.Size]);

        ValidateMetadata(
            header.PacketId,
            header.Type,
            header.Version);

        ValidateFlags(
            header.Flags,
            header.RequestId);

        if (expectedType.HasValue &&
            header.Type != expectedType.Value)
        {
            throw new InvalidDataException(
                $"Unexpected packet type. " +
                $"Expected={expectedType.Value}, " +
                $"Received={header.Type}.");
        }

        var body = payload[PacketHeader.Size..];

        ValidateBodyLength(body.Length);

        var packet = reg.CreatePacket(
            header.Type,
            header.PacketId);

        if (packet is null)
        {
            throw new InvalidDataException(
                $"Unknown packet. " +
                $"Type={header.Type}, " +
                $"Id={header.PacketId}.");
        }

        if (_options.ValidatePacketMetadata)
        {
            ValidateCreatedPacket(packet, header);
        }

        // PackedStream(ReadOnlySpan<byte>) 内部使用 MemoryStream，
        // 不会直接从 NetworkStream 读取。
        using var bodyStream = new PackedStream(body);

        // 具体 Body 反序列化由生成器生成的方法完成。
        packet.OnRead(bodyStream);

        if (_options.RequireFullyConsumedBody &&
            bodyStream.Position != bodyStream.Length)
        {
            throw new InvalidDataException(
                $"Packet body was not fully consumed. " +
                $"Type={header.Type}, " +
                $"Id={header.PacketId}, " +
                $"Remaining={bodyStream.Length - bodyStream.Position}.");
        }

        return new DecodedPacket(header, packet);
    }

    private void ValidateBodyLength(int bodyLength)
    {
        if (bodyLength < 0 ||
            bodyLength > _options.MaxBodyLength)
        {
            throw new InvalidDataException(
                $"Invalid packet body length: {bodyLength}.");
        }
    }

    private static void ValidateMetadata(
        int packetId,
        PacketType packetType,
        byte version)
    {
        if (packetId < 0)
        {
            throw new InvalidDataException(
                $"Invalid packet id: {packetId}.");
        }

        if (packetType == PacketType.kNone ||
            !Enum.IsDefined(packetType))
        {
            throw new InvalidDataException(
                $"Invalid packet type: {packetType}.");
        }

        if (version == 0)
        {
            throw new InvalidDataException(
                "Packet version cannot be zero.");
        }
    }

    private static void ValidateCreatedPacket(
        IPacket packet,
        in PacketHeader header)
    {
        if (packet.PacketId != header.PacketId)
        {
            throw new InvalidDataException(
                $"Packet registry returned an invalid PacketId. " +
                $"Expected={header.PacketId}, " +
                $"Actual={packet.PacketId}.");
        }

        if (packet.PacketType != header.Type)
        {
            throw new InvalidDataException(
                $"Packet registry returned an invalid PacketType. " +
                $"Expected={header.Type}, " +
                $"Actual={packet.PacketType}.");
        }

        if (packet.PacketVersion != header.Version)
        {
            throw new InvalidDataException(
                $"Unsupported packet version. " +
                $"Type={header.Type}, " +
                $"Id={header.PacketId}, " +
                $"Local={packet.PacketVersion}, " +
                $"Remote={header.Version}.");
        }
    }

    private static void ValidateFlags(
        PacketFlags flags,
        ulong requestId)
    {
        if ((flags & ~KnownFlags) != 0)
        {
            throw new InvalidDataException(
                $"Unsupported packet flags: {flags}.");
        }

        var isRequest =
            (flags & PacketFlags.Request) != 0;

        var isResponse =
            (flags & PacketFlags.Response) != 0;

        var isOneWay =
            (flags & PacketFlags.OneWay) != 0;

        var isError =
            (flags & PacketFlags.Error) != 0;

        var isAck =
            (flags & PacketFlags.Ack) != 0;

        var requiresAck =
            (flags & PacketFlags.AckRequired) != 0;

        if (isRequest && isResponse)
        {
            throw new InvalidDataException(
                "Packet cannot be both Request and Response.");
        }

        if (isOneWay && (isRequest || isResponse))
        {
            throw new InvalidDataException(
                "OneWay packet cannot also be Request or Response.");
        }

        if (isError && !isResponse)
        {
            throw new InvalidDataException(
                "Error flag is only valid on Response packets.");
        }

        if (isAck && requiresAck)
        {
            throw new InvalidDataException(
                "Packet cannot be both Ack and AckRequired.");
        }

        if ((isRequest || isResponse || isAck) &&
            requestId == 0)
        {
            throw new InvalidDataException(
                "Request, Response and Ack packets require " +
                "a non-zero RequestId.");
        }

        if (isOneWay && requestId != 0)
        {
            throw new InvalidDataException(
                "OneWay packets must use RequestId 0.");
        }
    }
}
