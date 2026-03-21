using DirectMediator;
using DirectMediator.Generated;
using Microsoft.Extensions.DependencyInjection;

// Handlers must be public/top-level so the source generator can reference them in generated code.
// Two distinct handler types are used so the generator emits both in NotificationPublisher.
public record TestNotification(int Id) : INotification;
public class TestNotificationHandlerA : INotificationHandler<TestNotification>
{
    private readonly Action _callback;
    public TestNotificationHandlerA(Action callback) => _callback = callback;

    public Task Handle(TestNotification notification, CancellationToken cancellationToken)
    {
        _callback();
        return Task.CompletedTask;
    }
}
public class TestNotificationHandlerB : INotificationHandler<TestNotification>
{
    private readonly Action _callback;
    public TestNotificationHandlerB(Action callback) => _callback = callback;

    public Task Handle(TestNotification notification, CancellationToken cancellationToken)
    {
        _callback();
        return Task.CompletedTask;
    }
}

public class NotificationPublisherTests
{
    [Fact]
    public async Task NotificationPublisher_PublishesNotification_AllHandlersCalled()
    {
        var calledCount = 0;

        var services = new ServiceCollection();
        // Register the concrete handler types so the generated NotificationPublisher can resolve them
        services.AddTransient<TestNotificationHandlerA>(sp =>
            new TestNotificationHandlerA(() => calledCount++));
        services.AddTransient<TestNotificationHandlerB>(sp =>
            new TestNotificationHandlerB(() => calledCount++));
        services.AddSingleton<NotificationPublisher>();

        var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<NotificationPublisher>();

        await publisher.Publish(new TestNotification(1));

        Assert.Equal(2, calledCount);
    }
}
