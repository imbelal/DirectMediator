using DirectMediator;

public record OrderCreatedNotification(string Product) : INotification;