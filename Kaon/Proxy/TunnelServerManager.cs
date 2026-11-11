using System.Net;
using Kaon.Protocol;
using Kaon.Protocol.Transform;
using Kaon.Transport;

namespace Kaon.Proxy;

public sealed record TunnelServerOptions(
    string Host,
    int Port,
    int MaxConnections,
    bool CompressionEnabled,
    int CompressionThreshold,
    bool EncryptionEnabled,
    string EncryptionKey);

public delegate ValueTask TunnelAcceptedHandler(
    TunnelConnection tunnel,
    CancellationToken cancellationToken);

public sealed class TunnelServerManager(TunnelServerOptions options) : IAsyncDisposable
{
    private readonly CompressionFrameTransform _compression = new(
        options.CompressionThreshold,
        encodeEnabled: options.CompressionEnabled);
    private readonly List<TunnelConnection> _connections = [];
    private readonly CancellationTokenSource _cts = new();
    private TcpConnectionListener? _listener;
    private Task? _acceptTask;

    public int ConnectionCount
    {
        get
        {
            lock (_connections)
                return _connections.Count;
        }
    }

    public bool CompressionEnabled
    {
        get => _compression.EncodeEnabled;
        set => _compression.EncodeEnabled = value;
    }

    public int CompressionThreshold
    {
        get => _compression.MinPayloadLength;
        set => _compression.MinPayloadLength = value;
    }

    public ValueTask StartAsync(
        TunnelAcceptedHandler accepted,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(accepted);

        var address = IPAddress.Parse(options.Host);
        _listener = new TcpConnectionListener(address, options.Port);
        _listener.Start();
        _acceptTask = Task.Run(() => AcceptLoopAsync(accepted), cancellationToken);
        return ValueTask.CompletedTask;
    }

    private async Task AcceptLoopAsync(TunnelAcceptedHandler accepted)
    {
        if (_listener == null)
            return;

        while (!_cts.IsCancellationRequested)
        {
            try
            {
                var transport = await _listener.AcceptAsync(_cts.Token).ConfigureAwait(false);
                if (ConnectionCount >= options.MaxConnections)
                {
                    await transport.DisposeAsync().ConfigureAwait(false);
                    continue;
                }

                var tunnel = new TunnelConnection(
                    transport,
                    FrameCodecBuilder.CreateTunnelCodec(
                        _compression,
                        options.EncryptionEnabled,
                        options.EncryptionKey));

                lock (_connections)
                    _connections.Add(tunnel);

                _ = Task.Run(() => accepted(tunnel, _cts.Token).AsTask(), _cts.Token);
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException) when (_cts.IsCancellationRequested)
            {
                break;
            }
        }
    }

    public async ValueTask RemoveAsync(TunnelConnection tunnel)
    {
        lock (_connections)
            _connections.Remove(tunnel);

        await tunnel.DisposeAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);

        if (_listener != null)
            await _listener.DisposeAsync().ConfigureAwait(false);

        TunnelConnection[] connections;
        lock (_connections)
        {
            connections = _connections.ToArray();
            _connections.Clear();
        }

        foreach (var connection in connections)
            await connection.DisposeAsync().ConfigureAwait(false);

        if (_acceptTask != null)
        {
            try { await _acceptTask.ConfigureAwait(false); }
            catch (OperationCanceledException) {}
        }

        _cts.Dispose();
    }
}
