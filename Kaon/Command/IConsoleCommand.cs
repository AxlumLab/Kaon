namespace Kaon.Command;

public interface IConsoleCommand
{
    ValueTask ExecuteAsync(CommandContext context);

    IEnumerable<string> Complete(CommandContext context)
        => [];
}

public interface ICommandMapper
{
    void RegisterCommands(CommandRegistry registry);
}
