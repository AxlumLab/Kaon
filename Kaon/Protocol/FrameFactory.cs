using System.Buffers;
using System.Text;
using Kaon.IO;
using Kaon.Protocol.Packet;

namespace Kaon.Protocol;

public sealed class FrameFactory
{
    public static FrameFactory Default { get; } = new();

    public Frame CreatePacket(ReadOnlyMemory<byte> payload, ulong streamId = 0, FrameFlags flags = FrameFlags.None)
        => CopyPacket(payload.Span, streamId, flags);

    public Frame CreateRawData(ulong streamId, ReadOnlyMemory<byte> data, FrameFlags flags = FrameFlags.None)
        => CopyRawData(streamId, data.Span, flags);

    public Frame CreateClose(ulong streamId, ReadOnlySpan<byte> payload = default)
        => Close(streamId, payload);

    public Frame CreateReset(ulong streamId, int errorCode = 0)
        => Reset(streamId, errorCode);

    public Frame CreatePing(ReadOnlySpan<byte> payload = default)
        => Ping(payload);

    public Frame CreatePong(ReadOnlySpan<byte> payload = default)
        => Pong(payload);

    public static Frame Packet(OwnedBuffer payload, ulong streamId = 0, FrameFlags flags = FrameFlags.None)
        => new(FrameType.Packet, streamId, payload, flags);

    public static Frame RawData(ulong streamId, OwnedBuffer payload, FrameFlags flags = FrameFlags.None)
        => new(FrameType.RawData, streamId, payload, flags);

    public static Frame CopyPacket(ReadOnlySpan<byte> payload, ulong streamId = 0, FrameFlags flags = FrameFlags.None)
        => Packet(CopyToOwnedBuffer(payload), streamId, flags);

    public static Frame CopyRawData(ulong streamId, ReadOnlySpan<byte> data, FrameFlags flags = FrameFlags.None)
        => RawData(streamId, CopyToOwnedBuffer(data), flags);

    public static Frame Close(ulong streamId, ReadOnlySpan<byte> payload = default)
        => new(FrameType.Close, streamId, payload.Length == 0 ? null : CopyToOwnedBuffer(payload));

    public static Frame Reset(ulong streamId, int errorCode = 0)
    {
        var buffer = new OwnedBuffer(MemoryPool<byte>.Shared.Rent(sizeof(int)));
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(buffer.WritableMemory.Span[..sizeof(int)], errorCode);
        buffer.SetLength(sizeof(int));
        return new Frame(FrameType.Reset, streamId, buffer);
    }

    public static Frame Ping(ReadOnlySpan<byte> payload = default)
        => new(FrameType.Ping, 0, payload.Length == 0 ? null : CopyToOwnedBuffer(payload));

    public static Frame Pong(ReadOnlySpan<byte> payload = default)
        => new(FrameType.Pong, 0, payload.Length == 0 ? null : CopyToOwnedBuffer(payload));

    public static Frame String(string message, ulong streamId = 0)
        => CopyRawData(streamId, Encoding.UTF8.GetBytes(message));

    internal static OwnedBuffer CopyToOwnedBuffer(ReadOnlySpan<byte> source)
    {
        var owner = MemoryPool<byte>.Shared.Rent(source.Length);
        FrameMetrics.AddPayloadCopy(source.Length);
        var buffer = new OwnedBuffer(owner);
        source.CopyTo(buffer.WritableMemory.Span);
        buffer.SetLength(source.Length);
        return buffer;
    }
}
