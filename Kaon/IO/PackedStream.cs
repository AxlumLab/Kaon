namespace Kaon.IO;

public sealed partial class PackedStream : Stream
{
    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private readonly byte[] _singleByteBuffer = new byte[1];
    private readonly byte[] _numberBuffer = new byte[sizeof(ulong)];
    private readonly byte[] _varNumberBuffer = new byte[10];

    public PackedStream()
    {
        _stream = new MemoryStream();
    }

    public PackedStream(int capacity)
    {
        _stream = new MemoryStream(capacity);
    }

    public PackedStream(ReadOnlySpan<byte> bytes)
    {
        _stream = new MemoryStream(bytes.ToArray());
    }

    public PackedStream(ReadOnlyMemory<byte> bytes)
    {
        _stream = new ReadOnlyMemoryStream(bytes);
    }

    public PackedStream(byte[] bytes)
    {
        _stream = new MemoryStream(bytes);
    }

    public PackedStream(Stream stream, bool leaveOpen = false)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _leaveOpen = leaveOpen;
    }

    public override bool CanRead => _stream.CanRead;
    public override bool CanSeek => _stream.CanSeek;
    public override bool CanWrite => _stream.CanWrite;
    public override long Length => _stream.Length;
    public override long Position
    {
        get => _stream.Position;
        set => _stream.Position = value;
    }

    public override void Flush() => _stream.Flush();
    public override Task FlushAsync(CancellationToken cancellationToken) => _stream.FlushAsync(cancellationToken);
    public override int Read(byte[] buffer, int offset, int count) => _stream.Read(buffer, offset, count);
    public override int Read(Span<byte> buffer) => _stream.Read(buffer);
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        => _stream.ReadAsync(buffer, cancellationToken);
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => _stream.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    public Task<int> ReadAsync(byte[] buffer, CancellationToken cancellationToken = default)
        => _stream.ReadAsync(buffer.AsMemory(), cancellationToken).AsTask();

    public override void Write(byte[] buffer, int offset, int count) => _stream.Write(buffer, offset, count);
    public override void Write(ReadOnlySpan<byte> buffer) => _stream.Write(buffer);
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        => _stream.WriteAsync(buffer, cancellationToken);
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => _stream.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    public Task WriteAsync(byte[] buffer, CancellationToken cancellationToken = default)
        => _stream.WriteAsync(buffer.AsMemory(), cancellationToken).AsTask();

    public override long Seek(long offset, SeekOrigin origin) => _stream.Seek(offset, origin);
    public override void SetLength(long value) => _stream.SetLength(value);

    public byte[] GetBytes()
    {
        if (_stream is MemoryStream memoryStream)
            return memoryStream.ToArray();

        if (!_stream.CanSeek)
            throw new NotSupportedException("The underlying stream must be seekable to copy all bytes.");

        var position = _stream.Position;
        try
        {
            _stream.Position = 0;
            using var copy = new MemoryStream(checked((int)_stream.Length));
            _stream.CopyTo(copy);
            return copy.ToArray();
        }
        finally
        {
            _stream.Position = position;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_leaveOpen)
            _stream.Dispose();

        base.Dispose(disposing);
    }
}

internal sealed class ReadOnlyMemoryStream(ReadOnlyMemory<byte> memory) : Stream
{
    private int _position;

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => memory.Length;
    public override long Position
    {
        get => _position;
        set
        {
            if (value < 0 || value > memory.Length)
                throw new ArgumentOutOfRangeException(nameof(value));
            _position = checked((int)value);
        }
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return Read(buffer.AsSpan(offset, count));
    }

    public override int Read(Span<byte> buffer)
    {
        var available = memory.Length - _position;
        if (available <= 0)
            return 0;

        var read = Math.Min(buffer.Length, available);
        memory.Span.Slice(_position, read).CopyTo(buffer);
        _position += read;
        return read;
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(Read(buffer.Span));
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        var target = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => memory.Length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };
        Position = target;
        return _position;
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }
}
