using System.Buffers.Binary;

namespace Kaon.Protocol;

[Flags]
public enum FrameFlags : ushort
{
    None = 0,

    Compressed = 1 << 0,
    Encrypted = 1 << 1,
    HasChecksum = 1 << 2,

    EndOfStream = 1 << 3,
}

public enum FrameDirection : byte
{
    None = 0,
    ConsumerToProvider = 1,
    ProviderToConsumer = 2
}

public readonly struct FrameHeader(
    FrameType type,
    ulong streamId,
    int length,
    FrameFlags flags = 0,
    byte version = FrameHeader.CurrentVersion,
    int originalLength = 0,
    ulong bridgeId = 0,
    FrameDirection direction = FrameDirection.None,
    ulong streamOffset = 0
) {
    public const uint ExpectedMagic = 0x4E53544B; // "NSTK"
    public const byte CurrentVersion = 2;
    public const int Size = 48;

    public byte Version { get; } = version;
    public FrameType Type { get; } = type;
    public FrameFlags Flags { get; } = flags;
    public ulong StreamId { get; } = streamId;
    public int Length { get; } = length;
    public int OriginalLength { get; } = originalLength;
    public ulong BridgeId { get; } = bridgeId;
    public FrameDirection Direction { get; } = direction;
    public ulong StreamOffset { get; } = streamOffset;
    public uint PayloadLength => checked((uint)Length);

    public void WriteTo(Span<byte> buffer)
    {
        if (buffer.Length < Size)
            throw new ArgumentException("Frame header buffer is too small.", nameof(buffer));

        BinaryPrimitives.WriteUInt32BigEndian(buffer[0..4], ExpectedMagic);
        buffer[4] = Version;
        buffer[5] = (byte)Type;
        BinaryPrimitives.WriteUInt16BigEndian(buffer[6..8], (ushort)Flags);
        BinaryPrimitives.WriteUInt64BigEndian(buffer[8..16], StreamId);
        BinaryPrimitives.WriteInt32BigEndian(buffer[16..20], Length);
        BinaryPrimitives.WriteInt32BigEndian(buffer[20..24], OriginalLength);
        BinaryPrimitives.WriteUInt64BigEndian(buffer[24..32], BridgeId);
        buffer[32] = (byte)Direction;
        buffer[33] = 0;
        buffer[34] = 0;
        buffer[35] = 0;
        BinaryPrimitives.WriteUInt64BigEndian(buffer[36..44], StreamOffset);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[44..48], 0);
    }

    public static FrameHeader ReadFrom(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < Size)
            throw new ArgumentException("Frame header buffer is too small.", nameof(buffer));

        var magic = BinaryPrimitives.ReadUInt32BigEndian(buffer[0..4]);
        if (magic != ExpectedMagic)
            throw new InvalidDataException($"Invalid Kaon frame magic: 0x{magic:X8}");

        var version = buffer[4];
        if (version != CurrentVersion)
            throw new InvalidDataException($"Unsupported Kaon frame version: {version}");

        var type = (FrameType)buffer[5];
        var flags = (FrameFlags)BinaryPrimitives.ReadUInt16BigEndian(buffer[6..8]);
        var streamId = BinaryPrimitives.ReadUInt64BigEndian(buffer[8..16]);
        var length = BinaryPrimitives.ReadInt32BigEndian(buffer[16..20]);
        var originalLength = BinaryPrimitives.ReadInt32BigEndian(buffer[20..24]);
        var bridgeId = BinaryPrimitives.ReadUInt64BigEndian(buffer[24..32]);
        var direction = (FrameDirection)buffer[32];
        var streamOffset = BinaryPrimitives.ReadUInt64BigEndian(buffer[36..44]);
        if (length < 0)
            throw new InvalidDataException($"Invalid Kaon frame length: {length}");
        if (originalLength < 0)
            throw new InvalidDataException($"Invalid Kaon original length: {originalLength}");

        return new FrameHeader(type, streamId, length, flags, version, originalLength, bridgeId, direction, streamOffset);
    }
}
