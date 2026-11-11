using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace Kaon.IO;

public sealed partial class PackedStream
{
    private const int StackBufferLimit = 1024;

    public new byte ReadByte()
    {
        var value = _stream.ReadByte();
        if (value < 0)
            throw new EndOfStreamException();

        return (byte)value;
    }

    public sbyte ReadSByte() => unchecked((sbyte)ReadByte());

    public byte[] ReadBytes(int length)
    {
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        var buffer = new byte[length];
        _stream.ReadExactly(buffer);
        return buffer;
    }

    public void ReadBytes(ref byte[] buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        _stream.ReadExactly(buffer);
    }

    public bool ReadBoolean()
    {
        return ReadByte() switch
        {
            0 => false,
            1 => true,
            var value => throw new InvalidDataException($"Boolean byte out of range: {value}.")
        };
    }

    public short ReadShort()
    {
        Span<byte> buffer = stackalloc byte[sizeof(short)];
        _stream.ReadExactly(buffer);
        return BinaryPrimitives.ReadInt16LittleEndian(buffer);
    }

    public ushort ReadUShort()
    {
        Span<byte> buffer = stackalloc byte[sizeof(ushort)];
        _stream.ReadExactly(buffer);
        return BinaryPrimitives.ReadUInt16LittleEndian(buffer);
    }

    public int ReadInt32()
    {
        Span<byte> buffer = stackalloc byte[sizeof(int)];
        _stream.ReadExactly(buffer);
        return BinaryPrimitives.ReadInt32LittleEndian(buffer);
    }

    public long ReadInt64()
    {
        Span<byte> buffer = stackalloc byte[sizeof(long)];
        _stream.ReadExactly(buffer);
        return BinaryPrimitives.ReadInt64LittleEndian(buffer);
    }

    public float ReadFloat()
    {
        Span<byte> buffer = stackalloc byte[sizeof(float)];
        _stream.ReadExactly(buffer);
        return BinaryPrimitives.ReadSingleLittleEndian(buffer);
    }

    public double ReadDouble()
    {
        Span<byte> buffer = stackalloc byte[sizeof(double)];
        _stream.ReadExactly(buffer);
        return BinaryPrimitives.ReadDoubleLittleEndian(buffer);
    }

    public string ReadString()
        => ReadUtf8String(ReadInt32());

    public string ReadVarString()
        => ReadUtf8String(ReadVarInt());

    public int ReadVarInt()
    {
        var shift = 0;
        var result = 0;

        for (var i = 0; i < 5; i++)
        {
            var current = ReadByte();
            result |= (current & 0x7F) << shift;
            if ((current & 0x80) == 0)
                return result;

            shift += 7;
        }

        throw new InvalidDataException("VarInt is too big.");
    }

    public long ReadVarLong()
    {
        var shift = 0;
        long result = 0;

        for (var i = 0; i < 10; i++)
        {
            var current = ReadByte();
            result |= (long)(current & 0x7F) << shift;
            if ((current & 0x80) == 0)
                return result;

            shift += 7;
        }

        throw new InvalidDataException("VarLong is too big.");
    }

    public void ReadComponent(ref IStreamable comp)
        => comp.OnDeSerialize(this);

    public IStreamable ReadComponent(IStreamable component)
    {
        component.OnDeSerialize(this);
        return component;
    }

    public T ReadComponent<T>(T ins) where T : IStreamable
    {
        ins.OnDeSerialize(this);
        return ins;
    }

    private string ReadUtf8String(int length)
    {
        if (length < 0)
            throw new InvalidDataException($"Negative string length: {length}.");

        if (length == 0)
            return string.Empty;

        if (length <= StackBufferLimit)
        {
            Span<byte> buffer = stackalloc byte[length];
            _stream.ReadExactly(buffer);
            return Encoding.UTF8.GetString(buffer);
        }

        var rented = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            var buffer = rented.AsSpan(0, length);
            _stream.ReadExactly(buffer);
            return Encoding.UTF8.GetString(buffer);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}
