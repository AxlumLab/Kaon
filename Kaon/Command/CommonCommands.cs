using Kaon.App;
using Kaon.Logger;
using Kaon.Packets;

namespace Kaon.Command;

[ConsoleCommand("help", "Show commands.", "help", "?")]
public sealed class HelpCommand : IConsoleCommand
{
    public ValueTask ExecuteAsync(CommandContext context)
    {
        foreach (var command in context.Registry.Commands)
            context.Logger.Info("{0,-20} {1}", command.Usage.Length == 0 ? command.Name : command.Usage, command.Description);
        return ValueTask.CompletedTask;
    }
}

[ConsoleCommand("exit", "Stop current app.", "exit", "quit")]
public sealed class ExitCommand : IConsoleCommand
{
    public ValueTask ExecuteAsync(CommandContext context)
    {
        context.GetHost<HostingApp>().StopApp();
        return ValueTask.CompletedTask;
    }
}

[ConsoleCommand("log-level", "Get or change the global log level.", "log-level [trace|debug|info|warn|error|fatal|none]")]
public sealed class LogLevelCommand : IConsoleCommand
{
    public ValueTask ExecuteAsync(CommandContext context)
    {
        if (!context.TryGetArgument(0, out var value))
        {
            context.Logger.Info("Current log level: {0}.", LogManager.MinimumLevel);
            return ValueTask.CompletedTask;
        }

        if (!TryParse(value, out var level))
        {
            context.Logger.Warn("Unknown log level: {0}.", value);
            return ValueTask.CompletedTask;
        }

        LogManager.SetMinimumLevel(level);
        context.Logger.Info("Global log level changed to {0}.", level);
        return ValueTask.CompletedTask;
    }

    private static bool TryParse(string value, out LogLevel level)
    {
        level = value.ToLowerInvariant() switch
        {
            "trace" => LogLevel.kTrace,
            "debug" => LogLevel.kDebug,
            "info" => LogLevel.kInfo,
            "warn" or "warning" => LogLevel.kWarn,
            "error" => LogLevel.kError,
            "fatal" => LogLevel.kFatal,
            "none" or "off" => LogLevel.kNone,
            _ => (LogLevel)(-1)
        };
        return (int)level >= (int)LogLevel.kTrace;
    }
}

[ConsoleCommand("log-status", "Show logger queue statistics.", "log-status")]
public sealed class LogStatusCommand : IConsoleCommand
{
    public ValueTask ExecuteAsync(CommandContext context)
    {
        var stats = LogManager.GetStatistics();
        context.Logger.Info(
            "Logger: level={0}, queued={1}/{2}, enqueued={3}, processed={4}, dropped={5}.",
            LogManager.MinimumLevel,
            stats.Queued,
            stats.QueueCapacity,
            stats.Enqueued,
            stats.Processed,
            stats.Dropped);
        return ValueTask.CompletedTask;
    }
}
