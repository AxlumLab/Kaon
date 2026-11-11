namespace Kaon.Protocol;

public sealed class FrameTransformPipeline(IEnumerable<IFrameTransform> transforms)
{
    public static FrameTransformPipeline Empty { get; } = new([]);

    private readonly IFrameTransform[] _transforms = transforms.ToArray();

    public Frame Encode(Frame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        var header = new FrameHeader(frame.Type, frame.StreamId, frame.PayloadLength, frame.Flags, originalLength: frame.OriginalLength, bridgeId: frame.BridgeId, direction: frame.Direction, streamOffset: frame.StreamOffset);
        var flags = header.Flags;
        var originalLength = header.OriginalLength;
        var current = frame.DetachPayload();

        try
        {
            foreach (var transform in _transforms)
            {
                var currentHeader = new FrameHeader(header.Type, header.StreamId, current?.Length ?? 0, flags, header.Version, originalLength, header.BridgeId, header.Direction, header.StreamOffset);
                var previous = current;
                var result = transform.Encode(in currentHeader, current);
                flags = result.Flags;
                originalLength = result.OriginalLength;
                current = result.Payload;

                if (!ReferenceEquals(previous, current))
                {
                    FrameMetrics.AddTransformReplacement(previous?.Length ?? 0, current?.Length ?? 0);
                    previous?.Dispose();
                }
                else
                {
                    FrameMetrics.AddTransformPassThrough(current?.Length ?? 0);
                }
            }

            return new Frame(header.Type, header.StreamId, current, flags, originalLength, header.BridgeId, header.Direction, header.StreamOffset);
        }
        catch
        {
            current?.Dispose();
            throw;
        }
        finally
        {
            frame.Dispose();
        }
    }

    public Frame Decode(Frame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        var header = new FrameHeader(frame.Type, frame.StreamId, frame.PayloadLength, frame.Flags, originalLength: frame.OriginalLength, bridgeId: frame.BridgeId, direction: frame.Direction, streamOffset: frame.StreamOffset);
        var flags = header.Flags;
        var originalLength = header.OriginalLength;
        var current = frame.DetachPayload();

        try
        {
            for (var i = _transforms.Length - 1; i >= 0; i--)
            {
                var currentHeader = new FrameHeader(header.Type, header.StreamId, current?.Length ?? 0, flags, header.Version, originalLength, header.BridgeId, header.Direction, header.StreamOffset);
                var previous = current;
                var result = _transforms[i].Decode(in currentHeader, current);
                flags = result.Flags;
                originalLength = result.OriginalLength;
                current = result.Payload;

                if (!ReferenceEquals(previous, current))
                {
                    FrameMetrics.AddTransformReplacement(previous?.Length ?? 0, current?.Length ?? 0);
                    previous?.Dispose();
                }
                else
                {
                    FrameMetrics.AddTransformPassThrough(current?.Length ?? 0);
                }
            }

            return new Frame(header.Type, header.StreamId, current, flags, originalLength, header.BridgeId, header.Direction, header.StreamOffset);
        }
        catch
        {
            current?.Dispose();
            throw;
        }
        finally
        {
            frame.Dispose();
        }
    }
}
