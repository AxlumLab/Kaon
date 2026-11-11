using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NetGen;

public enum FieldType : byte
{
    kUnknown = 0,
    kString,
    kBool,
    kInt32,
    kInt64,
    kByte,
    kSByte,
    kFloat,
    kDouble,
    kShort,
    kUShort,
    kVarInt,
    kVarLong,
    kComponent
}

public readonly struct PacketFieldInfo(FieldType type, bool nullable, string className, int order, int sourceOrder)
{
    public FieldType Type { get; } = type;
    public bool Nullable { get; } = nullable;
    public string ClassName { get; } = className;
    public int Order { get; } = order;
    public int SourceOrder { get; } = sourceOrder;
}

public readonly struct PacketMetadata(int id, PacketType type, byte version)
{
    public int Id { get; } = id;
    public PacketType Type { get; } = type;
    public byte Version { get; } = version;
}

public sealed class PacketFieldReceiver : ISyntaxReceiver
{
    public List<ClassDeclarationSyntax> Candidates { get; } = [];

    public void OnVisitSyntaxNode(SyntaxNode node)
    {
        if (node is not ClassDeclarationSyntax cls)
            return;

        if (!cls.AttributeLists
                .SelectMany(a => a.Attributes)
                .Any(a => a.Name.ToString().Contains("Packet")))
            return;

        if (cls.Members.Any(member => member.AttributeLists.Count > 0))
        {
            Candidates.Add(cls);
        }
    }
}


