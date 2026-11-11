namespace Kaon.App;

[AttributeUsage(AttributeTargets.Class)]
public sealed class HostingAppAttribute : Attribute
{
    public HostingAppAttribute()
    {
    }
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class HostingHandlerAttribute(int order = 0) : Attribute
{
    public int Order { get; } = order;
}

public interface IHostingAppHandler<TApp>
    where TApp : HostingApp
{
    void PreMain(TApp app, string[] args)
    {
    }

    void PostMain(TApp app, string[] args)
    {
    }
}

public enum HostingServiceStartMode
{
    Lazy = 0,
    Eager = 1
}

public enum HostingServiceLifetime
{
    Singleton = 0,
    Transient = 1
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class HostingServiceAttribute(
    HostingServiceStartMode startMode = HostingServiceStartMode.Lazy,
    HostingServiceLifetime lifetime = HostingServiceLifetime.Singleton) : Attribute
{
    public HostingServiceStartMode StartMode { get; } = startMode;

    public HostingServiceLifetime Lifetime { get; } = lifetime;
}

public interface IHostingService
{
    ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }

    ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }
}
