using System.Buffers;

namespace Kaon.Transport;

public static class StreamUtil
{
    public static async ValueTask ReadExactAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[totalRead..], cancellationToken);
            if (read == 0)
                throw new EndOfStreamException("Remote closed the stream while reading.");
            totalRead += read;
        }
    }

    public static async ValueTask WriteAllAsync(Stream stream, ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await stream.WriteAsync(buffer, cancellationToken);
    }

    public static byte[] RentAndCopy(ReadOnlyMemory<byte> source)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(source.Length);
        source.CopyTo(buffer.AsMemory(0, source.Length));
        return buffer;
    }
}
