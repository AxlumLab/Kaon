using Kaon.Packets;
using Kaon.Transport;

namespace Kaon.Proxy;

public sealed class PublicProxyListener(
    IListener<IStreamConnection> listener,
    StreamIdAllocator streamIds,
    ProxyStreamManager streams,
    ProxyStreamPump pump,
    IProxyControlChannel control,
    ProxyOpenStreamTracker openStreams,
    TimeSpan? openStreamTimeout = null)
{
    private readonly TimeSpan _openStreamTimeout = openStreamTimeout ?? TimeSpan.FromSeconds(10);

    public void RegisterHandlers(PacketDispatcher dispatcher)
    {
        dispatcher.Register<COpenStreamResultPacket>((streamId, packet, cancellationToken) =>
        {
            if (packet.Packet is COpenStreamResultPacket result)
                openStreams.TryComplete(streamId, result.Success, result.Message);

            return ValueTask.CompletedTask;
        });
    }

    public async Task RunAsync(
        string serviceName,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            IStreamConnection connection;

            try
            {
                connection = await listener.AcceptAsync(
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var streamId = streamIds.Next();

            var stream = new ProxyStream(
                streamId,
                connection);

            if (!streams.TryAdd(stream))
            {
                await connection.DisposeAsync();
                continue;
            }

            if (!openStreams.TryBegin(streamId))
            {
                await streams.RemoveAsync(streamId);
                continue;
            }

            await control.SendOpenStreamAsync(
                streamId,
                serviceName,
                cancellationToken);

            _ = WaitAndRunAsync(
                stream,
                cancellationToken);
        }
    }

    private async Task WaitAndRunAsync(
        ProxyStream stream,
        CancellationToken cancellationToken)
    {
        // 等待客户端 OpenStreamResult
        // 成功后 MarkOpen，失败则释放公网侧连接。
        var result = await openStreams.WaitAsync(
            stream.StreamId,
            _openStreamTimeout,
            cancellationToken);

        if (!result.Success)
        {
            await streams.RemoveAsync(stream.StreamId);
            return;
        }

        stream.MarkOpen();

        try
        {
            await pump.RunAsync(
                stream,
                cancellationToken);
        }
        finally
        {
            await streams.RemoveAsync(stream.StreamId);
        }
    }
}
