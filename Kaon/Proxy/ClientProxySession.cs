using Kaon.Packets;
using Kaon.Transport;

namespace Kaon.Proxy;

/*
 查询 service
 连接本地目标
 创建 ProxyStream
 注册到 StreamManager
 返回 OpenStreamResultPacket
 启动 ProxyStreamPump
 */

public sealed class ClientProxySession(
    IConnector<IStreamConnection> connector,
    LocalServiceRegistry services,
    ProxyStreamManager streams,
    ProxyStreamPump pump,
    IProxyControlChannel control)
{
    public void RegisterHandlers(PacketDispatcher dispatcher)
    {
        dispatcher.Register<SOpenStreamPacket>(async (streamId, packet, cancellationToken) =>
        {
            if (packet.Packet is SOpenStreamPacket openStream)
                await HandleOpenStreamAsync(streamId, openStream.ServiceName, cancellationToken);
        });
    }

    public async ValueTask HandleOpenStreamAsync(
        ulong streamId,
        string serviceName,
        CancellationToken cancellationToken)
    {
        if (!services.TryGet(serviceName, out var service) ||
            service is null)
        {
            await control.SendOpenStreamResultAsync(
                streamId,
                success: false,
                $"Service not found: {serviceName}",
                cancellationToken);
            return;
        }

        IStreamConnection connection;

        try
        {
            connection = await connector.ConnectAsync(
                service.LocalEndPoint,
                cancellationToken);
        }
        catch
        {
            await control.SendOpenStreamResultAsync(
                streamId,
                success: false,
                $"Connect local service failed: {serviceName}",
                cancellationToken);
            return;
        }

        var stream = new ProxyStream(streamId, connection);

        if (!streams.TryAdd(stream))
        {
            await connection.DisposeAsync();
            await control.SendOpenStreamResultAsync(
                streamId,
                success: false,
                "Duplicate stream id.",
                cancellationToken);
            return;
        }

        stream.MarkOpen();

        await control.SendOpenStreamResultAsync(
            streamId,
            success: true,
            message: null,
            cancellationToken);

        _ = RunStreamAsync(stream, cancellationToken);
    }

    private async Task RunStreamAsync(
        ProxyStream stream,
        CancellationToken cancellationToken)
    {
        try
        {
            await pump.RunAsync(stream, cancellationToken);
        }
        finally
        {
            await streams.RemoveAsync(stream.StreamId);
        }
    }
}
