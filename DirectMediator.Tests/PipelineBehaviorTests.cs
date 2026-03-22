using DirectMediator;
using DirectMediator.Generated;
using Microsoft.Extensions.DependencyInjection;

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

// --- Pipeline behavior implementations ---
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly List<string> _log;
    public LoggingBehavior(List<string> log) => _log = log;

    public async Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken, RequestHandlerDelegate<TResponse> next)
    {
        _log.Add($"before:{typeof(TRequest).Name}");
        var result = await next();
        _log.Add($"after:{typeof(TRequest).Name}");
        return result;
    }
}

public class OrderingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly List<string> _log;
    private readonly string _name;
    public OrderingBehavior(List<string> log, string name) { _log = log; _name = name; }

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
            new LoggingBehavior<TestCommand, Unit>(log));
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
            new LoggingBehavior<TestQuery, string>(log));
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
        services.AddSingleton<IPipelineBehavior<TestCommand, Unit>>(new OrderingBehavior<TestCommand, Unit>(log, "first"));
        services.AddSingleton<IPipelineBehavior<TestCommand, Unit>>(new OrderingBehavior<TestCommand, Unit>(log, "second"));
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
            new LoggingBehavior<TestCommand, Unit>(log));
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
            new LoggingBehavior<TestQuery, string>(log));
        services.AddSingleton<IMediator, Mediator>();

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new TestQuery(42));

        Assert.Equal("Result 42", result);
        Assert.Equal(new[] { "before:TestQuery", "after:TestQuery" }, log);
    }
}
