using DirectMediator;
using DirectMediator.Generated;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Reuse TestCommand/TestCommandHandler (CommandDispatcherTests.cs) and
// TestQuery/TestQueryHandler (QueryDispatcherTests.cs).
// Declare a dedicated notification type to avoid conflicts with TestNotification.
public record PipelineNotification(string Value) : INotification;
public class PipelineNotificationHandler : INotificationHandler<PipelineNotification>
{
    private readonly Action _callback;
    public PipelineNotificationHandler(Action callback) => _callback = callback;
    public Task Handle(PipelineNotification notification, CancellationToken cancellationToken)
    {
        _callback();
        return Task.CompletedTask;
    }
}

// --- Test-only pipeline behavior implementations (renamed to avoid clash with built-in LoggingBehavior) ---
public class TrackingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly List<string> _log;
    public TrackingBehavior(List<string> log) => _log = log;

    public async Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken, RequestHandlerDelegate<TResponse> next)
    {
        _log.Add($"before:{typeof(TRequest).Name}");
        var result = await next();
        _log.Add($"after:{typeof(TRequest).Name}");
        return result;
    }
}

public class OrderTrackingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly List<string> _log;
    private readonly string _name;
    public OrderTrackingBehavior(List<string> log, string name) { _log = log; _name = name; }

    public async Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken, RequestHandlerDelegate<TResponse> next)
    {
        _log.Add($"{_name}:before");
        var result = await next();
        _log.Add($"{_name}:after");
        return result;
    }
}

public class PipelineBehaviorTests
{
    [Fact]
    public async Task CommandDispatcher_PipelineBehavior_IsInvoked()
    {
        var log = new List<string>();

        var services = new ServiceCollection();
        services.AddTransient<TestCommandHandler>(sp =>
            new TestCommandHandler(() => log.Add("handler")));
        services.AddSingleton<IPipelineBehavior<TestCommand, Unit>>(
            new TrackingBehavior<TestCommand, Unit>(log));
        services.AddSingleton<CommandDispatcher>();

        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<CommandDispatcher>();

        await dispatcher.Send(new TestCommand("X"), default);

        Assert.Equal(new[] { "before:TestCommand", "handler", "after:TestCommand" }, log);
    }

    [Fact]
    public async Task QueryDispatcher_PipelineBehavior_IsInvoked()
    {
        var log = new List<string>();

        var services = new ServiceCollection();
        services.AddTransient<TestQueryHandler>();
        services.AddSingleton<IPipelineBehavior<TestQuery, string>>(
            new TrackingBehavior<TestQuery, string>(log));
        services.AddSingleton<QueryDispatcher>();

        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<QueryDispatcher>();

        var result = await dispatcher.Query(new TestQuery(7), default);

        Assert.Equal("Result 7", result);
        Assert.Equal(new[] { "before:TestQuery", "after:TestQuery" }, log);
    }

    [Fact]
    public async Task PipelineBehaviors_ExecuteInRegistrationOrder()
    {
        var log = new List<string>();

        var services = new ServiceCollection();
        services.AddTransient<TestCommandHandler>(sp =>
            new TestCommandHandler(() => log.Add("handler")));
        services.AddSingleton<IPipelineBehavior<TestCommand, Unit>>(new OrderTrackingBehavior<TestCommand, Unit>(log, "first"));
        services.AddSingleton<IPipelineBehavior<TestCommand, Unit>>(new OrderTrackingBehavior<TestCommand, Unit>(log, "second"));
        services.AddSingleton<CommandDispatcher>();

        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<CommandDispatcher>();

        await dispatcher.Send(new TestCommand("X"), default);

        // first → second → handler → second → first
        Assert.Equal(new[] { "first:before", "second:before", "handler", "second:after", "first:after" }, log);
    }

