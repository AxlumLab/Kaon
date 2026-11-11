using System.IO.Compression;
using System.Buffers;

namespace Kaon.Protocol.Transform;

public sealed class CompressionFrameTransform(
    int minPayloadLength = 1024,
    CompressionLevel level = CompressionLevel.Fastest,
    bool encodeEnabled = true)
    : IFrameTransform
{
    private int _encodeEnabled = encodeEnabled ? 1 : 0;
    private int _minPayloadLength = Math.Max(0, minPayloadLength);

    /// <summary>
    /// 是否在发送端启用压缩。接收端不受这个开关影响，只要 Header 带 Compressed 位就会解压。
    /// </summary>
    public bool EncodeEnabled
    {
        get => Volatile.Read(ref _encodeEnabled) != 0;
        set => Volatile.Write(ref _encodeEnabled, value ? 1 : 0);
    }

    /// <summary>
    /// 小包压缩收益低，还会增加 CPU 和延迟；低于该阈值直接原样发送。
    /// </summary>
    public int MinPayloadLength
    {
        get => Volatile.Read(ref _minPayloadLength);
        set => Volatile.Write(ref _minPayloadLength, Math.Max(0, value));
    }

    public FrameTransformResult Encode(in FrameHeader header, OwnedBuffer? payload)
    {
        var input = payload?.Memory ?? ReadOnlyMemory<byte>.Empty;
        if (!EncodeEnabled ||
            input.Length < MinPayloadLength ||
            (header.Flags & FrameFlags.Compressed) != 0)
            return new FrameTransformResult(header.Flags, payload, header.OriginalLength);

        var output = new OwnedBuffer(MemoryPool<byte>.Shared.Rent(input.Length));
        try
        {
            if (!BrotliEncoder.TryCompress(input.Span, output.WritableMemory.Span[..input.Length], out var written, GetQuality(level), window: 22) ||
                written >= input.Length)
            {
                output.Dispose();
                return new FrameTransformResult(header.Flags, payload, header.OriginalLength);
            }

            output.SetLength(written);
            return new FrameTransformResult(header.Flags | FrameFlags.Compressed, output, input.Length);
        }
        catch
        {
            output.Dispose();
            throw;
        }
    }

    public FrameTransformResult Decode(in FrameHeader header, OwnedBuffer? payload)
    {
        var input = payload?.Memory ?? ReadOnlyMemory<byte>.Empty;
        if ((header.Flags & FrameFlags.Compressed) == 0)
            return new FrameTransformResult(header.Flags, payload, header.OriginalLength);

        if (header.OriginalLength <= 0)
            throw new InvalidDataException("Compressed frame is missing OriginalLength.");
        if (header.OriginalLength > 16 * 1024 * 1024)
            throw new InvalidDataException($"Compressed frame original length too large: {header.OriginalLength}");
        if (header.OriginalLength > input.Length * 128)
            throw new InvalidDataException("Compressed frame ratio is too large.");

        var output = new OwnedBuffer(MemoryPool<byte>.Shared.Rent(header.OriginalLength));
        try
        {
            if (!BrotliDecoder.TryDecompress(input.Span, output.WritableMemory.Span[..header.OriginalLength], out var written) ||
                written != header.OriginalLength)
                throw new InvalidDataException("Compressed frame decoded length mismatch.");

            output.SetLength(written);
            return new FrameTransformResult(header.Flags & ~FrameFlags.Compressed, output);
        }
        catch
        {
            output.Dispose();
            throw;
        }
    }

    private static int GetQuality(CompressionLevel compressionLevel)
    {
        return compressionLevel switch
        {
            CompressionLevel.NoCompression => 0,
            CompressionLevel.Fastest => 1,
            CompressionLevel.Optimal => 5,
            CompressionLevel.SmallestSize => 11,
            _ => 5
        };
    }
}
