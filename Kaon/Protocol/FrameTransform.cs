namespace Kaon.Protocol;

public readonly struct FrameTransformResult(FrameFlags flags, OwnedBuffer? payload, int originalLength = 0)
{
    public FrameFlags Flags { get; } = flags;
    public OwnedBuffer? Payload { get; } = payload;
    public int OriginalLength { get; } = originalLength;
}

public interface IFrameTransform
{
    /// <summary>
    /// Transforms never dispose the input buffer. Returning the same buffer means pass-through;
    /// returning a different buffer means the pipeline owns and disposes the previous input.
    /// If a transform throws after renting an output buffer, that transform must dispose its own output.
    /// The pipeline disposes the current input on exceptions.
    /// </summary>
    FrameTransformResult Encode(
        in FrameHeader header,
        OwnedBuffer? payload);

    /// <inheritdoc cref="Encode"/>
    FrameTransformResult Decode(
        in FrameHeader header,
        OwnedBuffer? payload);
}
