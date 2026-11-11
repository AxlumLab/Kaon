using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;

namespace Kaon.Logger;

public static class LogManager
{
    private static readonly Lock sConfigureLock = new();
    private static readonly ConcurrentDictionary<string, ILogger> sLoggers = new(StringComparer.Ordinal);
    private static ILogger? sSharedLogger;
    private static IOutput[]? sConfiguredOutputs;

    public static bool UseShortTypeString { get; set; } = true;
    public static Func<string, ILogger> LoggerCreateFunc { get; set; } = BuildDefaultLogger;
    internal static LogOptions Options { get; private set; } = new();

    public static void Configure(LogOptions options, params IOutput[] outputs)
    {
        ArgumentNullException.ThrowIfNull(options);
        lock (sConfigureLock)
        {
            if (LogOutput.Get() != null)
                throw new InvalidOperationException("Logging has already started and cannot be reconfigured.");

            options.Validate();
            Options = options;
            sConfiguredOutputs = outputs is { Length: > 0 } ? outputs : null;
        }
    }

    public static string GetPattern() => Options.Pattern;
    public static LogLevel MinimumLevel => LogOutput.Get()?.MinimumLevel ?? Options.MinimumLevel;

    public static void SetPattern(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            throw new ArgumentException("Log pattern cannot be empty.", nameof(pattern));

        Options.Pattern = pattern;
        LogOutput.Get()?.UpdatePattern(pattern);
    }

    public static void SetMinimumLevel(LogLevel level)
    {
        Options.MinimumLevel = level;
        LogOutput.Get()?.SetMinimumLevel(level);
    }

    public static void SetCategoryMinimumLevel(string category, LogLevel level)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        EnsureOutput().SetCategoryMinimumLevel(category, level);
    }

    public static bool RemoveCategoryMinimumLevel(string category)
        => LogOutput.Get()?.RemoveCategoryMinimumLevel(category) ?? false;

    public static ILogger GetLogger([CallerMemberName] string name = "")
        => sLoggers.GetOrAdd(name, LoggerCreateFunc);

    public static ILogger GetLogger(object obj)
    {
        ArgumentNullException.ThrowIfNull(obj);
        return GetLogger(obj.GetType());
    }

    public static ILogger GetLogger(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        var name = UseShortTypeString ? BuildShortTypeString(type) : type.Name;
        return GetLogger(name);
    }

    internal static ILogger GetSharedLogger()
        => sSharedLogger ??= LoggerCreateFunc(string.Empty);

    internal static LogOutput EnsureOutput()
    {
        var current = LogOutput.Get();
        if (current != null)
            return current;

        lock (sConfigureLock)
        {
            current = LogOutput.Get();
            if (current != null)
                return current;
            return sConfiguredOutputs is { Length: > 0 }
                ? LogOutput.New(Options, sConfiguredOutputs)
                : LogOutput.New(Options);
        }
    }

    public static LogStatistics GetStatistics()
        => LogOutput.Get()?.GetStatistics() ?? default;

    public static void Shutdown()
        => LogOutput.Get()?.Stop();

    private static ILogger BuildDefaultLogger(string name)
        => new LoggerImpl(name);

    private static string BuildShortTypeString(Type type)
    {
        var ns = type.Namespace;
        if (string.IsNullOrEmpty(ns))
            return type.Name;

        var builder = new StringBuilder(ns.Length + type.Name.Length + 1);
        foreach (var part in ns.Split('.'))
        {
            if (part.Length > 0)
                builder.Append(part[0]).Append('.');
        }

        return builder.Append(type.Name).ToString();
    }
}
