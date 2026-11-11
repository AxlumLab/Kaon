using Kaon.App;
using Kaon.Proxy;
using Kaon.Transport;

namespace Kaon.Client.Proxy;

[HostingService]
public sealed class ClientLocalProxyService(ClientConfig config) : IAsyncDisposable
{
    private LocalProxyEntryManager? _entry;

    public bool IsStarted => _entry != null;

    public void Start(
        ProxyProtocol protocol,
        LocalTcpConnectionHandler tcpHandler,
        LocalUdpDatagramHandler udpHandler)
    {
        if (_entry != null)
            return;

        _entry = new LocalProxyEntryManager(
            new LocalProxyEntryOptions(
                protocol,
                config.ListenHost,
                config.ListenPort,
                config.ProxyBufferSize),
            tcpHandler,
            udpHandler);
        _entry.Start();
    }

    public async ValueTask StopAsync()
    {
        if (_entry == null)
            return;

        await _entry.DisposeAsync().ConfigureAwait(false);
        _entry = null;
    }

    public ValueTask DisposeAsync()
    {
        return StopAsync();
    }
}
