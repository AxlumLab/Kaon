using Kaon.Config;

namespace Kaon.App;

public partial class HostingApp
{
    private readonly Dictionary<Type, HostedConfig> _configs = new();
    
    public TConfig UseConfig<TConfig>(IConfigBackend? backend = null, string? configPath = null)
        where TConfig : class, IConfig, new()
    {
        var type = typeof(TConfig);
        if (_configs.TryGetValue(type, out var existing))
            return (TConfig)existing.Instance;

        var config = new TConfig();
        var metadata = config as IGeneratedConfigMetadata;
        var serializers = new ConfigSerializerRegistry();
        metadata?.RegisterSerializers(serializers);

        var path = configPath ?? metadata?.ConfigFileName;
        var resolvedBackend = backend ?? metadata?.CreateBackend() ?? new JsonBackend();
        var manager = new ConfigFactory(config, resolvedBackend, CommandLine, path, serializers);
        manager.Setup();

        _configs[type] = new HostedConfig(config, manager);
        return config;
    }

    public TConfig GetConfig<TConfig>()
        where TConfig : class, IConfig
    {
        if (_configs.TryGetValue(typeof(TConfig), out var config))
            return (TConfig)config.Instance;

        throw new InvalidOperationException($"Config {typeof(TConfig).FullName} is not registered.");
    }

    private void SaveConfigs()
    {
        foreach (var config in _configs.Values)
        {
            config.Factory.Save();
            config.Dispose();
        }

        _configs.Clear();
    }

    private sealed record HostedConfig(IConfig Instance, ConfigFactory Factory) : IDisposable
    {
        public void Dispose()
        {
            Factory.Dispose();
        }
    }
}