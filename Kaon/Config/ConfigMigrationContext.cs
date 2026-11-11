namespace Kaon.Config;

public sealed class ConfigMigrationContext
{
    private readonly Dictionary<string, string> _values;

    internal ConfigMigrationContext(Dictionary<string, string> values, int oldVersion, int newVersion)
    {
        _values = values;
        OldVersion = oldVersion;
        NewVersion = newVersion;
    }

    public int OldVersion { get; }
    public int NewVersion { get; }
    public bool IsChanged { get; private set; }

    public bool Contains(string key)
        => _values.ContainsKey(NormalizeKey(key));

    public bool TryGet(string key, out string value)
        => _values.TryGetValue(NormalizeKey(key), out value!);

    public string? Get(string key)
        => _values.GetValueOrDefault(NormalizeKey(key));

    public void Set(string key, string value)
    {
        _values[NormalizeKey(key)] = value;
        IsChanged = true;
    }

    public bool Remove(string key)
    {
        var removed = _values.Remove(NormalizeKey(key));
        IsChanged |= removed;
        return removed;
    }

    private static string NormalizeKey(string key)
        => App.CmdLine.NormalizeKey(key);
}

public interface IConfigMigration
{
    Task MigrateAsync(ConfigMigrationContext context)
        => Task.CompletedTask;
}
