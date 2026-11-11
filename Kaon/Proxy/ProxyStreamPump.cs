using System.Buffers;
using Kaon.Protocol;
using Kaon.Transport;

namespace Kaon.Proxy;

public sealed class ProxyStreamPump(
    IFrameSender sender,
    FrameFactory? frameFactory = null,
    int bufferSize = 64 * 1024)
{
    private readonly FrameFactory _frameFactory = frameFactory ?? FrameFactory.Default;
    private readonly int _bufferSize = Math.Clamp(bufferSize, 4 * 1024, 1024 * 1024);

    public async Task RunAsync(
        ProxyStream stream,
        CancellationToken cancellationToken)
    {
        ulong nextOffset = 0;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                OwnedBuffer? buffer = new(MemoryPool<byte>.Shared.Rent(_bufferSize));
                var read = 0;
                try
                {
                    read = await stream.Connection.ReadAsync(
                        buffer.WritableMemory[.._bufferSize],
                        cancellationToken);
                    buffer.SetLength(read);
                }
                catch
                {
                    buffer.Dispose();
                    throw;
                }

                if (read == 0)
                {
                    buffer.Dispose();
                    stream.MarkLocalClosed();

                    await sender.SendAsync(
                        new Frame(FrameType.Close, stream.StreamId, null, streamOffset: nextOffset),
                        cancellationToken);

                    break;
                }

                var offset = nextOffset;
                nextOffset = checked(nextOffset + (ulong)read);
                var ownedFrame = new Frame(
                    FrameType.RawData,
                    stream.StreamId,
                    buffer,
                    streamOffset: offset);
                buffer = null;
                await sender.SendAsync(
                    ownedFrame,
                    cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch
        {
            stream.MarkReset();

            try
            {
                await sender.SendAsync(
                    _frameFactory.CreateReset(stream.StreamId),
                    CancellationToken.None);
            }
            catch
            {
                // The tunnel may already be closed; the local cleanup path still runs in the caller.
            }

            throw;
        }
    }
}
