using DirectMediator;
using DirectMediator.Generated;
using Microsoft.Extensions.DependencyInjection;

// Uses TestCommand/TestCommandHandler from CommandDispatcherTests.cs,
// TestQuery/TestQueryHandler from QueryDispatcherTests.cs,
// TestNotificationHandlerA/B from NotificationPublisherTests.cs, and
// PipelineNotification/PipelineNotificationHandler from PipelineBehaviorTests.cs.

public class MediatorTests
{
    /// <summary>Registers every handler declared across the test assembly so the generated Mediator resolves.</summary>
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddTransient<TestCommandHandler>(sp => new TestCommandHandler(() => { }));
        services.AddTransient<TestQueryHandler>();
        services.AddTransient<TestNotificationHandlerA>(sp => new TestNotificationHandlerA(() => { }));
        services.AddTransient<TestNotificationHandlerB>(sp => new TestNotificationHandlerB(() => { }));
        services.AddTransient<PipelineNotificationHandler>(sp => new PipelineNotificationHandler(() => { }));
        services.AddSingleton<IMediator, Mediator>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Mediator_Send_Command_HandlerCalled()
    {
        var called = false;
        var services = new ServiceCollection();
        services.AddTransient<TestCommandHandler>(sp => new TestCommandHandler(() => called = true));
        services.AddTransient<TestQueryHandler>();
        services.AddTransient<TestNotificationHandlerA>(sp => new TestNotificationHandlerA(() => { }));
        services.AddTransient<TestNotificationHandlerB>(sp => new TestNotificationHandlerB(() => { }));
        services.AddTransient<PipelineNotificationHandler>(sp => new PipelineNotificationHandler(() => { }));
        services.AddSingleton<IMediator, Mediator>();
        var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();

        await mediator.Send(new TestCommand("hello"));

        Assert.True(called);
    }

    [Fact]
    public async Task Mediator_Send_Query_ReturnsResult()
    {
        var mediator = BuildProvider().GetRequiredService<IMediator>();

        var result = await mediator.Send(new TestQuery(99));

        Assert.Equal("Result 99", result);
    }

    [Fact]
    public async Task Mediator_Publish_Notification_HandlerCalled()
    {
        var calledCount = 0;
        var services = new ServiceCollection();
        services.AddTransient<TestCommandHandler>(sp => new TestCommandHandler(() => { }));
        services.AddTransient<TestQueryHandler>();
        services.AddTransient<TestNotificationHandlerA>(sp => new TestNotificationHandlerA(() => { }));
        services.AddTransient<TestNotificationHandlerB>(sp => new TestNotificationHandlerB(() => { }));
        services.AddTransient<PipelineNotificationHandler>(sp =>
            new PipelineNotificationHandler(() => calledCount++));
        services.AddSingleton<IMediator, Mediator>();
        var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();

        await mediator.Publish(new PipelineNotification("event"));

        Assert.Equal(1, calledCount);
    }

    [Fact]
    public void Mediator_IsRegisteredByAddDirectMediator()
    {
        var services = new ServiceCollection();
        services.AddDirectMediator();

        // Verify the IMediator service descriptor was registered — without resolving it,
        // since test-only handlers require Action constructor params not wired in DI.
        var descriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(IMediator));
        Assert.NotNull(descriptor);
    }

    [Fact]
    public async Task Mediator_Send_ReturnsUnit_ForCommand()
    {
        var mediator = BuildProvider().GetRequiredService<IMediator>();

        var result = await mediator.Send(new TestCommand("x"));

        Assert.Equal(Unit.Value, result);
    }
}
