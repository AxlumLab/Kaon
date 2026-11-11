namespace Kaon.Protocol;

public enum FrameType : byte
{
    Null = 0,
    Packet = 1,
    RawData = 2,
    Close = 3,
    Reset = 4,
    Ping = 5,
    Pong = 6,
}


public sealed class Frame : IDisposable
{
    private OwnedBuffer? _payload;

    public Frame(
        FrameType type,
        ulong streamId,
        OwnedBuffer? payload,
        FrameFlags flags = 0,
        int originalLength = 0,
        ulong bridgeId = 0,
        FrameDirection direction = FrameDirection.None,
        ulong streamOffset = 0)
    {
        Type = type;
        StreamId = streamId;
        Flags = flags;
        OriginalLength = originalLength;
        BridgeId = bridgeId;
        Direction = direction;
        StreamOffset = streamOffset;
        _payload = payload;
    }

    public static Frame Empty => new(FrameType.Null, 0, null);

    public FrameType Type { get; }
    public ulong StreamId { get; }
    public FrameFlags Flags { get; }
    public int OriginalLength { get; }
    public ulong BridgeId { get; }
    public FrameDirection Direction { get; }
    public ulong StreamOffset { get; }
    public ReadOnlyMemory<byte> Payload => _payload?.Memory ?? ReadOnlyMemory<byte>.Empty;
    public int PayloadLength => _payload?.Length ?? 0;

    public OwnedBuffer? DetachPayload()
    {
        return Interlocked.Exchange(ref _payload, null);
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _payload, null)?.Dispose();
    }
}
