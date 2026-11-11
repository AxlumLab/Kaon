namespace Kaon.Command;

public sealed record CommandDescriptor(
    string Name,
    string Description,
    string Usage,
    IReadOnlyList<string> Aliases);

public sealed class CommandRegistry
{
    private readonly Dictionary<string, CommandRegistration> _commands = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<CommandRegistration> _ordered = [];

    public IReadOnlyList<CommandDescriptor> Commands
        => _ordered.Select(static item => item.Descriptor).ToArray();

    public void Register<TCommand>(
        string name,
        string description = "",
        string usage = "",
        params string[] aliases)
        where TCommand : IConsoleCommand, new()
    {
        Register(name, static () => new TCommand(), description, usage, aliases);
    }

    public void Register(
        string name,
        Func<IConsoleCommand> factory,
        string description = "",
        string usage = "",
        params string[] aliases)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(factory);

        var descriptor = new CommandDescriptor(name, description, usage, aliases);
        var registration = new CommandRegistration(descriptor, factory);
        AddName(name, registration);
        foreach (var alias in aliases)
            AddName(alias, registration);

        _ordered.Add(registration);
    }

    public bool TryGet(string name, out IConsoleCommand command, out CommandDescriptor descriptor)
    {
        if (_commands.TryGetValue(name, out var registration))
        {
            command = registration.Factory();
            descriptor = registration.Descriptor;
            return true;
        }

        command = default!;
        descriptor = default!;
        return false;
    }

    public IEnumerable<string> CompleteCommandName(string prefix)
    {
        foreach (var registration in _ordered)
        {
            if (registration.Descriptor.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                yield return registration.Descriptor.Name;

            foreach (var alias in registration.Descriptor.Aliases)
            {
                if (alias.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    yield return alias;
            }
        }
    }

    private void AddName(string name, CommandRegistration registration)
    {
        if (!string.IsNullOrWhiteSpace(name))
            _commands[name] = registration;
    }

    private sealed record CommandRegistration(
        CommandDescriptor Descriptor,
        Func<IConsoleCommand> Factory);
}
