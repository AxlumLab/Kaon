using Kaon.Protocol;
using Kaon.Protocol.Packet;
using Kaon.Protocol.Transform;
using Kaon.Packets;
using Kaon.Transport;

namespace Kaon.Proxy;

public sealed record TunnelClientOptions(
    string Host,
    int Port,
    int TunnelCount,
    int MaxTunnelCount,
    long ScaleUpQueuedBytes,
    bool CompressionEnabled,
    int CompressionThreshold,
    bool EncryptionEnabled,
    string EncryptionKey,
    int BufferSize);

public sealed class ProxyTunnelLane(
    int index,
    TunnelConnection tunnel,
    IProxyControlChannel control,
    ProxyStreamPump pump,
    Task sessionTask)
{
    public int Index { get; } = index;
    public TunnelConnection Tunnel { get; } = tunnel;
    public IProxyControlChannel Control { get; } = control;
    public ProxyStreamPump Pump { get; } = pump;
    public Task SessionTask { get; } = sessionTask;
}

public sealed class TunnelClientLaneContext(
    int index,
    TunnelConnection tunnel,
    IProxyControlChannel control,
    ProxyStreamPump pump,
    PacketDispatcher dispatcher)
{
    public int Index { get; } = index;
    public TunnelConnection Tunnel { get; } = tunnel;
    public IProxyControlChannel Control { get; } = control;
    public ProxyStreamPump Pump { get; } = pump;
    public PacketDispatcher Dispatcher { get; } = dispatcher;
}

public delegate ValueTask ConfigureTunnelClientLane(
    TunnelClientLaneContext context,
    CancellationToken cancellationToken);

