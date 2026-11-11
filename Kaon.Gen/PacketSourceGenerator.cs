using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NetGen;

public enum PacketType
{
    kNone,
    kServer,
    kClient
}

public readonly struct PacketInfo(PacketType type, int id, string className)
{
    public PacketType Type { get; } = type;
    public int Id { get; } = id;
    public string ClassName { get; } = className;
}


public sealed class PacketReceiver : ISyntaxReceiver
{
    public List<ClassDeclarationSyntax> Packets { get; } = [];
    public List<ClassDeclarationSyntax> Mappers { get; } = [];

    
    public void OnVisitSyntaxNode(SyntaxNode node)
    {
        if (node is not ClassDeclarationSyntax cls)
            return;
        
        foreach (var attrList in cls.AttributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                if (attr.Name.ToString().StartsWith("PacketMapper"))
                    Mappers.Add(cls);
                else if (attr.Name.ToString().StartsWith("Packet"))
                    Packets.Add(cls);
            }
        }
    }
}


[Generator]
public sealed class PacketSourceGenerator : ISourceGenerator
{
    private readonly Dictionary<int, PacketInfo> _clientPackets = [];
    private readonly Dictionary<int, PacketInfo> _serverPackets = [];
    private readonly List<string> _nameSpaces = [];
    
    public static readonly DiagnosticDescriptor DebugDescriptor =
        new(
            id: "NETGENDBG",
            title: "Kaon.Gen Debug",
            messageFormat: "{0}",
            category: "Kaon.Gen",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

    public void Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForSyntaxNotifications(() => new PacketReceiver());
    }

    public void Execute(GeneratorExecutionContext context)
    {
        _nameSpaces.Clear();
        _clientPackets.Clear();
        _serverPackets.Clear();
        
        if (context.SyntaxReceiver is not PacketReceiver receiver)
            return;

        var compilation = context.Compilation;

        foreach (var cls in receiver.Packets)
        {
            var model = compilation.GetSemanticModel(cls.SyntaxTree);

            foreach (var attrList in cls.AttributeLists)
            {
                foreach (var attr in attrList.Attributes)
                {
                    if (!attr.Name.ToString().StartsWith("Packet"))
                        continue;

                    if (attr.ArgumentList is null)
                        continue;

                    var args = attr.ArgumentList.Arguments;
                    if (args.Count < 2)
                        continue;

                    // -------- PacketId --------
                    var idConst = model.GetConstantValue(args[0].Expression);
                    if (!idConst.HasValue || idConst.Value is not int packetId)
                        continue;

                    // -------- PacketType (enum) --------
                    var typeConst = model.GetConstantValue(args[1].Expression);
                    if (!typeConst.HasValue || typeConst.Value is not byte enumValue)
                        continue;

                    var packetType = (PacketType)enumValue;

                    // -------- PacketName (optional string) --------
                    var packetName = cls.Identifier.Text;

                    var info = new PacketInfo(
                        packetType,
                        packetId,
                        packetName
                    );

                    if (model.GetDeclaredSymbol(cls) is INamedTypeSymbol symbol)
                    {
                        var nms = symbol.ContainingNamespace.IsGlobalNamespace
                            ? ""
                            : symbol.ContainingNamespace.ToDisplayString();
                        if (!_nameSpaces.Contains(nms) && nms != string.Empty)
                            _nameSpaces.Add(nms);
                    }

                    if (info.Type == PacketType.kClient)
                    {
                        if (_clientPackets.ContainsKey(info.Id))
                        {
                            context.ReportDiagnostic(Diagnostic.Create(
                                new DiagnosticDescriptor("NETGEN01", "Duplicated packetId", $"PacketId {packetId} duplicate", "Kaon.Gen", DiagnosticSeverity.Error, true), cls.Identifier.GetLocation()));
                        }
                        else
                        {
                            _clientPackets.Add(info.Id, info);
                        }
                    }
                    else
                    {
                        if (_serverPackets.ContainsKey(info.Id))
                        {
                            context.ReportDiagnostic(Diagnostic.Create(
                                new DiagnosticDescriptor("NETGEN01", "Duplicated packetId", $"PacketId {packetId} duplicate", "Kaon.Gen", DiagnosticSeverity.Error, true), cls.Identifier.GetLocation()));
                        }
                        else
                        {
                            _serverPackets.Add(info.Id, info);
                        }
                    }
                }
            }
        }

        var srcOfPackets = GeneratePacketRegistry();

        foreach (var mapper in receiver.Mappers)
        {
            CreateMapper(mapper, srcOfPackets, context);
        }
    }

    public string GeneratePacketRegistry()
    {
        var builder = new StringBuilder();
        foreach (var item in _clientPackets)
        {
            builder.AppendLine($"        reg.RegisterClient<{item.Value.ClassName}>({item.Key});");
        }
        builder.AppendLine("\n\n\n");
        foreach (var item in _serverPackets)
        {
            builder.AppendLine($"        reg.RegisterServer<{item.Value.ClassName}>({item.Key});");
        }

        return builder.ToString();
    }

    public void CreateMapper(ClassDeclarationSyntax mapperCls, string reg, GeneratorExecutionContext context)
    {
        var model = context.Compilation.GetSemanticModel(mapperCls.SyntaxTree);
        var ns = "";
        if (model.GetDeclaredSymbol(mapperCls) is INamedTypeSymbol symbol)
        {
            ns = symbol.ContainingNamespace.IsGlobalNamespace
                ? ""
                : symbol.ContainingNamespace.ToDisplayString();
        }
        var mapperName = mapperCls.Identifier.Text;

        var sb = new StringBuilder();

        sb.AppendLine("using Kaon.Protocol.Packet;");
        foreach (var nameSpace in _nameSpaces)
        {
            sb.AppendLine($"using {nameSpace};");
        }
        if (!string.IsNullOrEmpty(ns))
        {
            sb.AppendLine($"namespace {ns} {{");
        }

        sb.AppendLine($"public partial class {mapperName}");
        sb.AppendLine("{");
        sb.AppendLine("    public void RegisterPackets(PacketRegistry reg)");
        sb.AppendLine($"    {{ {reg} }}");
        sb.AppendLine("}");

        if (!string.IsNullOrEmpty(ns))
            sb.AppendLine("}");

        context.AddSource(
            $"{mapperName}.PacketMapper.g.cs",
            sb.ToString());
    }
    
    public static string GetNamespace(ClassDeclarationSyntax cls)
    {
        var node = cls.Parent;
        while (node != null)
        {
            if (node is NamespaceDeclarationSyntax ns)
                return ns.Name.ToString();
            node = node.Parent;
        }
        return "";
    }
}