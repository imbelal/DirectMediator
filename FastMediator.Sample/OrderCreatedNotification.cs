using FastMediator;

public record OrderCreatedNotification(string Product) : INotification;