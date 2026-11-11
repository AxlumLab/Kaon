using System.Net;

namespace Kaon.Transport;

/// <summary>
/// Adapts a typed stream connector to the common IStreamConnection connector shape used by proxy sessions.
/// </summary>
public sealed class StreamConnectorAdapter<TConnection>(IConnector<TConnection> inner)
    : IConnector<IStreamConnection>
    where TConnection : IStreamConnection
{
    public async ValueTask<IStreamConnection> ConnectAsync(
        EndPoint remoteEndPoint,
        CancellationToken cancellationToken = default)
    {
        return await inner.ConnectAsync(remoteEndPoint, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Adapts a typed stream listener to the common IStreamConnection listener shape used by proxy listeners.
/// </summary>
public sealed class StreamListenerAdapter<TConnection>(IListener<TConnection> inner)
    : IListener<IStreamConnection>
    where TConnection : IStreamConnection
{
    public EndPoint LocalEndPoint => inner.LocalEndPoint;

    public async ValueTask<IStreamConnection> AcceptAsync(CancellationToken cancellationToken = default)
    {
        return await inner.AcceptAsync(cancellationToken).ConfigureAwait(false);
    }

    public ValueTask CloseAsync(CancellationToken cancellationToken = default)
    {
        return inner.CloseAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return inner.DisposeAsync();
    }
}
