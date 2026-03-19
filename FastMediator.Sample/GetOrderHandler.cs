using FastMediator;

public class GetOrderHandler : IRequestHandler<GetOrderQuery, string>
{
    public Task<string> Handle(GetOrderQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult($"Order #{request.Id}");
    }
}
