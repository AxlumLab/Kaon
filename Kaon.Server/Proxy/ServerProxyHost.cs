using System.Collections.Concurrent;
using System.Security.Cryptography;
using Kaon.App;
using Kaon.Logger;
using Kaon.Packets;
using Kaon.Protocol;
using Kaon.Protocol.Packet;
using Kaon.Proxy;
using Kaon.Transport;

namespace Kaon.Server.Proxy;

[HostingService(HostingServiceStartMode.Eager)]
public sealed class ServerProxyHost(
    ServerConfig config,
    PacketCodec packetCodec,
    ServerTunnelService tunnelService) : IHostingService, IAsyncDisposable
{
    private readonly ILogger _logger = LogManager.GetLogger(typeof(ServerProxyHost));
    private readonly CancellationTokenSource _cts = new();

    private readonly ConcurrentDictionary<ulong, ClientSession> _sessions = new();
    private readonly ConcurrentDictionary<string, ServiceRoute> _services = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<ulong, PublishedService> _servicesById = new();
    private readonly ConcurrentDictionary<ulong, BridgeConnection> _bridges = new();

    // Compatibility index while clients still keep local ProxyStream ids.
    // Frame v2 carries BridgeId, but StreamKey remains useful for old control/open routing.
    private readonly ConcurrentDictionary<StreamKey, ulong> _streamRoutes = new();
    private readonly ConcurrentDictionary<StreamKey, ulong> _pendingOpens = new();

    private readonly StreamIdAllocator _streamIds = new();
    private long _nextSessionId;
    private long _nextServiceId = 1000;
    private long _nextBridgeId = 9000;

    public string GetStatus()
    {
        return
            $"listen={config.IpAddress}:{config.Port}, sessions={_sessions.Count}, " +
            $"services={_servicesById.Count}, bridges={_bridges.Count}, maxConnections={config.MaxTunnelConnections}, " +
            $"compression={(tunnelService.CompressionEnabled ? "on" : "off")}:{tunnelService.CompressionThreshold}";
    }

    public string BuildClientList()
    {
        var rows = _sessions.Values
            .OrderBy(static session => session.SessionId)
            .Select(static session =>
                $"{session.SessionId}  name={session.Name}  tunnels={session.Tunnels.Count}  control={session.Tunnels.ControlKey}  services={session.Services.Count}  bridges={session.Bridges.Count}");

        return string.Join(Environment.NewLine, rows);
    }

    public string BuildServiceList()
    {
        return BuildServiceList(string.Empty);
    }

    public void SetCompressionEnabled(bool enabled)
    {
        config.TunnelCompressionEnabled = enabled;
        tunnelService.CompressionEnabled = enabled;
        _logger.Info("Tunnel send compression set to {0}.", enabled ? "on" : "off");
    }

    public void SetCompressionThreshold(int threshold)
    {
        config.TunnelCompressionThreshold = threshold;
        tunnelService.CompressionThreshold = threshold;
        _logger.Info("Tunnel compression threshold set to {0} bytes.", tunnelService.CompressionThreshold);
    }

    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        await tunnelService.StartAsync(AcceptTunnelAsync, cancellationToken).ConfigureAwait(false);
        _logger.Info("Relay server listening on {0}:{1}.", config.IpAddress, config.Port);
    }

    private async ValueTask AcceptTunnelAsync(TunnelConnection tunnel, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));

        Frame? firstFrame = null;
        try
        {
            firstFrame = await tunnel.ReceiveAsync(timeout.Token).ConfigureAwait(false);
            var decoded = packetCodec.Decode(firstFrame.Payload, PacketType.kClient);

            switch (decoded.Packet)
            {
                case CSessionHelloPacket hello:
                    await AcceptSessionHelloAsync(tunnel, hello, timeout.Token).ConfigureAwait(false);
                    break;
                case CAttachTunnelPacket attach:
                    await AcceptAttachTunnelAsync(tunnel, attach, timeout.Token).ConfigureAwait(false);
                    break;
                default:
                    throw new InvalidDataException($"First tunnel packet must be session handshake, got {decoded.Packet.GetType().Name}.");
            }
        }
        catch
        {
            await tunnel.DisposeAsync().ConfigureAwait(false);
            throw;
        }
        finally
        {
            firstFrame?.Dispose();
        }
    }

    private async ValueTask AcceptSessionHelloAsync(
        TunnelConnection tunnel,
        CSessionHelloPacket hello,
        CancellationToken cancellationToken)
    {
        var session = new ClientSession(
            checked((ulong)Interlocked.Increment(ref _nextSessionId)),
            CreateAttachToken(),
            tunnel);

        session.Name = string.IsNullOrWhiteSpace(hello.ClientName)
            ? $"session-{session.SessionId}"
            : hello.ClientName;

        _sessions[session.SessionId] = session;

        await SendServerPacketAsync(
            session,
            0,
            new SSessionAcceptedPacket
            {
                SessionId = session.SessionId.ToString(),
                AttachToken = session.AttachToken,
                TunnelId = 1
            },
            cancellationToken).ConfigureAwait(false);

        _ = Task.Run(() => RunTunnelAsync(session, session.Tunnels.ControlTunnel), CancellationToken.None);

        _logger.Info(
            "Session {0} attached tunnel {1}, remote={2}.",
            session.SessionId,
            session.Tunnels.ControlKey,
            tunnel.RemoteEndPoint?.ToString() ?? "unknown");
    }

    private async ValueTask AcceptAttachTunnelAsync(
        TunnelConnection tunnel,
        CAttachTunnelPacket attach,
        CancellationToken cancellationToken)
    {
        if (!ulong.TryParse(attach.SessionId, out var sessionId) ||
            !_sessions.TryGetValue(sessionId, out var session) ||
            !CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(session.AttachToken),
                System.Text.Encoding.UTF8.GetBytes(attach.AttachToken)))
        {
            await tunnel.SendAsync(
                FrameFactory.Packet(
                    packetCodec.EncodeOwned(
                        new STunnelAttachedPacket
                        {
                            Success = false,
                            TunnelId = 0,
                            Message = "Invalid session attach."
                        },
                        PacketFlags.OneWay)),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!session.TryAttachTunnel(tunnel, out var relayTunnel, out var message))
        {
            await tunnel.SendAsync(
                FrameFactory.Packet(
                    packetCodec.EncodeOwned(
                        new STunnelAttachedPacket
                        {
                            Success = false,
                            TunnelId = 0,
                            Message = message
                        },
                        PacketFlags.OneWay)),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        await tunnel.SendAsync(
            FrameFactory.Packet(
                packetCodec.EncodeOwned(
                    new STunnelAttachedPacket
                    {
                        Success = true,
                        TunnelId = checked((int)relayTunnel.Key.TunnelId)
                    },
                    PacketFlags.OneWay)),
            cancellationToken).ConfigureAwait(false);

        _ = Task.Run(() => RunTunnelAsync(session, relayTunnel), CancellationToken.None);

        _logger.Info(
            "Session {0} attached tunnel {1}, remote={2}.",
            session.SessionId,
            relayTunnel.Key,
            tunnel.RemoteEndPoint?.ToString() ?? "unknown");
    }

    private async Task RunTunnelAsync(ClientSession session, RelayTunnel tunnel)
    {
        try
        {
            await foreach (var frame in tunnel.Connection.ReadAllAsync(_cts.Token).ConfigureAwait(false))
                await HandleFrameAsync(session, tunnel, frame, _cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
        }
        catch (Exception e)
        {
            _logger.Error(e, "Session {0} failed.", session.SessionId);
        }
        finally
        {
            await DetachTunnelAsync(session, tunnel).ConfigureAwait(false);
        }
    }

    private static string CreateAttachToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    private async ValueTask HandleFrameAsync(
        ClientSession session,
        RelayTunnel tunnel,
        Frame frame,
        CancellationToken cancellationToken)
    {
        try
        {
            switch (frame.Type)
            {
                case FrameType.Packet:
                    await HandlePacketAsync(session, frame, cancellationToken).ConfigureAwait(false);
                    break;
                case FrameType.RawData:
                case FrameType.Close:
                case FrameType.Reset:
                    await RouteBridgeFrameAsync(session, tunnel, frame, cancellationToken).ConfigureAwait(false);
                    break;
                case FrameType.Ping:
                    await tunnel.Connection.SendAsync(new Frame(FrameType.Pong, 0, frame.DetachPayload()), cancellationToken).ConfigureAwait(false);
                    break;
            }
        }
        finally
        {
            frame.Dispose();
        }
    }

    private async ValueTask HandlePacketAsync(
        ClientSession session,
        Frame frame,
        CancellationToken cancellationToken)
    {
        var decoded = packetCodec.Decode(frame.Payload, PacketType.kClient);

        switch (decoded.Packet)
        {
            case CRegisterServicePacket register:
                RegisterService(session, register);
                break;
            case CUnregisterServicePacket unregister:
                UnregisterService(session, unregister.ServiceName);
                break;
            case COpenStreamPacket open:
                await HandleOpenStreamAsync(session, frame.StreamId, open.ServiceName, cancellationToken).ConfigureAwait(false);
                break;
            case COpenStreamResultPacket result:
                await HandleOpenStreamResultAsync(session, frame.StreamId, result, cancellationToken).ConfigureAwait(false);
                break;
            case CListServicesPacket list:
                await SendServerPacketAsync(
                    session,
                    0,
                    new SServiceListPacket { Services = BuildServiceList(list.Filter) },
                    cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    private void RegisterService(ClientSession session, CRegisterServicePacket register)
    {
        session.Name = string.IsNullOrWhiteSpace(register.ClientName)
            ? $"session-{session.SessionId}"
            : register.ClientName;

        var service = session.Services.GetOrAdd(
            register.ServiceName,
            _ => new PublishedService(
                checked((ulong)Interlocked.Increment(ref _nextServiceId)),
                session.SessionId,
                register.ServiceName,
                register.Protocol,
                register.ServiceName));

        _servicesById[service.ServiceId] = service;
        _services.GetOrAdd(register.ServiceName, static _ => new ServiceRoute()).Add(service);

        _logger.Info(
            "Service {0} \"{1}\" published by session {2}.",
            service.ServiceId,
            service.Name,
            session.SessionId);
    }

    private void UnregisterService(ClientSession session, string serviceName)
    {
        if (_services.TryGetValue(serviceName, out var route))
            route.Remove(session.SessionId);

        if (session.Services.TryRemove(serviceName, out var service))
            _servicesById.TryRemove(service.ServiceId, out _);
    }

    private async ValueTask HandleOpenStreamAsync(
        ClientSession consumer,
        ulong consumerStreamId,
        string serviceSelector,
        CancellationToken cancellationToken)
    {
        if (!TryResolveService(consumer, serviceSelector, out var service) ||
            !_sessions.TryGetValue(service.OwnerSessionId, out var provider))
        {
            await SendServerOpenResultAsync(
                consumer,
                consumerStreamId,
                success: false,
                $"Service not available: {serviceSelector}",
                cancellationToken).ConfigureAwait(false);
            return;
        }

        var providerStreamId = _streamIds.Next();
        var providerKey = new StreamKey(provider.SessionId, providerStreamId);
        var bridge = new BridgeConnection(
            checked((ulong)Interlocked.Increment(ref _nextBridgeId)),
            service.ServiceId,
            consumer.SessionId,
            provider.SessionId,
            consumerStreamId,
            providerStreamId);

        _bridges[bridge.BridgeId] = bridge;
        consumer.Bridges[bridge.BridgeId] = bridge;
        provider.Bridges[bridge.BridgeId] = bridge;
        _pendingOpens[providerKey] = bridge.BridgeId;

#if DEBUG
        _logger.Debug(
            "Bridge {0} open begin: consumer={1}:{2}, provider={3}:{4}, service={5} \"{6}\".",
            bridge.BridgeId,
            consumer.SessionId,
            consumerStreamId,
            provider.SessionId,
            providerStreamId,
            service.ServiceId,
            service.Name);
#endif

        await SendServerPacketAsync(
            provider,
            providerStreamId,
            new SOpenStreamPacket { ServiceName = service.OwnerLocalServiceId },
            cancellationToken).ConfigureAwait(false);
    }

    private bool TryResolveService(
        ClientSession consumer,
        string selector,
        out PublishedService service)
    {
        service = null!;

        if (ulong.TryParse(selector, out var serviceId) &&
            _servicesById.TryGetValue(serviceId, out service!) &&
            service.OwnerSessionId != consumer.SessionId)
            return true;

        var colon = selector.IndexOf(':');
        if (colon > 0 &&
            ulong.TryParse(selector[..colon], out var sessionId))
        {
            var serviceName = selector[(colon + 1)..];
            return _sessions.TryGetValue(sessionId, out var owner) &&
                   owner.SessionId != consumer.SessionId &&
                   owner.Services.TryGetValue(serviceName, out service!);
        }

        return _services.TryGetValue(selector, out var route) &&
               route.TryPick(consumer.SessionId, out service);
    }

    private string BuildServiceList(string filter)
    {
        var rows = new List<string>();
        foreach (var service in _servicesById.Values.OrderBy(static x => x.ServiceId))
        {
            if (!_sessions.TryGetValue(service.OwnerSessionId, out var owner))
                continue;

            if (!string.IsNullOrWhiteSpace(filter) &&
                !service.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                !owner.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                !service.ServiceId.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase))
                continue;

            rows.Add($"{service.ServiceId}  name={service.Name}  owner={owner.SessionId}:{owner.Name}  protocol={service.Protocol}");
        }

        return string.Join(Environment.NewLine, rows);
    }

    private async ValueTask HandleOpenStreamResultAsync(
        ClientSession provider,
        ulong providerStreamId,
        COpenStreamResultPacket result,
        CancellationToken cancellationToken)
    {
        var providerKey = new StreamKey(provider.SessionId, providerStreamId);
        if (!_pendingOpens.TryRemove(providerKey, out var bridgeId) ||
            !_bridges.TryGetValue(bridgeId, out var bridge))
            return;

        if (!_sessions.TryGetValue(bridge.ConsumerSessionId, out var consumer))
        {
            RemoveBridge(bridge);
            return;
        }

        if (result.Success)
        {
            bridge.TryActivate();
            _streamRoutes[bridge.ConsumerKey] = bridge.BridgeId;
            _streamRoutes[bridge.ProviderKey] = bridge.BridgeId;
        }
        else
        {
            bridge.TryClose("provider rejected");
            RemoveBridge(bridge);
        }

#if DEBUG
        _logger.Debug(
            "Bridge {0} open result: provider={1}:{2}, consumer={3}:{4}, success={5}, message={6}.",
            bridge.BridgeId,
            provider.SessionId,
            providerStreamId,
            bridge.ConsumerSessionId,
            bridge.ConsumerStreamId,
            result.Success,
            result.Message ?? "");
#endif

        await SendServerOpenResultAsync(
            consumer,
            bridge.ConsumerStreamId,
            result.Success,
            result.Message,
            cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask RouteBridgeFrameAsync(
        ClientSession sourceSession,
        RelayTunnel sourceTunnel,
        Frame frame,
        CancellationToken cancellationToken)
    {
        var sourceKey = new StreamKey(sourceSession.SessionId, frame.StreamId);
        if (!_streamRoutes.TryGetValue(sourceKey, out var bridgeId) ||
            !_bridges.TryGetValue(bridgeId, out var bridge) ||
            !bridge.TryResolvePeer(sourceSession.SessionId, out var targetSessionId, out var targetStreamId) ||
            !_sessions.TryGetValue(targetSessionId, out var targetSession))
        {
#if DEBUG
            _logger.Debug(
                "Bridge route missing: source={0}, tunnel={1}, stream={2}, type={3}, knownRoutes=[{4}].",
                sourceSession.SessionId,
                sourceTunnel.Key,
                frame.StreamId,
                frame.Type,
                string.Join(",", _streamRoutes.Select(static pair => $"{pair.Key.SessionId}:{pair.Key.StreamId}->bridge:{pair.Value}")));
#endif
            if (frame.Type != FrameType.Close)
                await sourceTunnel.Connection.SendAsync(FrameFactory.Reset(frame.StreamId, 404), cancellationToken).ConfigureAwait(false);
            return;
        }

        var targetTunnel = targetSession.Tunnels.Schedule(bridge, frame.PayloadLength);
#if DEBUG
        _logger.Trace(
            "Frame routed: bridge={0} source={1} target={2} offset={3} type={4} length={5}.",
            bridge.BridgeId,
            sourceTunnel.Key,
            targetTunnel.Key,
            0,
            frame.Type,
            frame.PayloadLength);
#endif
        var direction = sourceSession.SessionId == bridge.ConsumerSessionId
            ? FrameDirection.ConsumerToProvider
            : FrameDirection.ProviderToConsumer;
        var forwarded = new Frame(
            frame.Type,
            targetStreamId,
            frame.DetachPayload(),
            frame.Flags,
            frame.OriginalLength,
            bridge.BridgeId,
            direction,
            frame.StreamOffset);
        await targetTunnel.Connection.SendAsync(forwarded, cancellationToken).ConfigureAwait(false);

        if (frame.Type is FrameType.Close or FrameType.Reset)
        {
            if (frame.Type == FrameType.Reset)
            {
                bridge.TryReset("reset frame");
                RemoveBridge(bridge);
            }
            else if (bridge.TryHalfClose(sourceSession.SessionId) &&
                     bridge.State == BridgeState.Closed)
            {
                RemoveBridge(bridge);
            }
        }
    }

    private void RemoveBridge(BridgeConnection bridge)
    {
        _bridges.TryRemove(bridge.BridgeId, out _);
        _streamRoutes.TryRemove(bridge.ConsumerKey, out _);
        _streamRoutes.TryRemove(bridge.ProviderKey, out _);
        _pendingOpens.TryRemove(bridge.ProviderKey, out _);

        if (_sessions.TryGetValue(bridge.ConsumerSessionId, out var consumer))
            consumer.Bridges.TryRemove(bridge.BridgeId, out _);

        if (_sessions.TryGetValue(bridge.ProviderSessionId, out var provider))
            provider.Bridges.TryRemove(bridge.BridgeId, out _);
    }

    private ValueTask SendServerOpenResultAsync(
        ClientSession session,
        ulong streamId,
        bool success,
        string? message,
        CancellationToken cancellationToken)
    {
        return SendServerPacketAsync(
            session,
            streamId,
            new SOpenStreamResultPacket { Success = success, Message = message },
            cancellationToken);
    }

    private ValueTask SendServerPacketAsync(
        ClientSession session,
        ulong streamId,
        IPacket packet,
        CancellationToken cancellationToken)
    {
        var payload = packetCodec.EncodeOwned(packet, PacketFlags.OneWay);
        return session.Tunnels.ControlTunnel.Connection.SendAsync(FrameFactory.Packet(payload, streamId), cancellationToken);
    }

    private async ValueTask RemoveSessionAsync(ClientSession session)
    {
        _sessions.TryRemove(session.SessionId, out _);

        foreach (var service in session.Services.Values)
        {
            if (_services.TryGetValue(service.Name, out var route))
                route.Remove(session.SessionId);
            _servicesById.TryRemove(service.ServiceId, out _);
        }

        foreach (var bridge in session.Bridges.Values.ToArray())
        {
            var peerId = bridge.ConsumerSessionId == session.SessionId
                ? bridge.ProviderSessionId
                : bridge.ConsumerSessionId;
            var peerStreamId = bridge.ConsumerSessionId == session.SessionId
                ? bridge.ProviderStreamId
                : bridge.ConsumerStreamId;

            RemoveBridge(bridge);

            if (_sessions.TryGetValue(peerId, out var peer))
            {
                try { await peer.Tunnels.ControlTunnel.Connection.SendAsync(FrameFactory.Reset(peerStreamId, 410), CancellationToken.None).ConfigureAwait(false); }
                catch { }
            }
        }

        await tunnelService.RemoveAsync(session.Tunnels.ControlTunnel.Connection).ConfigureAwait(false);
        _logger.Info("Session {0} disconnected.", session.SessionId);
    }

    private async ValueTask DetachTunnelAsync(ClientSession session, RelayTunnel tunnel)
    {
        if (!session.Tunnels.Detach(tunnel.Key.TunnelId))
            return;

        await tunnelService.RemoveAsync(tunnel.Connection).ConfigureAwait(false);
        _logger.Info("Session {0} detached tunnel {1}.", session.SessionId, tunnel.Key);

        if (session.Tunnels.Count == 0)
        {
            await RemoveSessionAsync(session).ConfigureAwait(false);
            return;
        }

        await ResetSessionBridgesAsync(session, "tunnel detached").ConfigureAwait(false);
    }

    private async ValueTask ResetSessionBridgesAsync(ClientSession session, string reason)
    {
        foreach (var bridge in session.Bridges.Values.ToArray())
        {
            bridge.TryReset(reason);
            var peerId = bridge.ConsumerSessionId == session.SessionId
                ? bridge.ProviderSessionId
                : bridge.ConsumerSessionId;
            var peerStreamId = bridge.ConsumerSessionId == session.SessionId
                ? bridge.ProviderStreamId
                : bridge.ConsumerStreamId;

            RemoveBridge(bridge);

            if (_sessions.TryGetValue(peerId, out var peer))
            {
                try { await peer.Tunnels.ControlTunnel.Connection.SendAsync(FrameFactory.Reset(peerStreamId, 502), CancellationToken.None).ConfigureAwait(false); }
                catch { }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);

        foreach (var session in _sessions.Values.ToArray())
            await tunnelService.RemoveAsync(session.Tunnels.ControlTunnel.Connection).ConfigureAwait(false);

        _cts.Dispose();
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        return DisposeAsync();
    }

    /// <summary>
    /// One online client session. Authentication is intentionally reserved:
    /// future JWT/session-token validation should happen before creating or attaching to this object.
    /// </summary>
    private sealed class ClientSession(ulong sessionId, string attachToken, TunnelConnection controlConnection)
    {
        public ulong SessionId { get; } = sessionId;
        public string AttachToken { get; } = attachToken;
        public string Name { get; set; } = $"session-{sessionId}";
        public TunnelPool Tunnels { get; } = new(sessionId, controlConnection);
        public ConcurrentDictionary<string, PublishedService> Services { get; } = new(StringComparer.Ordinal);
        public ConcurrentDictionary<ulong, BridgeConnection> Bridges { get; } = new();

        public bool TryAttachTunnel(TunnelConnection connection, out RelayTunnel tunnel, out string? message)
        {
            return Tunnels.TryAttach(connection, out tunnel, out message);
        }
    }

    /// <summary>
    /// Physical tunnel set owned by one session. The first tunnel is the control tunnel for now.
    /// Later AttachTunnel can add data tunnels here after validating a short-lived attach token.
    /// </summary>
    private sealed class TunnelPool
    {
        private const int MaxTunnels = 16;
        private readonly Lock _lock = new();
        private readonly Dictionary<uint, RelayTunnel> _tunnels = new();
        private uint _nextTunnelId = 1;

        public TunnelPool(ulong sessionId, TunnelConnection controlConnection)
        {
            ControlTunnel = new RelayTunnel(new TunnelKey(sessionId, 1), controlConnection);
            _tunnels[1] = ControlTunnel;
        }

        public RelayTunnel ControlTunnel { get; }
        public string ControlKey => ControlTunnel.Key.ToString();
        public int Count
        {
            get
            {
                lock (_lock)
                    return _tunnels.Count;
            }
        }

        public bool TryAttach(TunnelConnection connection, out RelayTunnel tunnel, out string? message)
        {
            lock (_lock)
            {
                if (_tunnels.Count >= MaxTunnels)
                {
                    tunnel = null!;
                    message = "Too many tunnels in this session.";
                    return false;
                }

                var tunnelId = ++_nextTunnelId;
                tunnel = new RelayTunnel(new TunnelKey(ControlTunnel.Key.SessionId, tunnelId), connection);
                _tunnels[tunnelId] = tunnel;
                message = null;
                return true;
            }
        }

        public bool Detach(uint tunnelId)
        {
            lock (_lock)
                return _tunnels.Remove(tunnelId);
        }

        public RelayTunnel Schedule(BridgeConnection bridge, int payloadLength)
        {
            lock (_lock)
            {
                if (_tunnels.Count == 0)
                    throw new InvalidOperationException("Session has no active tunnels.");

                return _tunnels.Values
                    .Where(static tunnel => !tunnel.Connection.IsClosed)
                    .OrderBy(static tunnel => tunnel.Connection.QueuedBytes)
                    .ThenBy(static tunnel => tunnel.Key.TunnelId)
                    .First();
            }
        }
    }

    private sealed record RelayTunnel(TunnelKey Key, TunnelConnection Connection);

    private readonly record struct TunnelKey(ulong SessionId, uint TunnelId)
    {
        public override string ToString() => $"{SessionId}/{TunnelId}";
    }

    /// <summary>
    /// Service currently advertised by an online session. This is separate from bridges:
    /// one service can back many simultaneous proxied connections.
    /// </summary>
    private sealed record PublishedService(
        ulong ServiceId,
        ulong OwnerSessionId,
        string Name,
        string Protocol,
        string OwnerLocalServiceId);

    /// <summary>
    /// Runtime bridge for one concrete proxied connection. Frame v2 carries BridgeId;
    /// StreamKey remains the compatibility index for local stream ids on each endpoint.
    /// </summary>
    private sealed class BridgeConnection(
        ulong bridgeId,
        ulong serviceId,
        ulong consumerSessionId,
        ulong providerSessionId,
        ulong consumerStreamId,
        ulong providerStreamId)
    {
        private int _state = (int)BridgeState.Opening;
        private int _consumerClosed;
        private int _providerClosed;

        public ulong BridgeId { get; } = bridgeId;
        public ulong ServiceId { get; } = serviceId;
        public ulong ConsumerSessionId { get; } = consumerSessionId;
        public ulong ProviderSessionId { get; } = providerSessionId;
        public ulong ConsumerStreamId { get; } = consumerStreamId;
        public ulong ProviderStreamId { get; } = providerStreamId;
        public StreamKey ConsumerKey => new(ConsumerSessionId, ConsumerStreamId);
        public StreamKey ProviderKey => new(ProviderSessionId, ProviderStreamId);
        public BridgeState State => (BridgeState)Volatile.Read(ref _state);

        public bool TryActivate()
        {
            return Interlocked.CompareExchange(
                ref _state,
                (int)BridgeState.Active,
                (int)BridgeState.Opening) == (int)BridgeState.Opening;
        }

        public bool TryHalfClose(ulong sourceSessionId)
        {
            if (sourceSessionId == ConsumerSessionId)
                Volatile.Write(ref _consumerClosed, 1);
            else if (sourceSessionId == ProviderSessionId)
                Volatile.Write(ref _providerClosed, 1);
            else
                return false;

            if (Volatile.Read(ref _consumerClosed) != 0 &&
                Volatile.Read(ref _providerClosed) != 0)
                Volatile.Write(ref _state, (int)BridgeState.Closed);
            else
                Volatile.Write(ref _state, (int)BridgeState.HalfClosed);

            return true;
        }

        public bool TryClose(string reason)
        {
            var previous = Interlocked.Exchange(ref _state, (int)BridgeState.Closed);
            return previous != (int)BridgeState.Closed;
        }

        public bool TryReset(string reason)
        {
            var previous = Interlocked.Exchange(ref _state, (int)BridgeState.Reset);
            return previous != (int)BridgeState.Reset;
        }

        public bool TryResolvePeer(ulong sourceSessionId, out ulong targetSessionId, out ulong targetStreamId)
        {
            if (sourceSessionId == ConsumerSessionId)
            {
                targetSessionId = ProviderSessionId;
                targetStreamId = ProviderStreamId;
                return true;
            }

            if (sourceSessionId == ProviderSessionId)
            {
                targetSessionId = ConsumerSessionId;
                targetStreamId = ConsumerStreamId;
                return true;
            }

            targetSessionId = 0;
            targetStreamId = 0;
            return false;
        }
    }

    private enum BridgeState
    {
        Opening = 0,
        Active = 1,
        HalfClosed = 2,
        Closing = 3,
        Closed = 4,
        Reset = 5
    }

    private readonly record struct StreamKey(ulong SessionId, ulong StreamId);

    private sealed class ServiceRoute
    {
        private readonly ConcurrentDictionary<ulong, PublishedService> _providers = new();
        private long _next;

        public void Add(PublishedService service)
        {
            _providers[service.OwnerSessionId] = service;
        }

        public void Remove(ulong sessionId)
        {
            _providers.TryRemove(sessionId, out _);
        }

        public bool TryPick(ulong consumerSessionId, out PublishedService service)
        {
            var providers = _providers.Values
                .Where(provider => provider.OwnerSessionId != consumerSessionId)
                .OrderBy(static provider => provider.OwnerSessionId)
                .ToArray();

            if (providers.Length == 0)
            {
                service = null!;
                return false;
            }

            var index = Math.Abs(Interlocked.Increment(ref _next));
            service = providers[index % providers.Length];
            return true;
        }
    }
}