public sealed class TunnelClientManager(
    TunnelClientOptions options,
    PacketCodec packetCodec,
    ProxyFrameRouter proxyRouter,
    PacketType expectedPacketType) : IAsyncDisposable, IFrameSender
{
    private readonly CompressionFrameTransform _compression = new(
        options.CompressionThreshold,
        encodeEnabled: options.CompressionEnabled);
    private readonly List<ProxyTunnelLane> _lanes = [];
    private readonly object _lanesLock = new();
    private readonly CancellationTokenSource _cts = new();
    private string? _sessionId;
    private string? _attachToken;
    private ConfigureTunnelClientLane? _configureLane;
    private Task? _scaleTask;

    public int Count
    {
        get
        {
            lock (_lanesLock)
                return _lanes.Count;
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

    public async ValueTask StartAsync(
        ConfigureTunnelClientLane configureLane,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configureLane);
        _configureLane = configureLane;

        var tunnelCount = Math.Max(1, options.TunnelCount);
        for (var i = 0; i < tunnelCount; i++)
            await StartLaneAsync(i, configureLane, cancellationToken).ConfigureAwait(false);
        _scaleTask = Task.Run(() => RunScaleLoopAsync(_cts.Token), CancellationToken.None);
    }

    public ProxyTunnelLane? SelectLane()
    {
        lock (_lanesLock)
        {
            if (_lanes.Count == 0)
                return null;

            return _lanes
                .Where(static lane => !lane.Tunnel.IsClosed)
                .OrderBy(static lane => lane.Tunnel.QueuedBytes)
                .ThenBy(static lane => lane.Index)
                .FirstOrDefault();
        }
    }

    public bool TryGetLane(int laneIndex, out ProxyTunnelLane? lane)
    {
        lock (_lanesLock)
        {
            lane = _lanes.FirstOrDefault(item => item.Index == laneIndex);
            return lane != null;
        }
    }

    private async ValueTask StartLaneAsync(
        int laneIndex,
        ConfigureTunnelClientLane configureLane,
        CancellationToken cancellationToken)
    {
        var transport = await TcpConnector.Shared.ConnectAsync(
            options.Host,
            options.Port,
            cancellationToken).ConfigureAwait(false);

        var tunnel = new TunnelConnection(
            transport,
            FrameCodecBuilder.CreateTunnelCodec(
                _compression,
                options.EncryptionEnabled,
                options.EncryptionKey));
        var control = new ProxyControlChannel(tunnel, packetCodec);
        var pump = new ProxyStreamPump(this, bufferSize: options.BufferSize);
        var dispatcher = new PacketDispatcher();

        if (laneIndex == 0)
        {
            await SendSessionHelloAsync(tunnel, cancellationToken).ConfigureAwait(false);
            await configureLane(
                new TunnelClientLaneContext(laneIndex, tunnel, control, pump, dispatcher),
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await AttachTunnelAsync(tunnel, cancellationToken).ConfigureAwait(false);
        }

        var session = new TunnelSession(
            tunnel,
            packetCodec,
            dispatcher,
            proxyRouter,
            expectedPacketType);
        var task = Task.Run(() => RunSessionAsync(laneIndex, session, cancellationToken), cancellationToken);
        lock (_lanesLock)
            _lanes.Add(new ProxyTunnelLane(laneIndex, tunnel, control, pump, task));
    }

    private async Task RunScaleLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
                ProxyTunnelLane[] lanes;
                lock (_lanesLock)
                    lanes = _lanes.ToArray();

                var max = Math.Max(options.TunnelCount, options.MaxTunnelCount);
                var queuedBytes = lanes.Sum(static lane => lane.Tunnel.QueuedBytes);
                if (queuedBytes == 0 && lanes.Length > Math.Max(1, options.TunnelCount))
                {
                    var candidate = lanes
                        .Where(static lane => lane.Index != 0)
                        .OrderByDescending(static lane => lane.Index)
                        .FirstOrDefault();
                    if (candidate != null)
                    {
                        lock (_lanesLock)
                            _lanes.Remove(candidate);
                        await candidate.Tunnel.CloseAsync(cancellationToken).ConfigureAwait(false);
                    }

                    continue;
                }

                if (lanes.Length < max && queuedBytes >= options.ScaleUpQueuedBytes)
                {
                    var laneIndex = lanes.Length == 0 ? 0 : lanes.Max(static lane => lane.Index) + 1;
                    if (_configureLane != null)
                        await StartLaneAsync(laneIndex, _configureLane, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
            }
        }
    }

    private async ValueTask SendSessionHelloAsync(
        TunnelConnection tunnel,
        CancellationToken cancellationToken)
    {
        await tunnel.SendAsync(
            FrameFactory.Packet(
                packetCodec.EncodeOwned(
                    new CSessionHelloPacket
                    {
                        ClientName = Environment.MachineName,
                        ClientInstanceId = Environment.MachineName,
                        Capabilities = "session-v1,attach-v1"
                    },
                    PacketFlags.OneWay)),
            cancellationToken).ConfigureAwait(false);

        using var response = await tunnel.ReceiveAsync(cancellationToken).ConfigureAwait(false);
        var decoded = packetCodec.Decode(response.Payload, PacketType.kServer);
        if (decoded.Packet is not SSessionAcceptedPacket accepted ||
            string.IsNullOrWhiteSpace(accepted.SessionId) ||
            string.IsNullOrWhiteSpace(accepted.AttachToken))
            throw new InvalidDataException("Server did not accept the session.");

        _sessionId = accepted.SessionId;
        _attachToken = accepted.AttachToken;
    }

    private async ValueTask AttachTunnelAsync(
        TunnelConnection tunnel,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_sessionId) || string.IsNullOrWhiteSpace(_attachToken))
            throw new InvalidOperationException("Cannot attach a tunnel before session hello completes.");

        await tunnel.SendAsync(
            FrameFactory.Packet(
                packetCodec.EncodeOwned(
                    new CAttachTunnelPacket
                    {
                        SessionId = _sessionId,
                        AttachToken = _attachToken,
                        ClientNonce = Guid.NewGuid().ToString("N")
                    },
                    PacketFlags.OneWay)),
            cancellationToken).ConfigureAwait(false);

        using var response = await tunnel.ReceiveAsync(cancellationToken).ConfigureAwait(false);
        var decoded = packetCodec.Decode(response.Payload, PacketType.kServer);
        if (decoded.Packet is not STunnelAttachedPacket attached || !attached.Success)
            throw new InvalidDataException((decoded.Packet as STunnelAttachedPacket)?.Message ?? "Server rejected tunnel attach.");
    }

    private static async Task RunSessionAsync(
        int laneIndex,
        TunnelSession session,
        CancellationToken cancellationToken)
    {
        try
        {
            await session.RunAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch
        {
            throw new InvalidOperationException($"Tunnel client lane {laneIndex} failed.");
        }
    }

    public async ValueTask SendAsync(Frame frame, CancellationToken cancellationToken = default)
    {
        var lane = SelectLane();
        if (lane == null)
        {
            frame.Dispose();
            throw new InvalidOperationException("No active tunnel lane.");
        }

        await lane.Tunnel.SendAsync(frame, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);

        ProxyTunnelLane[] lanes;
        lock (_lanesLock)
            lanes = _lanes.ToArray();

        foreach (var lane in lanes)
            await lane.Tunnel.DisposeAsync().ConfigureAwait(false);

        foreach (var task in lanes.Select(static lane => lane.SessionTask))
        {
            try { await task.ConfigureAwait(false); }
            catch (OperationCanceledException) {}
            catch { }
        }

        lock (_lanesLock)
            _lanes.Clear();

        if (_scaleTask != null)
        {
            try { await _scaleTask.ConfigureAwait(false); }
            catch (OperationCanceledException) {}
        }

        _cts.Dispose();
    }
}
