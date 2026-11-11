using System.Net;
using System.Collections.Concurrent;
using Kaon.Logger;
using Kaon.App;
using Kaon.Packets;
using Kaon.Protocol;
using Kaon.Proxy;
using Kaon.Transport;

namespace Kaon.Client.Proxy;

[HostingService(HostingServiceStartMode.Eager)]
public sealed class ClientProxyHost(
    ClientConfig config,
    ClientTunnelService tunnelService,
    ClientLocalProxyService localProxyService) : IHostingService, IAsyncDisposable
{
    private readonly ILogger _logger = LogManager.GetLogger(typeof(ClientProxyHost));
    private readonly CancellationTokenSource _cts = new();
    private readonly ProxyStreamManager _streams = new();
    private readonly ProxyOpenStreamTracker _openStreams = new();
    private readonly ConcurrentDictionary<ulong, UdpProxySession> _udpSessions = new();
    private readonly ConcurrentDictionary<string, ulong> _udpEntryStreams = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _providerOpenLimiter = new(32, 32);
    private long _nextStreamId;
    private string _targetServiceName = config.TargetServiceName;
    private ProxyProtocol _protocol = ParseProtocol(config.Protocol);
    private bool _listenEnabled = config.ListenEnabled;
    private ClientRole _role = ParseRole(config.Mode);
    private LocalServiceRegistry? _services;

    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        config.Mode = ToConfigRole(_role);
        ValidateRoleConfig();
        _services = new LocalServiceRegistry();
        var service = _role == ClientRole.Receive ? RegisterLocalService() : null;

        await tunnelService.StartAsync(
            new ProxyFrameRouter(_streams, RouteUdpFrameAsync),
            (context, ct) => ConfigureTunnelLaneAsync(context, service, ct),
            cancellationToken).ConfigureAwait(false);

        if (_listenEnabled && _role == ClientRole.Entry)
            StartLocalListener();

        _logger.Info(
            "Proxy client connected {0}:{1}, name={2}, role={3}, lanes={4}, service={5}, targetService={6}, localTarget={7}:{8}, listen={9}:{10}.",
            config.ServerHost,
            config.ServerPort,
            config.ClientName,
            _role,
            tunnelService.Count,
            config.ServiceName,
            _targetServiceName,
            config.LocalHost,
            config.LocalPort,
            _listenEnabled && _role == ClientRole.Entry ? config.ListenHost : "disabled",
            _listenEnabled && _role == ClientRole.Entry ? config.ListenPort : 0);
    }

    private async ValueTask ConfigureTunnelLaneAsync(
        TunnelClientLaneContext context,
        ProxyServiceDescriptor? service,
        CancellationToken cancellationToken)
    {
        RegisterInitiatorHandlers(context.Dispatcher);
        if (_role == ClientRole.Receive)
            RegisterProviderHandlers(context.Dispatcher, context.Index, context.Control, context.Pump);

        if (_role == ClientRole.Receive && service != null)
            await context.Control.SendRegisterServiceAsync(service, cancellationToken).ConfigureAwait(false);
    }

    private void RegisterInitiatorHandlers(PacketDispatcher dispatcher)
    {
        dispatcher.Register<SOpenStreamResultPacket>((streamId, packet, cancellationToken) =>
        {
            if (packet.Packet is SOpenStreamResultPacket result)
                _openStreams.TryComplete(streamId, result.Success, result.Message);

            return ValueTask.CompletedTask;
        });

        dispatcher.Register<SServiceListPacket>((streamId, packet, cancellationToken) =>
        {
            if (packet.Packet is SServiceListPacket list)
                _logger.Info("Available services:\n{0}", string.IsNullOrWhiteSpace(list.Services) ? "(empty)" : list.Services);

            return ValueTask.CompletedTask;
        });
    }

    private void RegisterProviderHandlers(
        PacketDispatcher dispatcher,
        int laneIndex,
        IProxyControlChannel control,
        ProxyStreamPump pump)
    {
        dispatcher.Register<SOpenStreamPacket>(async (streamId, packet, cancellationToken) =>
        {
            if (packet.Packet is not SOpenStreamPacket openStream)
                return;

            _ = Task.Run(
                () => RunProviderOpenStreamAsync(laneIndex, control, pump, streamId, openStream.ServiceName, _cts.Token),
                CancellationToken.None);
            await ValueTask.CompletedTask;
        });
    }

    private async Task RunProviderOpenStreamAsync(
        int laneIndex,
        IProxyControlChannel control,
        ProxyStreamPump pump,
        ulong streamId,
        string serviceName,
        CancellationToken cancellationToken)
    {
        await _providerOpenLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await HandleProviderOpenStreamAsync(
                laneIndex,
                control,
                pump,
                streamId,
                serviceName,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception e)
        {
            _logger.Error(e, "Provider open stream {0} failed.", streamId);
            try
            {
                await control.SendOpenStreamResultAsync(
                    streamId,
                    success: false,
                    "Provider open failed.",
                    CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
            }
        }
        finally
        {
            _providerOpenLimiter.Release();
        }
    }

    public void SetTarget(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            _logger.Warn("Target service cannot be empty.");
            return;
        }

        _targetServiceName = target.Trim();
        _logger.Info("Proxy target switched to {0}.", _targetServiceName);
    }

    public void SetListenEndpoint(string host, int port)
    {
        config.ListenHost = host;
        config.ListenPort = port;
        _logger.Info("Local proxy listen endpoint set to {0}:{1}. Restart listening to apply.", host, port);
    }

    public async ValueTask SetProxyEnabledAsync(bool enabled)
    {
        _listenEnabled = enabled;
        config.ListenEnabled = enabled;

        if (!enabled)
        {
            await StopLocalListenerAsync().ConfigureAwait(false);
            _logger.Info("Local proxy entry stopped.");
            return;
        }

        if (_role != ClientRole.Entry)
        {
            _logger.Warn("Only entry clients can start local proxy entry.");
            return;
        }

        if (!localProxyService.IsStarted)
            StartLocalListener();
    }

    public async ValueTask SetRoleAsync(string role)
    {
        var parsed = ParseRole(role);
        if (parsed == _role)
        {
            _logger.Info("Client role is already {0}.", _role);
            return;
        }

        _role = parsed;
        config.Mode = ToConfigRole(parsed);

        if (_role != ClientRole.Entry)
            await SetProxyEnabledAsync(false).ConfigureAwait(false);

        _logger.Warn("Client role set to {0}. Restart this client to rebuild tunnel registration.", config.Mode);
    }

    public void SetTargetEndpoint(string host, int port)
    {
        config.LocalEnabled = true;
        config.LocalHost = host;
        config.LocalPort = port;
        if (_role == ClientRole.Receive)
            RegisterLocalService();
        _logger.Info("Local service target set to {0}:{1}.", host, port);
    }

    public async ValueTask SetLocalEnabled(bool enabled)
    {
        config.LocalEnabled = enabled;
        if (enabled)
        {
            config.ListenEnabled = false;
            _listenEnabled = false;
            await StopLocalListenerAsync().ConfigureAwait(false);
        }

        _logger.Info("Receiver local endpoint set to {0}. Restart this client to rebuild tunnel registration.", enabled ? "on" : "off");
    }

    public void SetProtocol(string protocol)
    {
        _protocol = ParseProtocol(protocol);
        config.Protocol = _protocol.ToString().ToLowerInvariant();
        if (_role == ClientRole.Receive)
            RegisterLocalService();
        _logger.Info("Proxy protocol set to {0}.", _protocol);
    }

    public void SetCompressionEnabled(bool enabled)
    {
        tunnelService.CompressionEnabled = enabled;
        _logger.Info("Tunnel send compression set to {0}.", enabled ? "on" : "off");
    }

    public void SetCompressionThreshold(int threshold)
    {
        tunnelService.CompressionThreshold = threshold;
        _logger.Info("Tunnel compression threshold set to {0} bytes.", tunnelService.CompressionThreshold);
    }

    public string GetStatus()
    {
        return
            $"client={config.ClientName}, role={_role}, protocol={_protocol}, " +
            $"service={config.ServiceName}, targetService={_targetServiceName}, " +
            $"local={config.LocalEnabled} {config.LocalHost}:{config.LocalPort}, " +
            $"listen={(_listenEnabled ? "on" : "off")} {config.ListenHost}:{config.ListenPort}, " +
            $"lanes={tunnelService.Count}, tcpStreams={_streams.Count}, udpSessions={_udpSessions.Count}, " +
            $"buffer={GetProxyBufferSize()}, compression={(tunnelService.CompressionEnabled ? "on" : "off")}:{tunnelService.CompressionThreshold}";
    }

    public async ValueTask RequestServiceListAsync(CancellationToken cancellationToken = default)
    {
        var lane = tunnelService.SelectLane();
        if (lane == null)
        {
            _logger.Warn("Tunnel is not connected.");
            return;
        }

        await lane.Control.SendListServicesAsync("", cancellationToken).ConfigureAwait(false);
    }

    private void StartLocalListener()
    {
        if (_role != ClientRole.Entry)
        {
            _logger.Warn("Only entry clients can listen for local app connections.");
            return;
        }

        localProxyService.Start(
            _protocol,
            async (connection, ct) => await OpenRemoteAndPumpAsync(connection, ct).ConfigureAwait(false),
            HandleLocalUdpDatagramAsync);

        _logger.Info(
            "Local {0} proxy entry listening on {1}:{2} for service {3}.",
            _protocol,
            config.ListenHost,
            config.ListenPort,
            config.ServiceName);
    }

    private ProxyServiceDescriptor RegisterLocalService()
    {
        if (!config.LocalEnabled)
            throw new InvalidOperationException("Receiver client requires proxy.local.enabled=true.");

        _services ??= new LocalServiceRegistry();
        var service = new ProxyServiceDescriptor
        {
            ClientName = config.ClientName,
            ServiceName = config.ServiceName,
            Protocol = _protocol,
            LocalEndPoint = new DnsEndPoint(config.LocalHost, config.LocalPort)
        };
        _services.Register(service);
        return service;
    }

    private async ValueTask StopLocalListenerAsync()
    {
        await localProxyService.StopAsync().ConfigureAwait(false);

        foreach (var streamId in _udpEntryStreams.Values.ToArray())
            await RemoveUdpSessionAsync(streamId).ConfigureAwait(false);
    }

    private async ValueTask HandleLocalUdpDatagramAsync(
        UdpConnection listener,
        OwnedDatagram datagram,
        CancellationToken cancellationToken)
    {
        using (datagram)
        {
            var session = await GetOrOpenEntryUdpSessionAsync(
                listener,
                datagram.RemoteEndPoint,
                cancellationToken).ConfigureAwait(false);

            if (session != null)
                await session.SendOwnedToTunnelAsync(datagram.DetachPayload(), cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<UdpProxySession?> GetOrOpenEntryUdpSessionAsync(
        UdpConnection listener,
        EndPoint appEndPoint,
        CancellationToken cancellationToken)
    {
        if (_role != ClientRole.Entry)
            return null;

        var key = appEndPoint.ToString() ?? string.Empty;
        if (_udpEntryStreams.TryGetValue(key, out var existingStreamId) &&
            _udpSessions.TryGetValue(existingStreamId, out var existingSession))
        {
            return await existingSession.WaitOpenAsync(cancellationToken).ConfigureAwait(false)
                ? existingSession
                : null;
        }

        var lane = tunnelService.SelectLane();
        if (lane == null)
            return null;

        var streamId = AllocateStreamId();
        if (!_openStreams.TryBegin(streamId))
            return null;

        var session = UdpProxySession.CreateEntry(streamId, tunnelService, listener, appEndPoint, key);
        if (!_udpSessions.TryAdd(streamId, session))
            return null;

        _udpEntryStreams[key] = streamId;
#if DEBUG
        _logger.Debug("UDP entry open begin: stream={0}, remote={1}, target={2}, lane={3}.", streamId, appEndPoint, _targetServiceName, lane.Index);
#endif

        try
        {
            await lane.Control.SendClientOpenStreamAsync(
                streamId,
                _targetServiceName,
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            session.MarkOpen(false);
            await RemoveUdpSessionAsync(streamId).ConfigureAwait(false);
            throw;
        }

        var result = await _openStreams.WaitAsync(
            streamId,
            TimeSpan.FromSeconds(10),
            cancellationToken).ConfigureAwait(false);

        if (!result.Success)
        {
            session.MarkOpen(false);
            _logger.Warn("Open remote UDP stream {0} failed: {1}", streamId, result.Message ?? string.Empty);
            await RemoveUdpSessionAsync(streamId).ConfigureAwait(false);
            try { await lane.Tunnel.SendAsync(FrameFactory.Reset(streamId, 408), CancellationToken.None).ConfigureAwait(false); }
            catch { }
            return null;
        }

        session.MarkOpen(true);

#if DEBUG
        _logger.Debug("UDP entry open success: stream={0}, remote={1}, sessions={2}.", streamId, appEndPoint, _udpSessions.Count);
#endif

        return session;
    }

    private async ValueTask HandleProviderOpenStreamAsync(
        int laneIndex,
        IProxyControlChannel control,
        ProxyStreamPump pump,
        ulong streamId,
        string serviceName,
        CancellationToken cancellationToken)
    {
        if (_role != ClientRole.Receive)
        {
            await control.SendOpenStreamResultAsync(
                streamId,
                success: false,
                "Client is not a receiver.",
                cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!_services!.TryGet(serviceName, out var service) ||
            service is null)
        {
            await control.SendOpenStreamResultAsync(
                streamId,
                success: false,
                $"Service not found: {serviceName}",
                cancellationToken).ConfigureAwait(false);
            return;
        }

        if (service.Protocol == ProxyProtocol.Udp)
        {
            await HandleProviderOpenUdpAsync(laneIndex, control, streamId, service, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await HandleProviderOpenTcpAsync(control, pump, streamId, service, cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask HandleProviderOpenTcpAsync(
        IProxyControlChannel control,
        ProxyStreamPump pump,
        ulong streamId,
        ProxyServiceDescriptor service,
        CancellationToken cancellationToken)
    {
        IStreamConnection connection;

        try
        {
            connection = await TcpConnector.Shared.ConnectAsync(
                service.LocalEndPoint,
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await control.SendOpenStreamResultAsync(
                streamId,
                success: false,
                $"Connect local service failed: {service.ServiceName}",
                cancellationToken).ConfigureAwait(false);
            return;
        }

        var stream = new ProxyStream(streamId, connection);

        if (!_streams.TryAdd(stream))
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            await control.SendOpenStreamResultAsync(
                streamId,
                success: false,
                "Duplicate stream id.",
                cancellationToken).ConfigureAwait(false);
            return;
        }

        stream.MarkOpen();

        await control.SendOpenStreamResultAsync(
            streamId,
            success: true,
            message: null,
            cancellationToken).ConfigureAwait(false);

        _ = RunTcpProviderStreamAsync(stream, pump, cancellationToken);
    }

    private async ValueTask HandleProviderOpenUdpAsync(
        int laneIndex,
        IProxyControlChannel control,
        ulong streamId,
        ProxyServiceDescriptor service,
        CancellationToken cancellationToken)
    {
        UdpConnection connection;

        try
        {
            connection = await UdpConnector.Shared.ConnectAsync(
                service.LocalEndPoint,
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await control.SendOpenStreamResultAsync(
                streamId,
                success: false,
                $"Connect local UDP service failed: {service.ServiceName}",
                cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!tunnelService.TryGetLane(laneIndex, out var lane))
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            await control.SendOpenStreamResultAsync(
                streamId,
                success: false,
                "Tunnel lane closed.",
                cancellationToken).ConfigureAwait(false);
            return;
        }

        var session = UdpProxySession.CreateProvider(
            streamId,
            tunnelService,
            connection,
            connection.RemoteEndPoint ?? service.LocalEndPoint,
            GetProxyBufferSize());

        if (!_udpSessions.TryAdd(streamId, session))
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            await control.SendOpenStreamResultAsync(
                streamId,
                success: false,
                "Duplicate UDP stream id.",
                cancellationToken).ConfigureAwait(false);
            return;
        }

        await control.SendOpenStreamResultAsync(
            streamId,
            success: true,
            message: null,
            cancellationToken).ConfigureAwait(false);

        session.StartReceiveLoop(_cts.Token);
#if DEBUG
        _logger.Debug("UDP provider open success: stream={0}, service={1}, target={2}, sessions={3}.", streamId, service.ServiceName, service.LocalEndPoint, _udpSessions.Count);
#endif
    }

    private async Task RunTcpProviderStreamAsync(
        ProxyStream stream,
        ProxyStreamPump pump,
        CancellationToken cancellationToken)
    {
        try
        {
            await pump.RunAsync(stream, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await _streams.RemoveAsync(stream.StreamId).ConfigureAwait(false);
        }
    }

    private async Task OpenRemoteAndPumpAsync(
        IStreamConnection connection,
        CancellationToken cancellationToken)
    {
        if (_role != ClientRole.Entry)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            return;
        }

        var lane = tunnelService.SelectLane();
        if (lane == null)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            return;
        }

        var streamId = AllocateStreamId();
        var stream = new ProxyStream(streamId, connection);

        if (!_streams.TryAdd(stream))
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            return;
        }

        if (!_openStreams.TryBegin(streamId))
        {
            await _streams.RemoveAsync(streamId).ConfigureAwait(false);
            return;
        }

        await lane.Control.SendClientOpenStreamAsync(
            streamId,
            _targetServiceName,
            cancellationToken).ConfigureAwait(false);

        var result = await _openStreams.WaitAsync(
            streamId,
            TimeSpan.FromSeconds(10),
            cancellationToken).ConfigureAwait(false);

        if (!result.Success)
        {
            _logger.Warn("Open remote stream {0} failed: {1}", streamId, result.Message ?? string.Empty);
            await _streams.RemoveAsync(streamId).ConfigureAwait(false);
            return;
        }

        stream.MarkOpen();

        try
        {
            await lane.Pump.RunAsync(stream, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await _streams.RemoveAsync(stream.StreamId).ConfigureAwait(false);
        }
    }

    private ulong AllocateStreamId()
    {
        return checked((ulong)Interlocked.Increment(ref _nextStreamId));
    }

    private async ValueTask RouteUdpFrameAsync(
        Frame frame,
        CancellationToken cancellationToken)
    {
        if (!_udpSessions.TryGetValue(frame.StreamId, out var session))
        {
            if (frame.Type == FrameType.RawData)
                _logger.Warn("Unknown UDP stream: {0}.", frame.StreamId);
#if DEBUG
            _logger.Debug(
                "Unknown UDP frame ignored: stream={0}, type={1}, role={2}, sessions=[{3}], entryMap=[{4}].",
                frame.StreamId,
                frame.Type,
                _role,
                string.Join(",", _udpSessions.Keys.OrderBy(static x => x)),
                string.Join(",", _udpEntryStreams.Select(static pair => $"{pair.Key}->{pair.Value}")));
#endif
            return;
        }

#if DEBUG
        _logger.Trace("Route UDP frame: stream={0}, type={1}, payload={2}, entry={3}.", frame.StreamId, frame.Type, frame.Payload.Length, session.EntryKey ?? "provider");
#endif

        switch (frame.Type)
        {
            case FrameType.RawData:
                await session.SendToLocalAsync(frame.Payload, cancellationToken).ConfigureAwait(false);
                break;
            case FrameType.Close:
            case FrameType.Reset:
                await RemoveUdpSessionAsync(frame.StreamId).ConfigureAwait(false);
                break;
        }
    }

    private async ValueTask RemoveUdpSessionAsync(ulong streamId)
    {
        if (!_udpSessions.TryRemove(streamId, out var session))
            return;

        if (!string.IsNullOrEmpty(session.EntryKey))
            _udpEntryStreams.TryRemove(session.EntryKey, out _);

        session.MarkOpen(false);
#if DEBUG
        _logger.Debug("UDP session removed: stream={0}, entry={1}, remaining={2}.", streamId, session.EntryKey ?? "provider", _udpSessions.Count);
#endif
        await session.DisposeAsync().ConfigureAwait(false);
    }

    private static ProxyProtocol ParseProtocol(string protocol)
    {
        return Enum.TryParse<ProxyProtocol>(protocol, ignoreCase: true, out var parsed)
            ? parsed
            : ProxyProtocol.Tcp;
    }

    private static ClientRole ParseRole(string mode)
    {
        if (mode.Equals("receive", StringComparison.OrdinalIgnoreCase) ||
            mode.Equals("receiver", StringComparison.OrdinalIgnoreCase) ||
            mode.Equals("provider", StringComparison.OrdinalIgnoreCase))
            return ClientRole.Receive;

        if (mode.Equals("entry", StringComparison.OrdinalIgnoreCase) ||
            mode.Equals("ingress", StringComparison.OrdinalIgnoreCase) ||
            mode.Equals("proxy", StringComparison.OrdinalIgnoreCase))
            return ClientRole.Entry;

        // 兼容旧配置。旧 both 实际会导致同一客户端既注册服务又监听入口，现在统一收敛为入口方。
        return ClientRole.Entry;
    }

    private static string ToConfigRole(ClientRole role)
    {
        return role == ClientRole.Receive ? "receive" : "entry";
    }

    private void ValidateRoleConfig()
    {
        if (config.LocalEnabled && config.ListenEnabled)
            throw new InvalidOperationException("Invalid client config: proxy.local.enabled and proxy.listen.enabled cannot both be true.");

        switch (_role)
        {
            case ClientRole.Entry:
                if (config.LocalEnabled)
                    throw new InvalidOperationException("Entry client cannot enable proxy.local.");
                if (!config.ListenEnabled)
                    _logger.Warn("Entry client has proxy.listen.enabled=false; it will not accept local app connections.");
                break;
            case ClientRole.Receive:
                if (config.ListenEnabled)
                    throw new InvalidOperationException("Receive client cannot enable proxy.listen.");
                if (!config.LocalEnabled)
                    throw new InvalidOperationException("Receive client requires proxy.local.enabled=true.");
                break;
        }
    }

    private int GetProxyBufferSize()
    {
        return Math.Clamp(config.ProxyBufferSize, 4 * 1024, 1024 * 1024);
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);

        await localProxyService.StopAsync().ConfigureAwait(false);

        await _streams.DisposeAsync().ConfigureAwait(false);

        foreach (var streamId in _udpSessions.Keys.ToArray())
            await RemoveUdpSessionAsync(streamId).ConfigureAwait(false);

        _providerOpenLimiter.Dispose();
        _cts.Dispose();
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        return DisposeAsync();
    }

    // Product-level mode switch only. The protocol model should treat Provider/Consumer
    // as per-service and per-bridge roles, not as a permanent identity for the client.
    private enum ClientRole
    {
        Entry,
        Receive
    }

    private sealed class UdpProxySession(
        ulong streamId,
        IFrameSender sender,
        UdpConnection connection,
        EndPoint localEndPoint,
        string? entryKey,
        int bufferSize) : IAsyncDisposable
    {
        private Task? _receiveTask;
        private readonly TaskCompletionSource<bool> _openCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly bool _isEntry = entryKey != null;

        public ulong StreamId { get; } = streamId;
        public string? EntryKey { get; } = entryKey;

        public static UdpProxySession CreateEntry(
            ulong streamId,
            IFrameSender sender,
            UdpConnection listener,
            EndPoint appEndPoint,
            string entryKey)
        {
            return new UdpProxySession(streamId, sender, listener, appEndPoint, entryKey, 0);
        }

        public static UdpProxySession CreateProvider(
            ulong streamId,
            IFrameSender sender,
            UdpConnection connection,
            EndPoint serviceEndPoint,
            int bufferSize)
        {
            return new UdpProxySession(streamId, sender, connection, serviceEndPoint, null, bufferSize);
        }

        public ValueTask SendOwnedToTunnelAsync(
            OwnedBuffer? payload,
            CancellationToken cancellationToken)
        {
            if (payload == null)
                return ValueTask.CompletedTask;

            return sender.SendAsync(
                FrameFactory.RawData(StreamId, payload),
                cancellationToken);
        }

        public void MarkOpen(bool success)
        {
            _openCompletion.TrySetResult(success);
        }

        public async ValueTask<bool> WaitOpenAsync(CancellationToken cancellationToken)
        {
            if (!_isEntry)
                return true;

            return await _openCompletion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        public ValueTask SendToLocalAsync(
            ReadOnlyMemory<byte> payload,
            CancellationToken cancellationToken)
        {
            return connection.SendAsync(payload, localEndPoint, cancellationToken);
        }

        public void StartReceiveLoop(CancellationToken cancellationToken)
        {
            _receiveTask = Task.Run(() => ReceiveLoopAsync(cancellationToken), cancellationToken);
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            var size = Math.Clamp(bufferSize, 4 * 1024, 1024 * 1024);
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    OwnedBuffer? buffer = new(System.Buffers.MemoryPool<byte>.Shared.Rent(size));
                    try
                    {
                        var datagram = await connection.ReceiveAsync(buffer.WritableMemory[..size], cancellationToken).ConfigureAwait(false);
                        buffer.SetLength(datagram.Data.Length);
                        await SendOwnedToTunnelAsync(buffer, cancellationToken).ConfigureAwait(false);
                        buffer = null;
                    }
                    finally
                    {
                        buffer?.Dispose();
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (EntryKey == null)
                await connection.DisposeAsync().ConfigureAwait(false);

            if (_receiveTask != null)
            {
                try { await _receiveTask.ConfigureAwait(false); }
                catch (OperationCanceledException) {}
                catch (ObjectDisposedException) {}
            }
        }
    }
}
