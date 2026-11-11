using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NetGen;

public sealed class CommandReceiver : ISyntaxReceiver
{
    public List<ClassDeclarationSyntax> Commands { get; } = [];
    public List<ClassDeclarationSyntax> Mappers { get; } = [];

    public void OnVisitSyntaxNode(SyntaxNode node)
    {
        if (node is not ClassDeclarationSyntax cls)
            return;

        foreach (var attr in cls.AttributeLists.SelectMany(static x => x.Attributes))
        {
            var name = attr.Name.ToString();
            if (name.Contains("ConsoleCommand"))
                Commands.Add(cls);
            else if (name.Contains("CommandMapper"))
                Mappers.Add(cls);
        }
    }
}

[Generator]
public sealed class CommandSourceGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForSyntaxNotifications(static () => new CommandReceiver());
    }

    public void Execute(GeneratorExecutionContext context)
    {
        if (context.SyntaxReceiver is not CommandReceiver receiver)
            return;

        var commands = CollectCommands(context, receiver.Commands);
        foreach (var mapper in receiver.Mappers)
            GenerateMapper(context, mapper, commands);
    }

    private static List<CommandInfo> CollectCommands(
        GeneratorExecutionContext context,
        IEnumerable<ClassDeclarationSyntax> candidates)
    {
        var commands = new List<CommandInfo>();

        foreach (var cls in candidates)
        {
            var model = context.Compilation.GetSemanticModel(cls.SyntaxTree);
            if (model.GetDeclaredSymbol(cls) is not INamedTypeSymbol symbol)
                continue;

            var attr = symbol.GetAttributes()
                .FirstOrDefault(static x => x.AttributeClass?.Name == "ConsoleCommandAttribute");
            if (attr == null)
                continue;

            var name = attr.ConstructorArguments.Length > 0
                ? attr.ConstructorArguments[0].Value?.ToString() ?? symbol.Name
                : symbol.Name;
            var description = attr.ConstructorArguments.Length > 1
                ? attr.ConstructorArguments[1].Value?.ToString() ?? ""
                : "";
            var usage = attr.ConstructorArguments.Length > 2
                ? attr.ConstructorArguments[2].Value?.ToString() ?? ""
                : "";
            var aliases = attr.ConstructorArguments.Length > 3
                ? attr.ConstructorArguments[3].Values
                    .Select(static x => x.Value?.ToString() ?? "")
                    .Where(static x => x.Length > 0)
                    .ToArray()
                : [];

            commands.Add(new CommandInfo(
                symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                name,
                description,
                usage,
                aliases));
        }

        return commands;
    }

    private static void GenerateMapper(
        GeneratorExecutionContext context,
        ClassDeclarationSyntax cls,
        IReadOnlyList<CommandInfo> commands)
    {
        var model = context.Compilation.GetSemanticModel(cls.SyntaxTree);
        if (model.GetDeclaredSymbol(cls) is not INamedTypeSymbol symbol)
            return;

        var ns = symbol.ContainingNamespace.IsGlobalNamespace
            ? ""
            : symbol.ContainingNamespace.ToDisplayString();

        var builder = new StringBuilder();
        if (ns.Length > 0)
            builder.Append("namespace ").Append(ns).AppendLine(";");

        builder.Append("public partial class ").Append(symbol.Name)
            .AppendLine(" : global::Kaon.Command.ICommandMapper");
        builder.AppendLine("{");
        builder.AppendLine("    public void RegisterCommands(global::Kaon.Command.CommandRegistry registry)");
        builder.AppendLine("    {");

        foreach (var command in commands)
        {
            builder.Append("        registry.Register<").Append(command.TypeName).Append(">(");
            AppendString(builder, command.Name);
            builder.Append(", ");
            AppendString(builder, command.Description);
            builder.Append(", ");
            AppendString(builder, command.Usage);
            foreach (var alias in command.Aliases)
            {
                builder.Append(", ");
                AppendString(builder, alias);
            }
            builder.AppendLine(");");
        }

        builder.AppendLine("    }");
        builder.AppendLine("}");

        context.AddSource($"{symbol.Name}.CommandMapper.g.cs", builder.ToString());
    }

    private static void AppendString(StringBuilder builder, string value)
    {
        builder.Append('"').Append(value.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"');
    }

    private readonly record struct CommandInfo(
        string TypeName,
        string Name,
        string Description,
        string Usage,
        IReadOnlyList<string> Aliases);
}
