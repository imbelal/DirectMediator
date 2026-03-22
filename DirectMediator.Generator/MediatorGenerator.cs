using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Text;
using System.Linq;
using System.Collections.Generic;

[Generator]
public class MediatorGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var compilationProvider = context.CompilationProvider;

        context.RegisterSourceOutput(compilationProvider, (spc, compilation) =>
        {
            // --------------------------
            // 1️⃣ Discover handlers
            // --------------------------
            var handlers = GetHandlers(compilation);

            // --------------------------
            // 2️⃣ Compile-time validation
            // --------------------------
            EmitDiagnostics(spc, handlers);

            // --------------------------
            // 3️⃣ Generate dispatcher + extension
            // --------------------------
            var source = Generate(handlers);
            spc.AddSource("DirectMediator.Generated.g.cs", SourceText.From(source, Encoding.UTF8));
        });
    }

    // -------------------------------
    // DISCOVERY
    // -------------------------------
    private List<HandlerInfo> GetHandlers(Compilation compilation)
    {
        var result = new List<HandlerInfo>();

        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);

            var classes = tree.GetRoot()
                .DescendantNodes()
                .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>();

            foreach (var cls in classes)
            {
                var symbol = model.GetDeclaredSymbol(cls) as INamedTypeSymbol;
                if (symbol == null) continue;

                foreach (var iface in symbol.AllInterfaces)
                {
                    if (iface.Name == "IRequestHandler" && iface.TypeArguments.Length == 2)
                    {
                        result.Add(new HandlerInfo
                        {
                            Handler = symbol,
                            Request = iface.TypeArguments[0],
                            Response = iface.TypeArguments[1],
                            Type = HandlerType.Request
                        });
                    }

                    if (iface.Name == "INotificationHandler" && iface.TypeArguments.Length == 1)
                    {
                        result.Add(new HandlerInfo
                        {
                            Handler = symbol,
                            Request = iface.TypeArguments[0],
                            Type = HandlerType.Notification
                        });
                    }
                }
            }
        }

        return result;
    }

    // -------------------------------
    // COMPILE-TIME VALIDATION
    // -------------------------------
    private void EmitDiagnostics(SourceProductionContext spc, List<HandlerInfo> handlers)
    {
        var requests = handlers.Where(h => h.Type == HandlerType.Request).ToList();
        var notifications = handlers.Where(h => h.Type == HandlerType.Notification).ToList();

        // Commands
        var commands = requests.Where(h => IsUnit(h.Response)).GroupBy(h => h.Request.ToDisplayString());
        foreach (var grp in commands)
            if (grp.Count() > 1)
                Report(spc, "FM001", $"Multiple handlers found for command '{grp.Key}'");

        // Queries
        var queries = requests.Where(h => !IsUnit(h.Response)).GroupBy(h => h.Request.ToDisplayString());
        foreach (var grp in queries)
            if (grp.Count() > 1)
                Report(spc, "FM002", $"Multiple handlers found for query '{grp.Key}'");

        // Notifications
        var notificationGroups = notifications.GroupBy(h => h.Request.ToDisplayString());
        foreach (var n in notificationGroups)
            if (!n.Any())
                Report(spc, "FM003", $"No handler found for notification '{n.Key}'");
    }

    private void Report(SourceProductionContext spc, string id, string message)
    {
        var diag = Diagnostic.Create(
            new DiagnosticDescriptor(id, id, message, "DirectMediator", DiagnosticSeverity.Error, true),
            Location.None);
        spc.ReportDiagnostic(diag);
    }

    // -------------------------------
    // CODE GENERATION
    // -------------------------------
    private string Generate(List<HandlerInfo> handlers)
    {
        var sb = new StringBuilder();

        var requests = handlers.Where(h => h.Type == HandlerType.Request).ToList();
        var commands = requests.Where(h => IsUnit(h.Response)).ToList();
        var queries = requests.Where(h => !IsUnit(h.Response)).ToList();
        var notifications = handlers.Where(h => h.Type == HandlerType.Notification).ToList();

        sb.AppendLine(@"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using DirectMediator;

namespace DirectMediator.Generated
{");

        GenerateCommandDispatcher(sb, commands);
        GenerateQueryDispatcher(sb, queries);
        GenerateNotificationPublisher(sb, notifications);
        GenerateMediator(sb, commands, queries, notifications);
        GenerateServiceCollectionExtension(sb, commands.Concat(queries).ToList(), notifications);

        sb.AppendLine("}"); // namespace
        return sb.ToString();
    }

    // -----------------------------------------------------------------------
    // SHARED: BuildPipeline static helper (emitted once inside each class)
    // Builds the delegate chain ONCE at construction time so there is zero
    // service-location or reflection overhead at dispatch time.
    // -----------------------------------------------------------------------
    private void AppendBuildPipelineHelper(StringBuilder sb)
    {
        sb.AppendLine(@"
// Builds the behavior pipeline as a delegate chain at construction time.
// First-registered behavior becomes the outermost wrapper (runs first / last).
private static System.Func<TReq, CancellationToken, Task<TResp>> BuildPipeline<TReq, TResp>(
    IRequestHandler<TReq, TResp> handler,
    IEnumerable<IPipelineBehavior<TReq, TResp>> behaviors)
    where TReq : IRequest<TResp>
{
    var list = new List<IPipelineBehavior<TReq, TResp>>(
        behaviors ?? Enumerable.Empty<IPipelineBehavior<TReq, TResp>>());
    System.Func<TReq, CancellationToken, Task<TResp>> chain = (req, ct) => handler.Handle(req, ct);
    for (var i = list.Count - 1; i >= 0; i--)
    {
        var b   = list[i];
        var inner = chain;
        chain = (req, ct) => b.Handle(req, ct, () => inner(req, ct));
    }
    return chain;
}");
    }

    // -------------------------------
    // COMMANDS
    // -------------------------------
    private void GenerateCommandDispatcher(StringBuilder sb, List<HandlerInfo> handlers)
    {
        sb.AppendLine("public sealed class CommandDispatcher : ICommandDispatcher");
        sb.AppendLine("{");

        // Handler fields
        foreach (var h in handlers)
            sb.AppendLine($"private readonly {h.Handler.ToDisplayString()} _{Camel(h.Handler.Name)};");

        // Pre-built pipeline delegate fields (one per command type)
        foreach (var h in handlers)
            sb.AppendLine($"private readonly System.Func<{h.Request.ToDisplayString()}, CancellationToken, Task<Unit>> _{Camel(h.Handler.Name)}Pipeline;");

        // Constructor: handlers + IEnumerable<IPipelineBehavior<TReq, TResp>> per handler
        var ctorParams = handlers
            .Select(h => $"{h.Handler.ToDisplayString()} {Camel(h.Handler.Name)}")
            .Concat(handlers.Select(h =>
                $"IEnumerable<IPipelineBehavior<{h.Request.ToDisplayString()}, Unit>> {Camel(h.Request.Name)}Behaviors = null"));
        sb.AppendLine($"public CommandDispatcher({string.Join(", ", ctorParams)}) {{");
        foreach (var h in handlers)
        {
            sb.AppendLine($"_{Camel(h.Handler.Name)} = {Camel(h.Handler.Name)};");
            sb.AppendLine($"_{Camel(h.Handler.Name)}Pipeline = BuildPipeline<{h.Request.ToDisplayString()}, Unit>({Camel(h.Handler.Name)}, {Camel(h.Request.Name)}Behaviors);");
        }
        sb.AppendLine("}");

        sb.AppendLine(@"
public Task Send<TCommand>(TCommand command, CancellationToken ct = default) where TCommand : ICommand
{
    return command switch
    {");
        foreach (var h in handlers)
            sb.AppendLine($"{h.Request.ToDisplayString()} c => (Task)_{Camel(h.Handler.Name)}Pipeline(c, ct),");
        sb.AppendLine(@"_ => throw new InvalidOperationException($""No handler found for command type '{(command is null ? typeof(TCommand) : command.GetType()).FullName}'"")};");
        sb.AppendLine("}");

        AppendBuildPipelineHelper(sb);
        sb.AppendLine("}");
    }

    // -------------------------------
    // QUERIES
    // -------------------------------
    private void GenerateQueryDispatcher(StringBuilder sb, List<HandlerInfo> handlers)
    {
        sb.AppendLine("public sealed class QueryDispatcher : IQueryDispatcherMarker");
        sb.AppendLine("{");

        // Handler fields
        foreach (var h in handlers)
            sb.AppendLine($"private readonly {h.Handler.ToDisplayString()} _{Camel(h.Handler.Name)};");

        // Pre-built pipeline delegate fields
        foreach (var h in handlers)
            sb.AppendLine($"private readonly System.Func<{h.Request.ToDisplayString()}, CancellationToken, Task<{h.Response.ToDisplayString()}>> _{Camel(h.Handler.Name)}Pipeline;");

        // Constructor
        var ctorParams = handlers
            .Select(h => $"{h.Handler.ToDisplayString()} {Camel(h.Handler.Name)}")
            .Concat(handlers.Select(h =>
                $"IEnumerable<IPipelineBehavior<{h.Request.ToDisplayString()}, {h.Response.ToDisplayString()}>> {Camel(h.Request.Name)}Behaviors = null"));
        sb.AppendLine($"public QueryDispatcher({string.Join(", ", ctorParams)}) {{");
        foreach (var h in handlers)
        {
            sb.AppendLine($"_{Camel(h.Handler.Name)} = {Camel(h.Handler.Name)};");
            sb.AppendLine($"_{Camel(h.Handler.Name)}Pipeline = BuildPipeline<{h.Request.ToDisplayString()}, {h.Response.ToDisplayString()}>({Camel(h.Handler.Name)}, {Camel(h.Request.Name)}Behaviors);");
        }
        sb.AppendLine("}");

        foreach (var h in handlers)
            sb.AppendLine($@"
public Task<{h.Response.ToDisplayString()}> Query({h.Request.ToDisplayString()} query, CancellationToken ct = default)
    => _{Camel(h.Handler.Name)}Pipeline(query, ct);");

        AppendBuildPipelineHelper(sb);
        sb.AppendLine("}");
    }

    // -------------------------------
    // NOTIFICATIONS
    // -------------------------------
    private void GenerateNotificationPublisher(StringBuilder sb, List<HandlerInfo> handlers)
    {
        sb.AppendLine("public sealed class NotificationPublisher : INotificationPublisher");
        sb.AppendLine("{");

        foreach (var h in handlers)
            sb.AppendLine($"private readonly {h.Handler.ToDisplayString()} _{Camel(h.Handler.Name)};");

        sb.AppendLine("public NotificationPublisher(" +
            string.Join(", ", handlers.Select(h => $"{h.Handler.ToDisplayString()} {Camel(h.Handler.Name)}")) +
            ") {");
        foreach (var h in handlers)
            sb.AppendLine($"_{Camel(h.Handler.Name)} = {Camel(h.Handler.Name)};");
        sb.AppendLine("}");

        sb.AppendLine(@"
public async Task Publish<TNotification>(TNotification notification, CancellationToken ct = default) where TNotification : INotification
{
    switch (notification)
    {");
        foreach (var group in handlers.GroupBy(h => h.Request.ToDisplayString()))
        {
            sb.AppendLine($"case {group.Key} n:");
            foreach (var h in group)
                sb.AppendLine($"    await _{Camel(h.Handler.Name)}.Handle(n, ct);");
            sb.AppendLine("    break;");
        }
        sb.AppendLine(@"default: throw new InvalidOperationException($""No handlers found for notification type '{notification?.GetType().FullName ?? typeof(TNotification).FullName ?? typeof(TNotification).Name}'""); }");
        sb.AppendLine("}");
        sb.AppendLine("}");
    }

    // -------------------------------
    // UNIFIED MEDIATOR
    // -------------------------------
    private void GenerateMediator(StringBuilder sb, List<HandlerInfo> commands, List<HandlerInfo> queries, List<HandlerInfo> notifications)
    {
        var allRequestHandlers = commands.Concat(queries).ToList();

        sb.AppendLine("public sealed class Mediator : IMediator");
        sb.AppendLine("{");

        // Handler fields
        foreach (var h in allRequestHandlers)
            sb.AppendLine($"private readonly {h.Handler.ToDisplayString()} _{Camel(h.Handler.Name)};");
        foreach (var h in notifications)
            sb.AppendLine($"private readonly {h.Handler.ToDisplayString()} _{Camel(h.Handler.Name)};");

        // Pipeline delegate fields (one per request type)
        foreach (var h in allRequestHandlers)
            sb.AppendLine($"private readonly System.Func<{h.Request.ToDisplayString()}, CancellationToken, Task<{h.Response.ToDisplayString()}>> _{Camel(h.Handler.Name)}Pipeline;");

        // Constructor: all handlers + IEnumerable<IPipelineBehavior> per request type
        var ctorParams = allRequestHandlers
            .Concat(notifications)
            .Select(h => $"{h.Handler.ToDisplayString()} {Camel(h.Handler.Name)}")
            .Concat(allRequestHandlers.Select(h =>
                $"IEnumerable<IPipelineBehavior<{h.Request.ToDisplayString()}, {h.Response.ToDisplayString()}>> {Camel(h.Request.Name)}Behaviors = null"));
        sb.AppendLine($"public Mediator({string.Join(", ", ctorParams)}) {{");
        foreach (var h in allRequestHandlers.Concat(notifications))
            sb.AppendLine($"_{Camel(h.Handler.Name)} = {Camel(h.Handler.Name)};");
        foreach (var h in allRequestHandlers)
            sb.AppendLine($"_{Camel(h.Handler.Name)}Pipeline = BuildPipeline<{h.Request.ToDisplayString()}, {h.Response.ToDisplayString()}>({Camel(h.Handler.Name)}, {Camel(h.Request.Name)}Behaviors);");
        sb.AppendLine("}");

        // IMediator.Send<TResponse>
        sb.AppendLine(@"
public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
{
    return request switch
    {");
        foreach (var h in allRequestHandlers)
            sb.AppendLine($"{h.Request.ToDisplayString()} r => (Task<TResponse>)(object)_{Camel(h.Handler.Name)}Pipeline(r, ct),");
        sb.AppendLine(@"_ => throw new InvalidOperationException($""No handler found for request type '{request?.GetType().FullName ?? typeof(IRequest<TResponse>).FullName}'"")};");
        sb.AppendLine("}");

        // INotificationPublisher.Publish
        sb.AppendLine(@"
public async Task Publish<TNotification>(TNotification notification, CancellationToken ct = default) where TNotification : INotification
{
    switch (notification)
    {");
        foreach (var group in notifications.GroupBy(h => h.Request.ToDisplayString()))
        {
            sb.AppendLine($"case {group.Key} n:");
            foreach (var h in group)
                sb.AppendLine($"    await _{Camel(h.Handler.Name)}.Handle(n, ct);");
            sb.AppendLine("    break;");
        }
        sb.AppendLine(@"default: throw new InvalidOperationException($""No handlers found for notification type '{notification?.GetType().FullName ?? typeof(TNotification).FullName ?? typeof(TNotification).Name}'""); }");
        sb.AppendLine("}");

        AppendBuildPipelineHelper(sb);
        sb.AppendLine("}");
    }

    // -------------------------------
    // EXTENSIONS
    // -------------------------------
    private void GenerateServiceCollectionExtension(StringBuilder sb,
    List<HandlerInfo> requestHandlers, List<HandlerInfo> notificationHandlers)
    {
        sb.AppendLine(@"

        public static class DirectMediatorServiceCollectionExtensions
        {
            public static IServiceCollection AddDirectMediator(this IServiceCollection services)
            {");

        // De-duplicate handlers that appear in both request and notification lists
        var allHandlers = requestHandlers.Concat(notificationHandlers)
            .GroupBy(h => h.Handler.ToDisplayString())
            .Select(g => g.First());
        foreach (var h in allHandlers)
            sb.AppendLine($"            services.AddTransient<{h.Handler.ToDisplayString()}>();");

        sb.AppendLine(@"
                services.AddSingleton<CommandDispatcher>();
                services.AddSingleton<QueryDispatcher>();
                services.AddSingleton<NotificationPublisher>();
                services.AddSingleton<IMediator, Mediator>();

                return services;
            }
        }");
    }

    // -------------------------------
    // HELPERS
    // -------------------------------
    private string Camel(string name) => char.ToLowerInvariant(name[0]) + name.Substring(1);
    private bool IsUnit(ITypeSymbol type) => type?.Name == "Unit";

    private class HandlerInfo
    {
        public INamedTypeSymbol Handler { get; set; }
        public ITypeSymbol Request { get; set; }
        public ITypeSymbol Response { get; set; }
        public HandlerType Type { get; set; }
    }

    private enum HandlerType { Request, Notification }
}