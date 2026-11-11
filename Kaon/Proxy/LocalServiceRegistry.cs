namespace Kaon.Proxy;

public sealed class LocalServiceRegistry
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, ProxyServiceDescriptor> _services = new(StringComparer.Ordinal);

    public void Register(ProxyServiceDescriptor service)
    {
        ArgumentNullException.ThrowIfNull(service);
        lock (_lock)
            _services[service.ServiceName] = service;
    }

    public bool TryGet(
        string serviceName,
        out ProxyServiceDescriptor? service)
    {
        lock (_lock)
            return _services.TryGetValue(serviceName, out service);
    }
}
