using System.Collections.Concurrent;
using Kaon.Protocol.Packet;

namespace Kaon.Proxy;

public delegate ValueTask PacketHandler(
    ulong streamId,
    DecodedPacket packet,
    CancellationToken cancellationToken);

/// <summary>
/// Minimal packet dispatcher for tunnel sessions.
/// It keeps proxy session code independent from PacketCodec and lets app code register packet handlers explicitly.
/// </summary>
public sealed class PacketDispatcher
{
    private readonly ConcurrentDictionary<(PacketType Type, int Id), PacketHandler> _handlers = new();

    public void Register(PacketType type, int packetId, PacketHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _handlers[(type, packetId)] = handler;
    }

    public void Register<TPacket>(PacketHandler handler)
        where TPacket : IPacket, new()
    {
        var packet = new TPacket();
        Register(packet.PacketType, packet.PacketId, handler);
    }

    public ValueTask DispatchAsync(
        ulong streamId,
        DecodedPacket packet,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packet);

        if (_handlers.TryGetValue((packet.Header.Type, packet.Header.PacketId), out var handler))
            return handler(streamId, packet, cancellationToken);

        return ValueTask.CompletedTask;
    }
}
