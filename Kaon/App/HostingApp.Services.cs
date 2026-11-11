namespace Kaon.App;

public delegate object HostingServiceFactory(HostingApp app);

public partial class HostingApp
{
    private readonly Dictionary<Type, HostingServiceDescriptor> _services = new();
    private readonly List<object> _serviceCreationOrder = [];

    public void RegisterService<TService>(
        Func<HostingApp, TService> factory,
        HostingServiceStartMode startMode = HostingServiceStartMode.Lazy,
        HostingServiceLifetime lifetime = HostingServiceLifetime.Singleton)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(factory);

        _services[typeof(TService)] = new HostingServiceDescriptor(
            app => factory(app)!,
            startMode,
            lifetime);
    }

    public TService GetService<TService>()
        where TService : class
    {
        if (TryGetService<TService>(out var service))
            return service;

        throw new InvalidOperationException($"Hosting service {typeof(TService).FullName} is not registered.");
    }

    public bool TryGetService<TService>(out TService service)
        where TService : class
    {
        if (!_services.TryGetValue(typeof(TService), out var descriptor))
        {
            service = default!;
            return false;
        }

        service = (TService)GetOrCreateService(typeof(TService), descriptor);
        return true;
    }

    protected async ValueTask StartServicesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var (type, descriptor) in _services.ToArray())
        {
            if (descriptor.StartMode != HostingServiceStartMode.Eager)
                continue;

            var service = GetOrCreateService(type, descriptor);
            if (service is IHostingService hostingService)
                await hostingService.StartAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask StopServicesAsync(CancellationToken cancellationToken = default)
    {
        for (var i = _serviceCreationOrder.Count - 1; i >= 0; i--)
        {
            var service = _serviceCreationOrder[i];
            try
            {
                if (service is IHostingService hostingService)
                    await hostingService.StopAsync(cancellationToken).ConfigureAwait(false);

                switch (service)
                {
                    case IAsyncDisposable asyncDisposable:
                        await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                        break;
                    case IDisposable disposable:
                        disposable.Dispose();
                        break;
                }
            }
            catch
            {
                // StopCore 不能因为某个 service 清理失败而跳过后续 service。
            }
        }

        _serviceCreationOrder.Clear();
    }

    private object GetOrCreateService(Type type, HostingServiceDescriptor descriptor)
    {
        if (descriptor.Lifetime == HostingServiceLifetime.Transient)
            return descriptor.Factory(this);

        if (descriptor.Instance != null)
            return descriptor.Instance;

        var instance = descriptor.Factory(this);
        descriptor.Instance = instance;
        _serviceCreationOrder.Add(instance);
        return instance;
    }

    private sealed class HostingServiceDescriptor(
        HostingServiceFactory factory,
        HostingServiceStartMode startMode,
        HostingServiceLifetime lifetime)
    {
        public HostingServiceFactory Factory { get; } = factory;
        public HostingServiceStartMode StartMode { get; } = startMode;
        public HostingServiceLifetime Lifetime { get; } = lifetime;
        public object? Instance { get; set; }
    }
}
