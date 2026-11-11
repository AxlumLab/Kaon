using System.Buffers.Binary;

namespace Kaon.Protocol.Transform;

public sealed class ChecksumFrameTransform : IFrameTransform
{
    public FrameTransformResult Encode(in FrameHeader header, OwnedBuffer? payload)
    {
        if ((header.Flags & FrameFlags.HasChecksum) != 0)
            return new FrameTransformResult(header.Flags, payload, header.OriginalLength);

        var input = payload?.Memory ?? ReadOnlyMemory<byte>.Empty;
        if (payload != null && payload.HasCapacity(input.Length + sizeof(uint)))
        {
            BinaryPrimitives.WriteUInt32BigEndian(
                payload.WritableMemory.Span.Slice(input.Length, sizeof(uint)),
                Crc32.Compute(input.Span));
            payload.SetLength(input.Length + sizeof(uint));
            return new FrameTransformResult(header.Flags | FrameFlags.HasChecksum, payload, header.OriginalLength);
        }

        var output = new OwnedBuffer(System.Buffers.MemoryPool<byte>.Shared.Rent(input.Length + sizeof(uint)));
        try
        {
            input.CopyTo(output.WritableMemory);
            BinaryPrimitives.WriteUInt32BigEndian(output.WritableMemory.Span[input.Length..], Crc32.Compute(input.Span));
            output.SetLength(input.Length + sizeof(uint));
            return new FrameTransformResult(header.Flags | FrameFlags.HasChecksum, output, header.OriginalLength);
        }
        catch
        {
            output.Dispose();
            throw;
        }
    }

    public FrameTransformResult Decode(in FrameHeader header, OwnedBuffer? payload)
    {
        if ((header.Flags & FrameFlags.HasChecksum) == 0)
            return new FrameTransformResult(header.Flags, payload, header.OriginalLength);

        var input = payload?.Memory ?? ReadOnlyMemory<byte>.Empty;
        if (input.Length < sizeof(uint))
            throw new InvalidDataException("Checksummed frame payload is too small.");

        var dataLength = input.Length - sizeof(uint);
        var data = input[..dataLength];
        var expected = BinaryPrimitives.ReadUInt32BigEndian(input.Span[dataLength..]);
        var actual = Crc32.Compute(data.Span);
        if (actual != expected)
            throw new InvalidDataException("Frame checksum mismatch.");

        payload?.SetLength(dataLength);
        return new FrameTransformResult(header.Flags & ~FrameFlags.HasChecksum, payload, header.OriginalLength);
    }

    private static class Crc32
    {
        private static readonly uint[] sTable = BuildTable();

        public static uint Compute(ReadOnlySpan<byte> data)
        {
            var crc = 0xFFFFFFFFu;
            foreach (var value in data)
                crc = (crc >> 8) ^ sTable[(crc ^ value) & 0xFF];

            return ~crc;
        }

        private static uint[] BuildTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < table.Length; i++)
            {
                var crc = i;
                for (var j = 0; j < 8; j++)
                    crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
                table[i] = crc;
            }

            return table;
        }
    }
}