    [Fact]
    public async Task Mediator_Send_Command_PipelineBehavior_IsInvoked()
    {
        var log = new List<string>();

        var services = new ServiceCollection();
        services.AddTransient<TestCommandHandler>(sp =>
            new TestCommandHandler(() => log.Add("handler")));
        services.AddTransient<TestQueryHandler>();
        services.AddTransient<TestNotificationHandlerA>(sp =>
            new TestNotificationHandlerA(() => { }));
        services.AddTransient<TestNotificationHandlerB>(sp =>
            new TestNotificationHandlerB(() => { }));
        services.AddTransient<PipelineNotificationHandler>(sp =>
            new PipelineNotificationHandler(() => { }));
        services.AddSingleton<IPipelineBehavior<TestCommand, Unit>>(
            new TrackingBehavior<TestCommand, Unit>(log));
        services.AddSingleton<IMediator, Mediator>();

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        await mediator.Send(new TestCommand("X"));

        Assert.Equal(new[] { "before:TestCommand", "handler", "after:TestCommand" }, log);
    }

    [Fact]
    public async Task Mediator_Send_Query_PipelineBehavior_IsInvoked()
    {
        var log = new List<string>();

        var services = new ServiceCollection();
        services.AddTransient<TestCommandHandler>(sp =>
            new TestCommandHandler(() => { }));
        services.AddTransient<TestQueryHandler>();
        services.AddTransient<TestNotificationHandlerA>(sp =>
            new TestNotificationHandlerA(() => { }));
        services.AddTransient<TestNotificationHandlerB>(sp =>
            new TestNotificationHandlerB(() => { }));
        services.AddTransient<PipelineNotificationHandler>(sp =>
            new PipelineNotificationHandler(() => { }));
        services.AddSingleton<IPipelineBehavior<TestQuery, string>>(
            new TrackingBehavior<TestQuery, string>(log));
        services.AddSingleton<IMediator, Mediator>();

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new TestQuery(42));

        Assert.Equal("Result 42", result);
        Assert.Equal(new[] { "before:TestQuery", "after:TestQuery" }, log);
    }

    // --- Built-in behavior tests ---

    [Fact]
    public async Task LoggingBehavior_LogsRequestAndResponse()
    {
        var log = new List<string>();
        var logger = new CapturingLogger<TestCommand>(log);

        var services = new ServiceCollection();
        services.AddTransient<TestCommandHandler>(sp =>
            new TestCommandHandler(() => { }));
        services.AddSingleton<IPipelineBehavior<TestCommand, Unit>>(
            new LoggingBehavior<TestCommand, Unit>(logger));
        services.AddSingleton<CommandDispatcher>();

        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<CommandDispatcher>();

        await dispatcher.Send(new TestCommand("hello"), default);

        Assert.Contains(log, m => m.Contains("Handling") && m.Contains("TestCommand"));
        Assert.Contains(log, m => m.Contains("Handled") && m.Contains("TestCommand"));
    }

    [Fact]
    public async Task PerformanceBehavior_DoesNotWarnForFastRequests()
    {
        var log = new List<string>();
        var logger = new CapturingLogger<TestCommand>(log);

        var services = new ServiceCollection();
        services.AddTransient<TestCommandHandler>(sp =>
            new TestCommandHandler(() => { }));
        services.AddSingleton<IPipelineBehavior<TestCommand, Unit>>(
            new PerformanceBehavior<TestCommand, Unit>(logger, slowThresholdMs: 60_000)); // very high threshold
        services.AddSingleton<CommandDispatcher>();

        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<CommandDispatcher>();

        await dispatcher.Send(new TestCommand("fast"), default);

        Assert.DoesNotContain(log, m => m.Contains("Slow"));
    }

    [Fact]
    public async Task PerformanceBehavior_WarnsForSlowRequests()
    {
        var log = new List<string>();
        var logger = new CapturingLogger<TestCommand>(log);

        var services = new ServiceCollection();
        services.AddTransient<TestCommandHandler>(sp =>
            new TestCommandHandler(() => { }));
        services.AddSingleton<IPipelineBehavior<TestCommand, Unit>>(
            new PerformanceBehavior<TestCommand, Unit>(logger, slowThresholdMs: -1)); // every request exceeds threshold
        services.AddSingleton<CommandDispatcher>();

        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<CommandDispatcher>();

        await dispatcher.Send(new TestCommand("slow"), default);

        Assert.Contains(log, m => m.Contains("Slow"));
    }
}

// Minimal ILogger<T> implementation for capturing log messages in tests.
public class CapturingLogger<T> : ILogger<T>
{
    private readonly List<string> _messages;
    public CapturingLogger(List<string> messages) => _messages = messages;

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => _messages.Add(formatter(state, exception));

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}
