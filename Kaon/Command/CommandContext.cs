using Kaon.Logger;

namespace Kaon.Command;

public sealed class CommandContext(
    object host,
    CommandRegistry registry,
    string rawLine,
    IReadOnlyList<string> arguments,
    CancellationToken cancellationToken = default)
{
    public object Host { get; } = host;

    public CommandRegistry Registry { get; } = registry;

    public string RawLine { get; } = rawLine;

    public IReadOnlyList<string> Arguments { get; } = arguments;

    public CancellationToken CancellationToken { get; } = cancellationToken;

    public ILogger Logger { get; } = LogManager.GetLogger("Command");

    public T GetHost<T>()
        where T : class
        => Host as T ?? throw new InvalidOperationException($"Command host is not {typeof(T).FullName}.");

    public bool TryGetArgument(int index, out string value)
    {
        if ((uint)index < (uint)Arguments.Count)
        {
            value = Arguments[index];
            return true;
        }

        value = "";
        return false;
    }
}
