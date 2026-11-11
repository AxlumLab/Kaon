namespace Kaon.Command;

[AttributeUsage(AttributeTargets.Class)]
public sealed class ConsoleCommandAttribute(
    string name,
    string description = "",
    string usage = "",
    params string[] aliases) : Attribute
{
    public string Name { get; } = name;
    public string Description { get; } = description;
    public string Usage { get; } = usage;
    public IReadOnlyList<string> Aliases { get; } = aliases;
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class CommandMapperAttribute : Attribute;
