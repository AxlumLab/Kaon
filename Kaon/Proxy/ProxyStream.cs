using Kaon.Transport;

namespace Kaon.Proxy;

/*
  普通 TCP Connection
         ↕
     ProxyStream
         ↕
  TunnelConnection
         ↕
  远端 ProxyStream
         ↕
  普通 TCP Connection
 */

public enum ProxyStreamState
{
    Opening,
    Open,
    LocalClosed,
    RemoteClosed,
    Closing,
    Closed,
    Reset,
}

public sealed class ProxyStream(
    ulong streamId,
    IStreamConnection connection) : IAsyncDisposable
{
    private int _state = (int)ProxyStreamState.Opening;

    public ulong StreamId { get; } = streamId;

    public IStreamConnection Connection { get; } = connection ?? throw new ArgumentNullException(nameof(connection));

    public ProxyStreamState State => (ProxyStreamState)Volatile.Read(ref _state);

    public bool IsWritable => State is ProxyStreamState.Open or ProxyStreamState.LocalClosed;

    public void MarkOpen()
    {
        Volatile.Write(ref _state, (int)ProxyStreamState.Open);
    }

    public void MarkLocalClosed()
    {
        Volatile.Write(ref _state, (int)ProxyStreamState.LocalClosed);
    }

    public void MarkRemoteClosed()
    {
        Volatile.Write(ref _state, (int)ProxyStreamState.RemoteClosed);
    }

    public void MarkClosing()
    {
        Volatile.Write(ref _state, (int)ProxyStreamState.Closing);
    }

    public void MarkClosed()
    {
        Volatile.Write(ref _state, (int)ProxyStreamState.Closed);
    }

    public void MarkReset()
    {
        Volatile.Write(ref _state, (int)ProxyStreamState.Reset);
    }

    public ValueTask DisposeAsync()
    {
        MarkClosed();
        return Connection.DisposeAsync();
    }
}
