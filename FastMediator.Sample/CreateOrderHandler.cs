using FastMediator;

public class CreateOrderHandler : IRequestHandler<CreateOrderCommand, Unit>
{
    public Task<Unit> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Order created: {request.Product}");
        return Task.FromResult(Unit.Value);
    }
}