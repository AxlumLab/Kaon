using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NetGen;

public sealed class HostingAppReceiver : ISyntaxReceiver
{
    public List<ClassDeclarationSyntax> Apps { get; } = new();
    public List<ClassDeclarationSyntax> Handlers { get; } = new();
    public List<ClassDeclarationSyntax> Services { get; } = new();

    public void OnVisitSyntaxNode(SyntaxNode node)
    {
        if (node is not ClassDeclarationSyntax cls)
            return;

        foreach (var attr in cls.AttributeLists.SelectMany(static list => list.Attributes))
        {
            var name = attr.Name.ToString();
            if (name.Contains("HostingApp"))
                Apps.Add(cls);
            else if (name.Contains("HostingHandler"))
                Handlers.Add(cls);
            else if (name.Contains("HostingService"))
                Services.Add(cls);
        }
    }
}

public readonly struct HostingHandlerInfo(string handlerType, int order)
{
    public string HandlerType { get; } = handlerType;
    public int Order { get; } = order;
}

public readonly struct HostingServiceInfo(
    INamedTypeSymbol symbol,
    string startMode,
    string lifetime)
{
    public INamedTypeSymbol Symbol { get; } = symbol;
    public string StartMode { get; } = startMode;
    public string Lifetime { get; } = lifetime;
}

