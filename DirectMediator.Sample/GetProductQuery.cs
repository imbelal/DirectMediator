using DirectMediator;

/// <summary>
/// Query to retrieve a product by ID. Implements ICacheableRequest to enable response caching.
/// </summary>
public record GetProductQuery(int ProductId) : ICacheableRequest<Product>
{
    /// <summary>
    /// Unique cache key for this product query.
    /// </summary>
    public string CacheKey => $"product:{ProductId}";

    /// <summary>
    /// Cache duration - null uses the default configured in AddDirectMediatorCaching().
    /// </summary>
    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(10);
}