[Generator]
public sealed class PacketSerializeGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForSyntaxNotifications(() => new PacketFieldReceiver());
    }

    public void Execute(GeneratorExecutionContext context)
    {
        if (context.SyntaxReceiver is not PacketFieldReceiver receiver)
            return;

        foreach (var cls in receiver.Candidates)
        {
            var model = context.Compilation.GetSemanticModel(cls.SyntaxTree);
            if (model.GetDeclaredSymbol(cls) is not INamedTypeSymbol classSymbol)
                continue;

            if (!HasAttribute(classSymbol, "PacketAttribute"))
                continue;

            var metadata = GetPacketMetadata(classSymbol);
            var fields = CollectPacketFields(classSymbol);
            //
            // context.ReportDiagnostic(
            //     Diagnostic.Create(
            //         DebugDescriptor,
            //         cls.Identifier.GetLocation(),
            //         $"Packet {classSymbol.Name}, Fields = {fields.Count}"));
            //
            var ns = "";
            if (model.GetDeclaredSymbol(cls) is INamedTypeSymbol symbol)
            {
                ns = symbol.ContainingNamespace.IsGlobalNamespace
                    ? ""
                    : symbol.ContainingNamespace.ToDisplayString();
            }
            var mapperName = cls.Identifier.Text;

            var sb = new StringBuilder();

            sb.AppendLine("using Kaon.Protocol.Packet;");
            sb.AppendLine("using Kaon.IO;");
            if (!string.IsNullOrEmpty(ns))
            {
                sb.AppendLine($"namespace {ns} {{");
            }

            sb.AppendLine($"public partial class {mapperName}");
            sb.AppendLine("{");
            sb.AppendLine($"    public int PacketId => {metadata.Id};");
            sb.AppendLine($"    public PacketType PacketType => PacketType.{metadata.Type};");
            sb.AppendLine($"    public byte PacketVersion => {metadata.Version};");
            sb.AppendLine("    public void OnWrite(PackedStream stream){");
            sb.AppendLine(GenerateOnWrite(ref fields, cls, context));
            sb.AppendLine("}");
            sb.AppendLine("    public void OnRead(PackedStream stream){");
            sb.AppendLine(GenerateOnRead(ref fields, cls, context));
            sb.AppendLine("}");
            sb.AppendLine("}");
            
            if (!string.IsNullOrEmpty(ns))
                sb.AppendLine("}");

            context.AddSource(
                $"{mapperName}.PacketSerialize.g.cs",
                sb.ToString());
        }
    }

    public string GenerateOnWrite(ref List<PacketFieldInfo> fields, ClassDeclarationSyntax cls, GeneratorExecutionContext ctx)
    {
        var builder = new StringBuilder();
        foreach (var fid in fields)
        {
            if (fid.Nullable)
            {
                builder.AppendLine($"stream.WriteBoolean(this.{fid.ClassName} == null);");
                builder.AppendLine($"if (this.{fid.ClassName} != null) {{");
            }

            switch (fid.Type)
            {
                case FieldType.kUnknown:
                    break;
                case FieldType.kString:
                    builder.AppendLine($"stream.WriteString(this.{fid.ClassName});");
                    break;
                case FieldType.kBool:
                    builder.AppendLine($"stream.WriteBoolean(this.{fid.ClassName});");
                    break;
                case FieldType.kInt32:
                    builder.AppendLine($"stream.WriteInt32(this.{fid.ClassName});");
                    break;
                case FieldType.kInt64:
                    builder.AppendLine($"stream.WriteInt64(this.{fid.ClassName});");
                    break;
                case FieldType.kByte:
                    builder.AppendLine($"stream.WriteByte(this.{fid.ClassName});");
                    break;
                case FieldType.kSByte:
                    builder.AppendLine($"stream.WriteSByte(this.{fid.ClassName});");
                    break;
                case FieldType.kFloat:
                    builder.AppendLine($"stream.WriteFloat(this.{fid.ClassName});");
                    break;
                case FieldType.kDouble:
                    builder.AppendLine($"stream.WriteDouble(this.{fid.ClassName});");
                    break;
                case FieldType.kShort:
                    builder.AppendLine($"stream.WriteShort(this.{fid.ClassName});");
                    break;
                case FieldType.kUShort:
                    builder.AppendLine($"stream.WriteUShort(this.{fid.ClassName});");
                    break;
                case FieldType.kVarInt:
                    builder.AppendLine($"stream.WriteVarInt(this.{fid.ClassName});");
                    break;
                case FieldType.kVarLong:
                    builder.AppendLine($"stream.WriteVarLong(this.{fid.ClassName});");
                    break;
                case FieldType.kComponent:
                    builder.AppendLine($"this.{fid.ClassName}.OnSerialize(stream);");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (fid.Nullable)
                builder.AppendLine("}");
        }

        return builder.ToString();
    }
    
    public string GenerateOnRead(ref List<PacketFieldInfo> fields, ClassDeclarationSyntax cls, GeneratorExecutionContext ctx)
    {
        var builder = new StringBuilder();
        foreach (var fid in fields)
        {
            if (fid.Nullable)
            {
                builder.AppendLine("if(!stream.ReadBoolean()) {");
            }

            switch (fid.Type)
            {
                case FieldType.kUnknown:
                    break;
                case FieldType.kString:
                    builder.AppendLine($"this.{fid.ClassName} = stream.ReadString();");
                    break;
                case FieldType.kBool:
                    builder.AppendLine($"this.{fid.ClassName} = stream.ReadBoolean();");
                    break;
                case FieldType.kInt32:
                    builder.AppendLine($"this.{fid.ClassName} = stream.ReadInt32();");
                    break;
                case FieldType.kInt64:
                    builder.AppendLine($"this.{fid.ClassName} = stream.ReadInt64();");
                    break;
                case FieldType.kByte:
                    builder.AppendLine($"this.{fid.ClassName} = stream.ReadByte();");
                    break;
                case FieldType.kSByte:
                    builder.AppendLine($"this.{fid.ClassName} = stream.ReadSByte();");
                    break;
                case FieldType.kFloat:
                    builder.AppendLine($"this.{fid.ClassName} = stream.ReadFloat();");
                    break;
                case FieldType.kDouble:
                    builder.AppendLine($"this.{fid.ClassName} = stream.ReadDouble();");
                    break;
                case FieldType.kShort:
                    builder.AppendLine($"this.{fid.ClassName} = stream.ReadShort();");
                    break;
                case FieldType.kUShort:
                    builder.AppendLine($"this.{fid.ClassName} = stream.ReadUShort();");
                    break;
                case FieldType.kVarInt:
                    builder.AppendLine($"this.{fid.ClassName} = stream.ReadVarInt();");
                    break;
                case FieldType.kVarLong:
                    builder.AppendLine($"this.{fid.ClassName} = stream.ReadVarLong();");
                    break;
                case FieldType.kComponent:
                    builder.AppendLine($"this.{fid.ClassName}.OnDeSerialize(stream);");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
            if (fid.Nullable)
            {
                builder.AppendLine("}");
            }
        }

        return builder.ToString();
    }

    private static List<PacketFieldInfo> CollectPacketFields(INamedTypeSymbol classSymbol)
    {
        var list = new List<PacketFieldInfo>();

        foreach (var member in classSymbol.GetMembers())
        {
            if (member is IPropertySymbol prop)
            {
                if (TryGetPacketField(prop, out var info))
                    list.Add(info);
            }
            else if (member is IFieldSymbol field)
            {
                if (TryGetPacketField(field, out var info))
                    list.Add(info);
            }
        }

        return list
            .OrderBy(static item => item.Order < 0 ? 1 : 0)
            .ThenBy(static item => item.Order < 0 ? item.SourceOrder : item.Order)
            .ToList();
    }

    private static bool TryGetPacketField(
        ISymbol symbol,
        out PacketFieldInfo info)
    {
        info = default;

        foreach (var attr in symbol.GetAttributes())
        {
            if (attr.AttributeClass?.Name != "PacketFieldAttribute")
                continue;

            if (attr.ConstructorArguments.Length < 1)
                return false;

            var argIndex = 0;
            var order = -1;
            if (attr.ConstructorArguments.Length > 1 &&
                attr.ConstructorArguments[0].Value is int configuredOrder)
            {
                order = configuredOrder;
                argIndex = 1;
            }

            var typeValue = attr.ConstructorArguments[argIndex].Value;
            var fieldType = typeValue is null
                ? FieldType.kUnknown
                : (FieldType)Convert.ToInt32(typeValue);

            bool nullable = false;
            if (attr.ConstructorArguments.Length > argIndex + 1 &&
                attr.ConstructorArguments[argIndex + 1].Value is bool b)
            {
                nullable = b;
            }
            foreach (var namedArg in attr.NamedArguments)
            {
                if (namedArg.Key == "Nullable" &&
                    namedArg.Value.Value is bool namedNullable)
                {
                    nullable = namedNullable;
                }
            }

            info = new PacketFieldInfo(
                fieldType,
                nullable,
                symbol.Name,
                order,
                symbol.Locations.FirstOrDefault()?.SourceSpan.Start ?? int.MaxValue);

            return true;
        }

        return false;
    }

    private static PacketMetadata GetPacketMetadata(INamedTypeSymbol symbol)
    {
        foreach (var attr in symbol.GetAttributes())
        {
            if (attr.AttributeClass?.Name != "PacketAttribute")
                continue;

            var id = attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is int packetId
                ? packetId
                : 0;
            var type = attr.ConstructorArguments.Length > 1 && attr.ConstructorArguments[1].Value is byte packetType
                ? (PacketType)packetType
                : PacketType.kNone;
            var version = attr.ConstructorArguments.Length > 3 && attr.ConstructorArguments[3].Value is byte packetVersion
                ? packetVersion
                : (byte)1;

            foreach (var namedArg in attr.NamedArguments)
            {
                if (namedArg.Key == "version" && namedArg.Value.Value is byte namedVersion)
                    version = namedVersion;
            }

            return new PacketMetadata(id, type, version);
        }

        return new PacketMetadata(0, PacketType.kNone, 1);
    }

    private static bool HasAttribute(ISymbol symbol, string attrName)
    {
        return symbol.GetAttributes()
            .Any(a => a.AttributeClass?.Name == attrName);
    }

    private static readonly DiagnosticDescriptor DebugDescriptor =
        new DiagnosticDescriptor(
            "NETGENDBG",
            "Kaon.Gen Debug",
            "{0}",
            "Kaon.Gen",
            DiagnosticSeverity.Info,
            isEnabledByDefault: true);

}
