using FastMediator;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class NotificationPublisherTests
{
    private record TestNotification(int Id) : INotification;

    [Fact]
    public async Task NotificationPublisher_PublishesNotification_AllHandlersCalled()
    {
        var calledCount = 0;

        var services = new ServiceCollection();
        services.AddTransient<INotificationHandler<TestNotification>>(sp =>
            new TestHandler(() => calledCount++));
        services.AddTransient<INotificationHandler<TestNotification>>(sp =>
            new TestHandler(() => calledCount++));
        services.AddSingleton<NotificationPublisher>();

        var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<NotificationPublisher>();

        await publisher.Publish(new TestNotification(1));

        Assert.Equal(2, calledCount);
    }

    private class TestHandler : INotificationHandler<TestNotification>
    {
        private readonly Action _callback;
        public TestHandler(Action callback) => _callback = callback;

        public Task Handle(TestNotification notification, CancellationToken cancellationToken)
        {
            _callback();
            return Task.CompletedTask;
        }
    }
}
