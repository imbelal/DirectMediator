using DirectMediator;

public record CreateOrderCommand(string Product) : ICommand;
