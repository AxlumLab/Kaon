namespace Kaon.Protocol.Transform;

public sealed class NoopFrameTransform : IFrameTransform
{
    public static NoopFrameTransform Instance { get; } = new();

    private NoopFrameTransform()
    {
    }

    public FrameTransformResult Encode(in FrameHeader header, OwnedBuffer? payload)
        => new(header.Flags, payload, header.OriginalLength);

    public FrameTransformResult Decode(in FrameHeader header, OwnedBuffer? payload)
        => new(header.Flags, payload, header.OriginalLength);
}
