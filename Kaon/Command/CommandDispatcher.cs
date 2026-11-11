using Kaon.Logger;

namespace Kaon.Command;

public sealed class CommandDispatcher
{
    private readonly ILogger _logger = LogManager.GetLogger(typeof(CommandDispatcher));
    private readonly object _host;

    public CommandDispatcher(CommandRegistry registry, object host)
    {
        Registry = registry;
        _host = host;
    }

    public CommandRegistry Registry { get; }

    public async ValueTask<bool> DispatchAsync(
        string line,
        CancellationToken cancellationToken = default)
    {
        var tokens = CommandLineTokenizer.Tokenize(line);
        if (tokens.Count == 0)
            return true;

        var commandName = NormalizeCommandName(tokens[0]);
        if (!Registry.TryGet(commandName, out var command, out _))
        {
            var suggestions = string.Join(", ", Registry.CompleteCommandName(commandName).Take(5));
            _logger.Warn(
                suggestions.Length == 0
                    ? $"Unknown command: {commandName}"
                    : $"Unknown command: {commandName}. Did you mean: {suggestions}");
            return false;
        }

        var args = tokens.Skip(1).ToArray();
        await command.ExecuteAsync(new CommandContext(_host, Registry, line, args, cancellationToken))
            .ConfigureAwait(false);
        return true;
    }

    public IReadOnlyList<string> Complete(string line)
    {
        var tokens = CommandLineTokenizer.Tokenize(line);
        if (tokens.Count <= 1 && !line.EndsWith(' '))
        {
            var slashPrefixed = line.StartsWith('/');
            var prefix = NormalizeCommandName(tokens.Count == 0 ? line : tokens[0]);
            return Registry.CompleteCommandName(prefix)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(name => slashPrefixed ? "/" + name : name)
                .ToArray();
        }

        var commandName = NormalizeCommandName(tokens[0]);
        if (!Registry.TryGet(commandName, out var command, out _))
            return [];

        var args = tokens.Skip(1).ToArray();
        return command.Complete(new CommandContext(_host, Registry, line, args)).ToArray();
    }

    private static string NormalizeCommandName(string commandName)
    {
        return commandName.Length > 1 && commandName[0] == '/'
            ? commandName[1..]
            : commandName;
    }
}
