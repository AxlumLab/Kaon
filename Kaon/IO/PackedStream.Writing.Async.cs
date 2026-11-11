using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace Kaon.IO;

public sealed partial class PackedStream
{
    public async Task WriteVarLongAsync(long value)
    {
        var unsigned = (ulong)value;
        var index = 0;

        do
        {
            var current = (byte)(unsigned & 0x7F);
            unsigned >>= 7;
            if (unsigned != 0)
                current |= 0x80;

            _varNumberBuffer[index++] = current;
        } while (unsigned != 0);

        await _stream.WriteAsync(_varNumberBuffer.AsMemory(0, index)).ConfigureAwait(false);
    }

    public async Task WriteLongArrayAsync(IEnumerable<long> values)
    {
        foreach (var value in values)
            await WriteLongAsync(value).ConfigureAwait(false);
    }

    public async Task WriteLongArrayAsync(IEnumerable<ulong> values)
    {
        foreach (var value in values)
            await WriteLongAsync((long)value).ConfigureAwait(false);
    }

    public Task WriteVarIntAsync(Enum value)
        => WriteVarIntAsync(Convert.ToInt32(value));

    public async Task WriteVarIntAsync(int value)
    {
        var unsigned = (uint)value;
        var index = 0;

        do
        {
            var current = (byte)(unsigned & 0x7F);
            unsigned >>= 7;
            if (unsigned != 0)
                current |= 0x80;

            _varNumberBuffer[index++] = current;
        } while (unsigned != 0);

        await _stream.WriteAsync(_varNumberBuffer.AsMemory(0, index)).ConfigureAwait(false);
    }

    public async Task WriteStringAsync(string value, int maxLength = short.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.Length > maxLength)
            throw new ArgumentException($"string ({value.Length}) exceeded maximum length ({maxLength})", nameof(value));

        var byteCount = Encoding.UTF8.GetByteCount(value);
        await WriteVarIntAsync(byteCount).ConfigureAwait(false);
        if (byteCount == 0)
            return;

        var rented = ArrayPool<byte>.Shared.Rent(byteCount);
        try
        {
            var memory = rented.AsMemory(0, byteCount);
            Encoding.UTF8.GetBytes(value, memory.Span);
            await _stream.WriteAsync(memory).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    public async Task WriteDoubleAsync(double value)
    {
        BinaryPrimitives.WriteDoubleBigEndian(_numberBuffer, value);
        await _stream.WriteAsync(_numberBuffer.AsMemory(0, sizeof(double))).ConfigureAwait(false);
    }

    public async Task WriteFloatAsync(float value)
    {
        BinaryPrimitives.WriteSingleBigEndian(_numberBuffer, value);
        await _stream.WriteAsync(_numberBuffer.AsMemory(0, sizeof(float))).ConfigureAwait(false);
    }

    public async Task WriteLongAsync(long value)
    {
        BinaryPrimitives.WriteInt64BigEndian(_numberBuffer, value);
        await _stream.WriteAsync(_numberBuffer.AsMemory(0, sizeof(long))).ConfigureAwait(false);
    }

    public async Task WriteIntAsync(int value)
    {
        BinaryPrimitives.WriteInt32BigEndian(_numberBuffer, value);
        await _stream.WriteAsync(_numberBuffer.AsMemory(0, sizeof(int))).ConfigureAwait(false);
    }

    public async Task WriteShortAsync(short value)
    {
        BinaryPrimitives.WriteInt16BigEndian(_numberBuffer, value);
        await _stream.WriteAsync(_numberBuffer.AsMemory(0, sizeof(short))).ConfigureAwait(false);
    }

    public async Task WriteUnsignedShortAsync(ushort value)
    {
        BinaryPrimitives.WriteUInt16BigEndian(_numberBuffer, value);
        await _stream.WriteAsync(_numberBuffer.AsMemory(0, sizeof(ushort))).ConfigureAwait(false);
    }

    public Task WriteBooleanAsync(bool value)
        => WriteUnsignedByteAsync(value ? (byte)1 : (byte)0);

    public async Task WriteUnsignedByteAsync(byte value)
    {
        _singleByteBuffer[0] = value;
        await _stream.WriteAsync(_singleByteBuffer).ConfigureAwait(false);
    }

    public Task WriteByteAsync(sbyte value)
        => WriteUnsignedByteAsync(unchecked((byte)value));
}
