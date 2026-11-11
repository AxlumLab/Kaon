using System.Buffers;
using System.Buffers.Binary;
using System.Net.Sockets;

namespace NetStickShared.Net2;

public struct NetContext
{
    public Socket Socket { get; init; }
    public SocketAsyncEventArgs RecvArgs { get; set; }
    
    public IMemoryOwner<byte> RecvOwner { get; set; }
    public Memory<byte> RecvMemory { get; set; }

    // 已缓存但未处理的数据长度
    public int Buffered { get; set; }
    // 协议 / 加密状态
    public ProtocolDecoder Decoder { get; set; }
}

public static class NetConnection 
{ 
    internal static NetContext InitConn(Socket socket)
    {
        var ctx = new NetContext
        {
            Socket = socket,
            RecvOwner = NetShared.MemPool.Rent(NetShared.ReceiveBufferSize),
            Buffered = 0,
            Decoder = new ProtocolDecoder()
        };

        ctx.RecvMemory = ctx.RecvOwner.Memory;

        var args = new SocketAsyncEventArgs();
        args.UserToken = ctx;
        args.Completed += OnRecvCompleted;

        ctx.RecvArgs = args;

        StartRecv(ref ctx);
        return ctx;
    }
    
    internal static void OnRecvCompleted(object? sender, SocketAsyncEventArgs e)
    {
        ProcessRecv(e);
    }
    
    internal static void StartRecv(ref NetContext ctx)
    {
        // 把 buffer 剩余空间交给 Socket
        ctx.RecvArgs.SetBuffer(
            ctx.RecvMemory.Slice(ctx.Buffered)
        );

        if (!ctx.Socket.ReceiveAsync(ctx.RecvArgs))
        {
            ProcessRecv(ctx.RecvArgs);
        }
    }
    
    internal static void ProcessRecv(SocketAsyncEventArgs e)
    {
        var ctx = (NetContext)e.UserToken!;

        if (e.BytesTransferred <= 0 ||
            e.SocketError != SocketError.Success)
        {
            Close(ref ctx);
            return;
        }

        // 更新缓冲区有效长度
        ctx.Buffered += e.BytesTransferred;

        // 尝试解析完整包
        ConsumePackets(ref ctx);

        // 继续接收
        StartRecv(ref ctx);
    }

    internal static void ConsumePackets(ref NetContext ctx)
    {
        var buffer = ctx.RecvMemory.Span;
        int offset = 0;

        while (true)
        {
            if (ctx.Buffered - offset < 4)
                break;

            int packetLen = BinaryPrimitives.ReadInt32LittleEndian(
                buffer.Slice(offset, 4)
            );

            if (ctx.Buffered - offset < 4 + packetLen)
                break;

            var packet = ctx.RecvMemory.Slice(
                offset + 4,
                packetLen
            );

            HandlePacket(ctx, packet);

            offset += 4 + packetLen;
        }

        // 把剩余半包移到 buffer 头部
        if (offset > 0)
        {
            buffer
                .Slice(offset, ctx.Buffered - offset)
                .CopyTo(buffer);

            ctx.Buffered -= offset;
        }
    }
    
    internal static void HandlePacket(ref NetContext ctx, ReadOnlyMemory<byte> packet)
    {
        // 1️⃣ 解密（AEAD）
        // 2️⃣ 协议解析
        // 3️⃣ 转发到另一端 Socket
    }

    internal static void Close(ref NetContext ctx)
    {
        try { ctx.Socket.Shutdown(SocketShutdown.Both); } catch {}
        try { ctx.Socket.Close(); } catch {}

        ctx.RecvOwner.Dispose();
    }

}