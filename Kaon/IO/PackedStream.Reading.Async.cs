using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace Kaon.IO;

public sealed partial class PackedStream
{
    public async Task<long> ReadVarLongAsync()
    {
        var shift = 0;
        long result = 0;

        for (var i = 0; i < 10; i++)
        {
            var current = await ReadUnsignedByteAsync().ConfigureAwait(false);
            result |= (long)(current & 0x7F) << shift;
            if ((current & 0x80) == 0)
                return result;

            shift += 7;
        }

        throw new InvalidDataException("VarLong is too big.");
    }

    public async Task<byte[]> ReadUInt8ArrayAsync(int length = 0)
    {
        if (length == 0)
            length = await ReadVarIntAsync().ConfigureAwait(false);

        if (length < 0)
            throw new InvalidDataException($"Negative array length: {length}.");

        var result = new byte[length];
        if (length > 0)
            await _stream.ReadExactlyAsync(result).ConfigureAwait(false);

        return result;
    }

    public async Task<byte> ReadUInt8Async()
        => await ReadUnsignedByteAsync().ConfigureAwait(false);

    public async Task<string> ReadStringAsync(int maxLength = 32767)
    {
        var length = await ReadVarIntAsync().ConfigureAwait(false);
        var value = await ReadUtf8StringAsync(length).ConfigureAwait(false);
        if (maxLength > 0 && value.Length > maxLength)
            throw new ArgumentException($"string ({value.Length}) exceeded maximum length ({maxLength})", nameof(maxLength));

        return value;
    }

    public async Task<double> ReadDoubleAsync()
    {
        await _stream.ReadExactlyAsync(_numberBuffer.AsMemory(0, sizeof(double))).ConfigureAwait(false);
        return BinaryPrimitives.ReadDoubleBigEndian(_numberBuffer);
    }

    public async Task<float> ReadFloatAsync()
    {
        await _stream.ReadExactlyAsync(_numberBuffer.AsMemory(0, sizeof(float))).ConfigureAwait(false);
        return BinaryPrimitives.ReadSingleBigEndian(_numberBuffer);
    }

    public async Task<ulong> ReadUnsignedLongAsync()
    {
        await _stream.ReadExactlyAsync(_numberBuffer.AsMemory(0, sizeof(ulong))).ConfigureAwait(false);
        return BinaryPrimitives.ReadUInt64BigEndian(_numberBuffer);
    }

    public async Task<long> ReadLongAsync()
    {
        await _stream.ReadExactlyAsync(_numberBuffer.AsMemory(0, sizeof(long))).ConfigureAwait(false);
        return BinaryPrimitives.ReadInt64BigEndian(_numberBuffer);
    }

    public async Task<int> ReadIntAsync()
    {
        await _stream.ReadExactlyAsync(_numberBuffer.AsMemory(0, sizeof(int))).ConfigureAwait(false);
        return BinaryPrimitives.ReadInt32BigEndian(_numberBuffer);
    }

    public async Task<short> ReadShortAsync()
    {
        await _stream.ReadExactlyAsync(_numberBuffer.AsMemory(0, sizeof(short))).ConfigureAwait(false);
        return BinaryPrimitives.ReadInt16BigEndian(_numberBuffer);
    }

    public async Task<bool> ReadBooleanAsync()
    {
        return await ReadUnsignedByteAsync().ConfigureAwait(false) switch
        {
            0 => false,
            1 => true,
            var value => throw new InvalidDataException($"Boolean byte out of range: {value}.")
        };
    }

    public async Task<byte> ReadUnsignedByteAsync()
    {
        await _stream.ReadExactlyAsync(_singleByteBuffer).ConfigureAwait(false);
        return _singleByteBuffer[0];
    }

    public async Task<sbyte> ReadByteAsync()
        => unchecked((sbyte)await ReadUnsignedByteAsync().ConfigureAwait(false));

    public async Task<int> ReadVarIntAsync()
    {
        var shift = 0;
        var result = 0;

        for (var i = 0; i < 5; i++)
        {
            var current = await ReadUnsignedByteAsync().ConfigureAwait(false);
            result |= (current & 0x7F) << shift;
            if ((current & 0x80) == 0)
                return result;

            shift += 7;
        }

        throw new InvalidDataException("VarInt is too big.");
    }

    private async Task<string> ReadUtf8StringAsync(int length)
    {
        if (length < 0)
            throw new InvalidDataException($"Negative string length: {length}.");

        if (length == 0)
            return string.Empty;

        var rented = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            var memory = rented.AsMemory(0, length);
            await _stream.ReadExactlyAsync(memory).ConfigureAwait(false);
            return Encoding.UTF8.GetString(memory.Span);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}
