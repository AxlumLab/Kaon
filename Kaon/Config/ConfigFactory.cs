using Kaon.App;
using Kaon.Logger;

namespace Kaon.Config;

public class ConfigFactory : IDisposable
{
    private const string VersionKey = "__version";

    public ConfigFactory(
        IConfig config,
        IConfigBackend backend,
        CmdLine? commandLine = null,
        string? configPath = null,
        ConfigSerializerRegistry? serializers = null)
    {
        _config = config;
        _backend = backend;
        _commandLine = commandLine;
        ConfigPath = ResolveConfigPath(commandLine, configPath);
        _serializers = serializers ?? new ConfigSerializerRegistry();
        _backend.Init(_serializers);
        _metadata = config as IGeneratedConfigMetadata;
        _migration = config as IConfigMigration;
        _logger = LogManager.GetLogger(this);
    }

    private enum CurrentState
    {
        Idle, Writing, Reading
    }

    public void Setup()
    {
        SetupAsync().GetAwaiter().GetResult();
    }

    public string ConfigPath { get; }

    public async Task SetupAsync()
    {
        var created = false;
        if (!File.Exists(ConfigPath))
        {
            if (!await SaveTo(ConfigPath).ConfigureAwait(false))
                throw new Exception("Failed to save default config.json");
            created = true;
        }

        if (!await LoadFrom(ConfigPath).ConfigureAwait(false))
            throw new Exception("Failed to load config.json");

        if (created || _isDirty)
        {
            if (!await SaveTo(ConfigPath).ConfigureAwait(false))
                throw new Exception("Failed to save migrated config.json");
            _isDirty = false;
        }
    }

    public void Save()
    {
        SaveAsync().GetAwaiter().GetResult();
    }

    public async Task SaveAsync()
    {
        if (!await SaveTo(ConfigPath).ConfigureAwait(false))
            throw new Exception("Failed to save default config.json");
        _isDirty = false;
    }

    public void ProcessField<T>(string key, ref T value)
    {
        switch (_state)
        {
            case CurrentState.Reading:
                ReadField(key, ref value);
                break;
            case CurrentState.Writing:
                WriteField(key, ref value);
                break;
            case CurrentState.Idle:
            default:
                break;
        }
    }

    private async Task<bool> LoadFrom(string path)
    {
        if (!TryEnter(CurrentState.Reading))
        {
            _logger.Warn("ConfigManager is not in idle state. Current state {0}", _state);
            return false;
        }

        try
        {
            _values.Clear();

            foreach (var (key, value) in _backend.ReadAll(path))
                _values[NormalizeKey(key)] = value;

            await MigrateIfNeeded().ConfigureAwait(false);
            await _config.OnProcessing(this).ConfigureAwait(false);
            return true;
        }
        catch (Exception e)
        {
            _logger.Error(e, "Failed to load config from {0}", path);
            return false;
        }
        finally
        {
            _values.Clear();
            _state = CurrentState.Idle;
        }
    }

    private async Task<bool> SaveTo(string path)
    {
        if (!TryEnter(CurrentState.Writing))
        {
            _logger.Warn("ConfigManager is not in idle state. Current state {0}", _state);
            return false;
        }

        try
        {
            _map.Clear();
            WriteVersion();
            await _config.OnProcessing(this).ConfigureAwait(false);

            if (_backend.WriteAll(path, _map))
                return true;

            _logger.Trace("ConfigManager failed to save config.");
            return false;
        }
        catch (Exception e)
        {
            _logger.Error(e, "Failed to save config to {0}", path);
            return false;
        }
        finally
        {
            _map.Clear();
            _state = CurrentState.Idle;
        }
    }

    private void ReadField<T>(string key, ref T value)
    {
        if (_commandLine?.TryTakeValue(key, out var cliValue) == true)
        {
            if (_serializers.TryDeserialize(key, cliValue, out T parsedCli))
                value = parsedCli;
            else
                _logger.Warn("Ignored invalid command line value for {0}: {1}", key, cliValue);
            return;
        }

        if (!_values.TryGetValue(NormalizeKey(key), out var rawValue))
        {
            _isDirty = true;
            return;
        }

        if (_serializers.TryDeserialize(key, rawValue, out T parsed))
            value = parsed;
        else
        {
            _isDirty = true;
            _logger.Warn("Ignored invalid config value for {0}: {1}", key, rawValue);
        }
    }

    private void WriteField<T>(string key, ref T value)
    {
        _map.TryAdd(key, _serializers.Serialize(key, value));
    }

    private void WriteVersion()
    {
        _map[VersionKey] = CurrentVersion.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private async Task MigrateIfNeeded()
    {
        var oldVersion = ReadStoredVersion();
        var currentVersion = CurrentVersion;
        if (oldVersion == currentVersion)
            return;

        if (oldVersion > currentVersion)
            throw new InvalidDataException($"Config version {oldVersion} is newer than supported version {currentVersion}.");

        if (_migration != null)
        {
            var context = new ConfigMigrationContext(_values, oldVersion, currentVersion);
            await _migration.MigrateAsync(context).ConfigureAwait(false);
            _isDirty |= context.IsChanged;
        }

        _values[NormalizeKey(VersionKey)] = currentVersion.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _isDirty = true;
    }

    private int ReadStoredVersion()
    {
        if (!_values.TryGetValue(NormalizeKey(VersionKey), out var rawVersion))
        {
            _isDirty = true;
            return 0;
        }

        if (int.TryParse(rawVersion, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var version))
            return version;

        _isDirty = true;
        return 0;
    }

    private bool TryEnter(CurrentState state)
    {
        if (_state != CurrentState.Idle)
            return false;

        _state = state;
        return true;
    }

    private static string NormalizeKey(string key)
        => CmdLine.NormalizeKey(key);

    private static string ResolveConfigPath(CmdLine? commandLine, string? configPath)
    {
        if (commandLine?.TryTakeValue("config", out var cliPath) == true ||
            commandLine?.TryTakeValue("config-path", out cliPath) == true)
        {
            return Path.IsPathRooted(cliPath)
                ? cliPath
                : Path.GetFullPath(cliPath, Environment.CurrentDirectory);
        }

        if (!string.IsNullOrWhiteSpace(configPath))
            return Path.IsPathRooted(configPath)
                ? configPath
                : Path.GetFullPath(configPath, Environment.CurrentDirectory);

        return Path.Combine(Environment.CurrentDirectory, "config.json");
    }

    public void Dispose()
    {
        _serializers.Dispose();
    }

    private CurrentState _state = CurrentState.Idle;
    private readonly IConfig _config;
    private readonly IConfigBackend _backend;
    private readonly IGeneratedConfigMetadata? _metadata;
    private readonly IConfigMigration? _migration;
    private readonly ConfigSerializerRegistry _serializers;
    private readonly CmdLine? _commandLine;
    private readonly ILogger _logger;
    private readonly Dictionary<string, string> _map = new();
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
    private bool _isDirty;
    private int CurrentVersion => Math.Max(1, _metadata?.ConfigVersion ?? 1);
}
