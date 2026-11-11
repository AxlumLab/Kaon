using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace Kaon.IO;

public sealed partial class PackedStream
{
    public void WriteNullableString(string? value)
    {
        if (value is not null)
            WriteVarString(value);
    }

    public new void WriteByte(byte value)
    {
        _stream.WriteByte(value);
    }

    public void WriteSByte(sbyte value)
    {
        _stream.WriteByte(unchecked((byte)value));
    }

    public void WriteBoolean(bool value)
    {
        _stream.WriteByte(value ? (byte)1 : (byte)0);
    }

    public void WriteShort(short value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(short)];
        BinaryPrimitives.WriteInt16LittleEndian(buffer, value);
        _stream.Write(buffer);
    }

    public void WriteUShort(ushort value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(ushort)];
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, value);
        _stream.Write(buffer);
    }

    public void WriteInt32(int value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
        _stream.Write(buffer);
    }

    public void WriteInt64(long value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(buffer, value);
        _stream.Write(buffer);
    }

    public void WriteFloat(float value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(float)];
        BinaryPrimitives.WriteSingleLittleEndian(buffer, value);
        _stream.Write(buffer);
    }

    public void WriteDouble(double value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(double)];
        BinaryPrimitives.WriteDoubleLittleEndian(buffer, value);
        _stream.Write(buffer);
    }

    public void WriteSignedInt32(int value)
        => WriteInt32(value);

    public void WriteSignedInt64(long value)
        => WriteInt64(value);

    public void WriteBytes(byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _stream.Write(value);
    }

    public void WriteBytes(ReadOnlySpan<byte> value)
    {
        _stream.Write(value);
    }

    public void WriteString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        WriteUtf8String(value, useVarLength: false);
    }

    public void WriteVarString(string? value)
    {
        WriteUtf8String(value ?? "[null]", useVarLength: true);
    }

    public void WriteVarInt(int value)
    {
        var unsigned = (uint)value;
        Span<byte> buffer = stackalloc byte[5];
        var index = 0;

        do
        {
            var current = (byte)(unsigned & 0x7F);
            unsigned >>= 7;
            if (unsigned != 0)
                current |= 0x80;

            buffer[index++] = current;
        } while (unsigned != 0);

        _stream.Write(buffer[..index]);
    }

    public void WriteVarLong(long value)
    {
        var unsigned = (ulong)value;
        Span<byte> buffer = stackalloc byte[10];
        var index = 0;

        do
        {
            var current = (byte)(unsigned & 0x7F);
            unsigned >>= 7;
            if (unsigned != 0)
                current |= 0x80;

            buffer[index++] = current;
        } while (unsigned != 0);

        _stream.Write(buffer[..index]);
    }

    public void WriteComponent(IStreamable component)
        => component.OnSerialize(this);

    public void WriteComponent<T>(T ins) where T : IStreamable
        => ins.OnSerialize(this);

    private void WriteUtf8String(string value, bool useVarLength)
    {
        var byteCount = Encoding.UTF8.GetByteCount(value);
        if (useVarLength)
            WriteVarInt(byteCount);
        else
            WriteInt32(byteCount);

        if (byteCount == 0)
            return;

        if (byteCount <= StackBufferLimit)
        {
            Span<byte> buffer = stackalloc byte[byteCount];
            Encoding.UTF8.GetBytes(value, buffer);
            _stream.Write(buffer);
            return;
        }

        var rented = ArrayPool<byte>.Shared.Rent(byteCount);
        try
        {
            var buffer = rented.AsSpan(0, byteCount);
            Encoding.UTF8.GetBytes(value, buffer);
            _stream.Write(buffer);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}
