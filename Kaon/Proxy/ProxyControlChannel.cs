using Kaon.Packets;
using Kaon.Protocol;
using Kaon.Protocol.Packet;
using Kaon.Transport;

namespace Kaon.Proxy;

public interface IProxyControlChannel
{
    ValueTask SendRegisterServiceAsync(
        ProxyServiceDescriptor service,
        CancellationToken cancellationToken = default);

    ValueTask SendListServicesAsync(
        string filter = "",
        CancellationToken cancellationToken = default);

    ValueTask SendServiceListAsync(
        string services,
        CancellationToken cancellationToken = default);

    ValueTask SendOpenStreamAsync(
        ulong streamId,
        string serviceName,
        CancellationToken cancellationToken = default);

    ValueTask SendClientOpenStreamAsync(
        ulong streamId,
        string serviceName,
        CancellationToken cancellationToken = default);

    ValueTask SendOpenStreamResultAsync(
        ulong streamId,
        bool success,
        string? message,
        CancellationToken cancellationToken = default);

    ValueTask SendServerOpenStreamResultAsync(
        ulong streamId,
        bool success,
        string? message,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Encodes proxy control packets into FrameType.Packet frames.
/// Stream specific control messages use Frame.StreamId; the packet body does not duplicate stream id.
/// </summary>
public sealed class ProxyControlChannel(
    TunnelConnection tunnel,
    PacketCodec packetCodec,
    FrameFactory? frameFactory = null) : IProxyControlChannel
{
    private readonly FrameFactory _frameFactory = frameFactory ?? FrameFactory.Default;

    public ValueTask SendRegisterServiceAsync(
        ProxyServiceDescriptor service,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(service);

        var packet = new CRegisterServicePacket
        {
            ClientName = service.ClientName,
            ServiceName = service.ServiceName,
            Protocol = service.Protocol.ToString(),
            LocalEndPoint = service.LocalEndPoint.ToString() ?? string.Empty,
            PublicEndPoint = service.PublicEndPoint?.ToString()
        };

        return SendPacketAsync(0, packet, cancellationToken);
    }

    public ValueTask SendListServicesAsync(
        string filter = "",
        CancellationToken cancellationToken = default)
    {
        return SendPacketAsync(
            0,
            new CListServicesPacket { Filter = filter },
            cancellationToken);
    }

    public ValueTask SendServiceListAsync(
        string services,
        CancellationToken cancellationToken = default)
    {
        return SendPacketAsync(
            0,
            new SServiceListPacket { Services = services },
            cancellationToken);
    }

    public ValueTask SendOpenStreamAsync(
        ulong streamId,
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        var packet = new SOpenStreamPacket
        {
            ServiceName = serviceName
        };

        return SendPacketAsync(streamId, packet, cancellationToken);
    }

    public ValueTask SendClientOpenStreamAsync(
        ulong streamId,
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        var packet = new COpenStreamPacket
        {
            ServiceName = serviceName
        };

        return SendPacketAsync(streamId, packet, cancellationToken);
    }

    public ValueTask SendOpenStreamResultAsync(
        ulong streamId,
        bool success,
        string? message,
        CancellationToken cancellationToken = default)
    {
        var packet = new COpenStreamResultPacket
        {
            Success = success,
            Message = message
        };

        return SendPacketAsync(streamId, packet, cancellationToken);
    }

    public ValueTask SendServerOpenStreamResultAsync(
        ulong streamId,
        bool success,
        string? message,
        CancellationToken cancellationToken = default)
    {
        var packet = new SOpenStreamResultPacket
        {
            Success = success,
            Message = message
        };

        return SendPacketAsync(streamId, packet, cancellationToken);
    }

    private ValueTask SendPacketAsync(
        ulong streamId,
        IPacket packet,
        CancellationToken cancellationToken)
    {
        var payload = packetCodec.EncodeOwned(packet, PacketFlags.OneWay);
        return tunnel.SendAsync(FrameFactory.Packet(payload, streamId), cancellationToken);
    }
}
