using DirectMediator;

public class OrderCreatedHandler : INotificationHandler<OrderCreatedNotification>
{
    public Task Handle(OrderCreatedNotification notification, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Notification: {notification.Product}");
        return Task.CompletedTask;
    }
}