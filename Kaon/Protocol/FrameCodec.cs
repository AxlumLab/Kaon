using System.Buffers;
using Kaon.Logger;
using Kaon.Transport;

namespace Kaon.Protocol;

public sealed class FrameCodecOptions
{
    public static FrameCodecOptions Default { get; } = new();

    public int MaxPayloadLength { get; init; } = 16 * 1024 * 1024;
}

public sealed class FrameCodec(FrameTransformPipeline? pipeline = null, FrameCodecOptions? options = null)
{
    public static FrameCodec Default { get; } = new();

    private readonly FrameTransformPipeline _pipeline = pipeline ?? FrameTransformPipeline.Empty;
    private readonly FrameCodecOptions _options = options ?? FrameCodecOptions.Default;
    private readonly ArrayPool<byte> _pool = ArrayPool<byte>.Shared;
    private readonly ILogger _logger = LogManager.GetLogger();

    public async ValueTask WriteAsync(Stream stream, Frame frame, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(frame);
        ValidatePayloadLength(frame.PayloadLength);

        var header = new FrameHeader(frame.Type, frame.StreamId, frame.PayloadLength, frame.Flags, originalLength: frame.OriginalLength, bridgeId: frame.BridgeId, direction: frame.Direction, streamOffset: frame.StreamOffset);
        ValidateHeader(header);

        using var transformed = _pipeline.Encode(frame);
        ValidatePayloadLength(transformed.PayloadLength);

        header = new FrameHeader(transformed.Type, transformed.StreamId, transformed.PayloadLength, transformed.Flags, originalLength: transformed.OriginalLength, bridgeId: transformed.BridgeId, direction: transformed.Direction, streamOffset: transformed.StreamOffset);

        var headerBuffer = _pool.Rent(FrameHeader.Size);
        try
        {
            header.WriteTo(headerBuffer);

            await StreamUtil.WriteAllAsync(stream, headerBuffer.AsMemory(0, FrameHeader.Size), cancellationToken).ConfigureAwait(false);
            if (transformed.PayloadLength > 0)
                await StreamUtil.WriteAllAsync(stream, transformed.Payload, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _pool.Return(headerBuffer);
        }
    }

    public async ValueTask<Frame> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var headerBuffer = _pool.Rent(FrameHeader.Size); //new byte[];
        OwnedBuffer? payload = null;

        try
        {
            await StreamUtil.ReadExactAsync(stream, headerBuffer.AsMemory(0, FrameHeader.Size), cancellationToken).ConfigureAwait(false);

            var header = FrameHeader.ReadFrom(headerBuffer.AsSpan(0, FrameHeader.Size));
            ValidateHeader(header);
            ValidatePayloadLength(header.Length);

            if (header.Length > 0)
            {
                payload = new OwnedBuffer(MemoryPool<byte>.Shared.Rent(header.Length));
                await StreamUtil.ReadExactAsync(
                    stream,
                    payload.WritableMemory[..header.Length],
                    cancellationToken).ConfigureAwait(false);
                payload.SetLength(header.Length);
            }

            var rawFrame = new Frame(header.Type, header.StreamId, payload, header.Flags, header.OriginalLength, header.BridgeId, header.Direction, header.StreamOffset);
            payload = null;

            var decoded = _pipeline.Decode(rawFrame);
            try
            {
                ValidatePayloadLength(decoded.PayloadLength);
                return decoded;
            }
            catch
            {
                decoded.Dispose();
                throw;
            }
        }
        finally
        {
            payload?.Dispose();
            _pool.Return(headerBuffer);
        }
    }

    private void ValidatePayloadLength(int length)
    {
        if (length < 0 || length > _options.MaxPayloadLength)
            throw new InvalidDataException($"Frame payload too large: {length}");
    }

    private static void ValidateHeader(in FrameHeader header)
    {
        if (!Enum.IsDefined(header.Type))
            throw new InvalidDataException($"Unsupported frame type: {header.Type}");

        const FrameFlags knownFlags =
            FrameFlags.Compressed |
            FrameFlags.Encrypted |
            FrameFlags.HasChecksum |
            FrameFlags.EndOfStream;

        if ((header.Flags & ~knownFlags) != 0)
            throw new InvalidDataException($"Unsupported frame flags: {header.Flags}");

        if (!Enum.IsDefined(header.Direction))
            throw new InvalidDataException($"Unsupported frame direction: {header.Direction}");

        if (header.Type is FrameType.Ping or FrameType.Pong && header.StreamId != 0)
            throw new InvalidDataException($"{header.Type} frame must use stream id 0.");
    }
}