[Generator]
public sealed class HostingAppSourceGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForSyntaxNotifications(static () => new HostingAppReceiver());
    }

    public void Execute(GeneratorExecutionContext context)
    {
        if (context.SyntaxReceiver is not HostingAppReceiver receiver)
            return;

        var handlers = CollectHandlers(context, receiver.Handlers);
        var services = CollectServices(context, receiver.Services);

        foreach (var cls in receiver.Apps)
        {
            var model = context.Compilation.GetSemanticModel(cls.SyntaxTree);
            if (model.GetDeclaredSymbol(cls) is not INamedTypeSymbol appSymbol)
                continue;

            if (!HasAttribute(appSymbol, "HostingAppAttribute"))
                continue;

            var appType = GetTypeName(appSymbol);
            handlers.TryGetValue(appType, out var appHandlers);
            var source = GenerateProgram(appSymbol, appHandlers ?? [], services);
            context.AddSource($"{appSymbol.Name}.HostingProgram.g.cs", source);
        }
    }

    private static List<HostingServiceInfo> CollectServices(
        GeneratorExecutionContext context,
        IEnumerable<ClassDeclarationSyntax> serviceClasses)
    {
        var result = new List<HostingServiceInfo>();
        foreach (var cls in serviceClasses)
        {
            var model = context.Compilation.GetSemanticModel(cls.SyntaxTree);
            if (model.GetDeclaredSymbol(cls) is not INamedTypeSymbol serviceSymbol)
                continue;

            if (!HasAttribute(serviceSymbol, "HostingServiceAttribute"))
                continue;

            result.Add(new HostingServiceInfo(
                serviceSymbol,
                GetHostingServiceEnumArgument(serviceSymbol, 0, "Lazy"),
                GetHostingServiceEnumArgument(serviceSymbol, 1, "Singleton")));
        }

        return result;
    }

    private static Dictionary<string, List<HostingHandlerInfo>> CollectHandlers(
        GeneratorExecutionContext context,
        IEnumerable<ClassDeclarationSyntax> handlerClasses)
    {
        var result = new Dictionary<string, List<HostingHandlerInfo>>();
        foreach (var cls in handlerClasses)
        {
            var model = context.Compilation.GetSemanticModel(cls.SyntaxTree);
            if (model.GetDeclaredSymbol(cls) is not INamedTypeSymbol handlerSymbol)
                continue;

            if (!HasAttribute(handlerSymbol, "HostingHandlerAttribute"))
                continue;

            foreach (var appType in GetHandledAppTypes(handlerSymbol))
            {
                if (!result.TryGetValue(appType, out var list))
                {
                    list = new List<HostingHandlerInfo>();
                    result[appType] = list;
                }

                list.Add(new HostingHandlerInfo(GetTypeName(handlerSymbol), GetHandlerOrder(handlerSymbol)));
            }
        }

        foreach (var list in result.Values)
            list.Sort(static (left, right) => left.Order.CompareTo(right.Order));

        return result;
    }

    private static IEnumerable<string> GetHandledAppTypes(INamedTypeSymbol handlerSymbol)
    {
        foreach (var iface in handlerSymbol.AllInterfaces)
        {
            if (iface.Name != "IHostingAppHandler" || iface.TypeArguments.Length != 1)
                continue;

            yield return GetTypeName(iface.TypeArguments[0]);
        }
    }

    private static int GetHandlerOrder(INamedTypeSymbol handlerSymbol)
    {
        foreach (var attr in handlerSymbol.GetAttributes())
        {
            if (attr.AttributeClass?.Name != "HostingHandlerAttribute")
                continue;

            if (attr.ConstructorArguments.Length > 0 &&
                attr.ConstructorArguments[0].Value is int order)
            {
                return order;
            }
        }

        return 0;
    }

    private static string GenerateProgram(
        INamedTypeSymbol appSymbol,
        IReadOnlyList<HostingHandlerInfo> handlers,
        IReadOnlyList<HostingServiceInfo> services)
    {
        var ns = appSymbol.ContainingNamespace.IsGlobalNamespace
            ? ""
            : appSymbol.ContainingNamespace.ToDisplayString();
        var appType = GetTypeName(appSymbol);

        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable enable");
        builder.AppendLine("using Kaon.Logger;");
        builder.AppendLine();
        if (ns.Length > 0)
        {
            builder.Append("namespace ").Append(ns).AppendLine(";");
            builder.AppendLine();
        }

        builder.AppendLine("internal static class Program");
        builder.AppendLine("{");
        builder.AppendLine("    public static void Main(string[] args)");
        builder.AppendLine("    {");
        builder.AppendLine("        var logger = global::Kaon.Logger.LogManager.GetLogger(typeof(Program));");
        builder.Append("        ").Append(appType).AppendLine("? app = null;");

        for (var i = 0; i < handlers.Count; i++)
        {
            builder.Append("        global::Kaon.App.IHostingAppHandler<").Append(appType)
                .Append(">? handler").Append(i).AppendLine(" = null;");
            builder.AppendLine("        try");
            builder.AppendLine("        {");
            builder.Append("            handler").Append(i).Append(" = new ").Append(handlers[i].HandlerType).AppendLine("();");
            builder.AppendLine("        }");
            builder.AppendLine("        catch (global::System.Exception e)");
            builder.AppendLine("        {");
            builder.Append("            logger.Error(e, \"Failed to initialize hosting handler ").Append(EscapeString(handlers[i].HandlerType)).AppendLine(".\");");
            builder.AppendLine("            global::System.Environment.ExitCode = 1;");
            builder.AppendLine("        }");
        }

        builder.AppendLine("        try");
        builder.AppendLine("        {");
        builder.Append("            app = new ").Append(appType).AppendLine("(args);");
        builder.AppendLine("            GeneratedHostingServices.Register(app);");
        foreach (var index in Enumerable.Range(0, handlers.Count))
            builder.Append("            SafePreMain(handler").Append(index).AppendLine(", app, args, logger);");
        builder.AppendLine("            app.Run();");
        builder.AppendLine("        }");
        builder.AppendLine("        catch (global::System.Exception e)");
        builder.AppendLine("        {");
        builder.AppendLine("            logger.Fatal(e, \"Hosting app crashed.\");");
        builder.AppendLine("            global::System.Environment.ExitCode = 1;");
        builder.AppendLine("        }");
        builder.AppendLine("        finally");
        builder.AppendLine("        {");
        builder.AppendLine("            if (app != null)");
        builder.AppendLine("            {");
        builder.AppendLine("                app.EnsureStopped();");

        for (var i = handlers.Count - 1; i >= 0; i--)
            builder.Append("                SafePostMain(handler").Append(i).AppendLine(", app, args, logger);");

        builder.AppendLine("            }");
        builder.AppendLine("            global::Kaon.Logger.LogManager.Shutdown();");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.Append("    private static void SafePreMain(global::Kaon.App.IHostingAppHandler<")
            .Append(appType).Append(">? handler, ").Append(appType)
            .AppendLine(" app, string[] args, global::Kaon.Logger.ILogger logger)");
        builder.AppendLine("    {");
        builder.AppendLine("        if (handler == null)");
        builder.AppendLine("            return;");
        builder.AppendLine();
        builder.AppendLine("        try");
        builder.AppendLine("        {");
        builder.AppendLine("            handler.PreMain(app, args);");
        builder.AppendLine("        }");
        builder.AppendLine("        catch (global::System.Exception e)");
        builder.AppendLine("        {");
        builder.AppendLine("            logger.Error(e, \"Hosting handler PreMain failed.\");");
        builder.AppendLine("            global::System.Environment.ExitCode = 1;");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.Append("    private static void SafePostMain(global::Kaon.App.IHostingAppHandler<")
            .Append(appType).Append(">? handler, ").Append(appType)
            .AppendLine(" app, string[] args, global::Kaon.Logger.ILogger logger)");
        builder.AppendLine("    {");
        builder.AppendLine("        if (handler == null)");
        builder.AppendLine("            return;");
        builder.AppendLine();
        builder.AppendLine("        try");
        builder.AppendLine("        {");
        builder.AppendLine("            handler.PostMain(app, args);");
        builder.AppendLine("        }");
        builder.AppendLine("        catch (global::System.Exception e)");
        builder.AppendLine("        {");
        builder.AppendLine("            logger.Error(e, \"Hosting handler PostMain failed.\");");
        builder.AppendLine("            global::System.Environment.ExitCode = 1;");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        builder.AppendLine();
        GenerateServiceRegistration(builder, appSymbol, services);
        return builder.ToString();
    }

    private static void GenerateServiceRegistration(
        StringBuilder builder,
        INamedTypeSymbol appSymbol,
        IReadOnlyList<HostingServiceInfo> services)
    {
        var appType = GetTypeName(appSymbol);
        builder.AppendLine("file static class GeneratedHostingServices");
        builder.AppendLine("{");
        builder.Append("    public static void Register(").Append(appType).AppendLine(" app)");
        builder.AppendLine("    {");
        foreach (var service in SortServicesByDependencies(services))
        {
            var serviceType = GetTypeName(service.Symbol);
            builder.Append("        app.RegisterService<").Append(serviceType).AppendLine(">(");
            builder.Append("            static host => ");
            builder.Append("new ").Append(serviceType).Append('(')
                .Append(BuildConstructorArguments(appSymbol, service.Symbol))
                .AppendLine("),");
            builder.Append("            global::Kaon.App.HostingServiceStartMode.")
                .Append(service.StartMode).AppendLine(",");
            builder.Append("            global::Kaon.App.HostingServiceLifetime.")
                .Append(service.Lifetime).AppendLine(");");
        }

        builder.AppendLine("    }");
        builder.AppendLine("}");
    }

    private static string BuildConstructorArguments(
        INamedTypeSymbol appSymbol,
        INamedTypeSymbol serviceSymbol)
    {
        var ctor = serviceSymbol.InstanceConstructors
            .Where(static item => !item.IsStatic && item.DeclaredAccessibility == Accessibility.Public)
            .OrderByDescending(static item => item.Parameters.Length)
            .FirstOrDefault();

        if (ctor == null || ctor.Parameters.Length == 0)
            return "";

        return string.Join(", ", ctor.Parameters.Select(param => ResolveConstructorArgument(appSymbol, param.Type)));
    }

    private static string ResolveConstructorArgument(INamedTypeSymbol appSymbol, ITypeSymbol parameterType)
    {
        if (SymbolEqualityComparer.Default.Equals(parameterType, appSymbol))
            return $"({GetTypeName(appSymbol)})host";

        foreach (var member in appSymbol.GetMembers())
        {
            if (member.DeclaredAccessibility != Accessibility.Public)
                continue;

            ITypeSymbol? memberType = member switch
            {
                IFieldSymbol field => field.Type,
                IPropertySymbol property => property.Type,
                _ => null
            };

            if (memberType != null &&
                SymbolEqualityComparer.Default.Equals(memberType, parameterType))
            {
                return $"(({GetTypeName(appSymbol)})host).{member.Name}";
            }
        }

        if (HasAttribute(parameterType, "ConfigAttribute") ||
            parameterType.AllInterfaces.Any(static iface => iface.Name == "IConfig") ||
            parameterType.Name == "IConfig")
            return $"host.GetConfig<{GetTypeName(parameterType)}>()";

        return $"host.GetService<{GetTypeName(parameterType)}>()";
    }

    private static IReadOnlyList<HostingServiceInfo> SortServicesByDependencies(
        IReadOnlyList<HostingServiceInfo> services)
    {
        var result = new List<HostingServiceInfo>(services.Count);
        var pending = services.ToList();
        var registered = new HashSet<string>();

        while (pending.Count > 0)
        {
            var progressed = false;
            for (var i = pending.Count - 1; i >= 0; i--)
            {
                var service = pending[i];
                if (!GetServiceDependencies(service.Symbol, services).All(registered.Contains))
                    continue;

                result.Add(service);
                registered.Add(GetTypeName(service.Symbol));
                pending.RemoveAt(i);
                progressed = true;
            }

            if (progressed)
                continue;

            result.AddRange(pending);
            break;
        }

        return result;
    }

    private static IEnumerable<string> GetServiceDependencies(
        INamedTypeSymbol serviceSymbol,
        IReadOnlyList<HostingServiceInfo> services)
    {
        var serviceTypes = services
            .Select(static service => GetTypeName(service.Symbol))
            .ToHashSet();

        var ctor = serviceSymbol.InstanceConstructors
            .Where(static item => !item.IsStatic && item.DeclaredAccessibility == Accessibility.Public)
            .OrderByDescending(static item => item.Parameters.Length)
            .FirstOrDefault();

        if (ctor == null)
            yield break;

        foreach (var parameter in ctor.Parameters)
        {
            var typeName = GetTypeName(parameter.Type);
            if (serviceTypes.Contains(typeName))
                yield return typeName;
        }
    }

    private static string GetHostingServiceEnumArgument(
        INamedTypeSymbol serviceSymbol,
        int index,
        string defaultValue)
    {
        foreach (var attr in serviceSymbol.GetAttributes())
        {
            if (attr.AttributeClass?.Name != "HostingServiceAttribute")
                continue;

            if (attr.ConstructorArguments.Length <= index)
                return defaultValue;

            var arg = attr.ConstructorArguments[index];
            if (arg.Value is int enumValue)
            {
                return arg.Type?.Name switch
                {
                    "HostingServiceStartMode" => enumValue == 1 ? "Eager" : "Lazy",
                    "HostingServiceLifetime" => enumValue == 1 ? "Transient" : "Singleton",
                    _ => defaultValue
                };
            }

            var value = arg.Value?.ToString();
            if (string.IsNullOrWhiteSpace(value))
                return defaultValue;

            return value;
        }

        return defaultValue;
    }

    private static bool HasAttribute(ISymbol symbol, string attrName)
        => symbol.GetAttributes().Any(attr => attr.AttributeClass?.Name == attrName);

    private static string GetTypeName(ITypeSymbol type)
        => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    private static string EscapeString(string value)
        => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
