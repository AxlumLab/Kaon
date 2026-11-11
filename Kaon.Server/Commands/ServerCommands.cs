using Kaon.Command;
using Kaon.Logger;

namespace Kaon.Server.Commands;

[ConsoleCommand("status", "Show relay server status.", "status")]
public sealed class ServerStatusCommand : IConsoleCommand
{
    public ValueTask ExecuteAsync(CommandContext context)
    {
        var status = context.GetHost<ServerApp>().ProxyHost?.GetStatus() ?? "Proxy server is not started.";
        context.Logger.Info(status);
        return ValueTask.CompletedTask;
    }
}

[ConsoleCommand("clients", "List connected client tunnels.", "clients")]
public sealed class ServerClientsCommand : IConsoleCommand
{
    public ValueTask ExecuteAsync(CommandContext context)
    {
        var rows = context.GetHost<ServerApp>().ProxyHost?.BuildClientList() ?? string.Empty;
        context.Logger.Info(
            "Connected clients:\n{0}",
            string.IsNullOrWhiteSpace(rows) ? "(empty)" : rows);
        return ValueTask.CompletedTask;
    }
}

[ConsoleCommand("services", "List registered proxy services.", "services")]
public sealed class ServerServicesCommand : IConsoleCommand
{
    public ValueTask ExecuteAsync(CommandContext context)
    {
        var rows = context.GetHost<ServerApp>().ProxyHost?.BuildServiceList() ?? string.Empty;
        context.Logger.Info(
            "Registered services:\n{0}",
            string.IsNullOrWhiteSpace(rows) ? "(empty)" : rows);
        return ValueTask.CompletedTask;
    }
}

[ConsoleCommand("compress", "Enable or disable relay send compression.", "compress <on|off>")]
public sealed class ServerCompressCommand : IConsoleCommand
{
    public ValueTask ExecuteAsync(CommandContext context)
    {
        if (!context.TryGetArgument(0, out var value))
        {
            context.Logger.Warn("Usage: compress <on|off>");
            return ValueTask.CompletedTask;
        }

        var enabled = value.Equals("on", StringComparison.OrdinalIgnoreCase) ||
                      value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                      value.Equals("1", StringComparison.OrdinalIgnoreCase);
        context.GetHost<ServerApp>().ProxyHost?.SetCompressionEnabled(enabled);
        return ValueTask.CompletedTask;
    }
}

[ConsoleCommand("compress-threshold", "Set relay compression threshold bytes.", "compress-threshold <bytes>")]
public sealed class ServerCompressThresholdCommand : IConsoleCommand
{
    public ValueTask ExecuteAsync(CommandContext context)
    {
        if (!context.TryGetArgument(0, out var rawValue) ||
            !int.TryParse(rawValue, out var threshold))
        {
            context.Logger.Warn("Usage: compress-threshold <bytes>");
            return ValueTask.CompletedTask;
        }

        context.GetHost<ServerApp>().ProxyHost?.SetCompressionThreshold(threshold);
        return ValueTask.CompletedTask;
    }
}
