using DirectMediator;

/// <summary>
/// Handler for GetProductQuery. Returns a Product from "database".
/// </summary>
public class GetProductHandler : IRequestHandler<GetProductQuery, Product>
{
    public Task<Product> Handle(GetProductQuery request, CancellationToken cancellationToken)
    {
        // In a real application, this would fetch from a database
        var product = new Product(request.ProductId, $"Product {request.ProductId}", 29.99m);
        return Task.FromResult(product);
    }
}
