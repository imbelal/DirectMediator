using DirectMediator;

public record GetOrderQuery(int Id) : IQuery<string>;