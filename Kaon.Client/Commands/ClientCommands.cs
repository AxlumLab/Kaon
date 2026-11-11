using Kaon.Client;
using Kaon.Command;
using Kaon.Logger;

namespace Kaon.Client.Commands;

[ConsoleCommand("list", "List online services on relay server.", "list")]
public sealed class ListCommand : IConsoleCommand
{
    public async ValueTask ExecuteAsync(CommandContext context)
    {
        var app = context.GetHost<ClientApp>();
        if (app.ProxyHost != null)
            await app.ProxyHost.RequestServiceListAsync(context.CancellationToken);
    }
}

[ConsoleCommand("target", "Select remote service. Supports <service> or <clientId>:<service>.", "target <service|clientId:service>")]
public sealed class TargetCommand : IConsoleCommand
{
    public ValueTask ExecuteAsync(CommandContext context)
    {
        if (!context.TryGetArgument(0, out var target))
        {
            context.Logger.Warn("Usage: target <service|clientId:service>");
            return ValueTask.CompletedTask;
        }

        context.GetHost<ClientApp>().ProxyHost?.SetTarget(target);
        return ValueTask.CompletedTask;
    }
}

[ConsoleCommand("listen", "Set local app ingress listen endpoint.", "listen <host> <port>")]
public sealed class ListenCommand : IConsoleCommand
{
    public ValueTask ExecuteAsync(CommandContext context)
    {
        if (!context.TryGetArgument(0, out var host) ||
            !context.TryGetArgument(1, out var rawPort) ||
            !int.TryParse(rawPort, out var port))
        {
            context.Logger.Warn("Usage: listen <host> <port>");
            return ValueTask.CompletedTask;
        }

        context.GetHost<ClientApp>().ProxyHost?.SetListenEndpoint(host, port);
        return ValueTask.CompletedTask;
    }
}

[ConsoleCommand("proxy", "Start or stop local app ingress proxy.", "proxy <on|off>")]
public sealed class ProxyCommand : IConsoleCommand
{
    public async ValueTask ExecuteAsync(CommandContext context)
    {
        if (!context.TryGetArgument(0, out var value))
        {
            context.Logger.Warn("Usage: proxy <on|off>");
            return;
        }

        var enabled = value.Equals("on", StringComparison.OrdinalIgnoreCase) ||
                      value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                      value.Equals("1", StringComparison.OrdinalIgnoreCase);
        if (context.GetHost<ClientApp>().ProxyHost != null)
            await context.GetHost<ClientApp>().ProxyHost!.SetProxyEnabledAsync(enabled);
    }
}

[ConsoleCommand("mode", "Set this client role: entry or receive.", "mode <entry|receive>")]
public sealed class ModeCommand : IConsoleCommand
{
    public async ValueTask ExecuteAsync(CommandContext context)
    {
        if (!context.TryGetArgument(0, out var value))
        {
            context.Logger.Warn("Usage: mode <entry|receive>");
            return;
        }

        if (context.GetHost<ClientApp>().ProxyHost != null)
            await context.GetHost<ClientApp>().ProxyHost!.SetRoleAsync(value);
    }
}

[ConsoleCommand("out", "Set receiver-side local app endpoint.", "out <host> <port>")]
public sealed class OutCommand : IConsoleCommand
{
    public ValueTask ExecuteAsync(CommandContext context)
    {
        if (!context.TryGetArgument(0, out var host) ||
            !context.TryGetArgument(1, out var rawPort) ||
            !int.TryParse(rawPort, out var port))
        {
            context.Logger.Warn("Usage: out <host> <port>");
            return ValueTask.CompletedTask;
        }

        context.GetHost<ClientApp>().ProxyHost?.SetTargetEndpoint(host, port);
        return ValueTask.CompletedTask;
    }
}

[ConsoleCommand("local", "Enable or disable receiver-side local app endpoint.", "local <on|off>")]
public sealed class LocalCommand : IConsoleCommand
{
    public ValueTask ExecuteAsync(CommandContext context)
    {
        if (!context.TryGetArgument(0, out var value))
        {
            context.Logger.Warn("Usage: local <on|off>");
            return ValueTask.CompletedTask;
        }

        var enabled = value.Equals("on", StringComparison.OrdinalIgnoreCase) ||
                      value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                      value.Equals("1", StringComparison.OrdinalIgnoreCase);
        context.GetHost<ClientApp>().ProxyHost?.SetLocalEnabled(enabled);
        return ValueTask.CompletedTask;
    }
}

[ConsoleCommand("protocol", "Set proxy protocol: tcp or udp.", "protocol <tcp|udp>")]
public sealed class ProtocolCommand : IConsoleCommand
{
    public ValueTask ExecuteAsync(CommandContext context)
    {
        if (!context.TryGetArgument(0, out var protocol))
        {
            context.Logger.Warn("Usage: protocol <tcp|udp>");
            return ValueTask.CompletedTask;
        }

        context.GetHost<ClientApp>().ProxyHost?.SetProtocol(protocol);
        return ValueTask.CompletedTask;
    }
}

[ConsoleCommand("status", "Show current client proxy settings.", "status")]
public sealed class StatusCommand : IConsoleCommand
{
    public ValueTask ExecuteAsync(CommandContext context)
    {
        var status = context.GetHost<ClientApp>().ProxyHost?.GetStatus() ?? "Proxy is not started.";
        context.Logger.Info(status);
        return ValueTask.CompletedTask;
    }
}

[ConsoleCommand("compress", "Enable or disable tunnel send compression.", "compress <on|off>")]
public sealed class CompressCommand : IConsoleCommand
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
        context.GetHost<ClientApp>().ProxyHost?.SetCompressionEnabled(enabled);
        return ValueTask.CompletedTask;
    }
}

[ConsoleCommand("compress-threshold", "Set tunnel compression threshold bytes.", "compress-threshold <bytes>")]
public sealed class CompressThresholdCommand : IConsoleCommand
{
    public ValueTask ExecuteAsync(CommandContext context)
    {
        if (!context.TryGetArgument(0, out var rawValue) ||
            !int.TryParse(rawValue, out var threshold))
        {
            context.Logger.Warn("Usage: compress-threshold <bytes>");
            return ValueTask.CompletedTask;
        }

        context.GetHost<ClientApp>().ProxyHost?.SetCompressionThreshold(threshold);
        return ValueTask.CompletedTask;
    }
}
