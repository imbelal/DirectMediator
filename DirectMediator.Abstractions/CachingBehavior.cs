using Microsoft.Extensions.Caching.Memory;

namespace DirectMediator;

/// <summary>
/// Built-in pipeline behavior that caches responses for requests implementing
/// <see cref="ICacheableRequest{TResponse}"/>. Non-cacheable requests pass through unchanged.
/// Register via <see cref="BehaviorServiceCollectionExtensions.AddDirectMediatorCaching"/>.
/// </summary>
/// <remarks>
/// The behavior is thread-safe and registered as a singleton. Cache invalidation is not
/// built-in — use <see cref="IMemoryCache.Remove"/> with the same cache key when stale entries
/// must be evicted.
/// </remarks>
public class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IMemoryCache _cache;
    private readonly CachingBehaviorOptions _options;

    /// <param name="cache">The memory cache to read from and write to.</param>
    /// <param name="options">Options controlling the default cache duration.</param>
    public CachingBehavior(IMemoryCache cache, CachingBehaviorOptions options)
    {
        _cache = cache;
        _options = options;
    }

    /// <inheritdoc/>
    public async Task<TResponse> Handle(
        TRequest request,
        CancellationToken cancellationToken,
        RequestHandlerDelegate<TResponse> next)
    {
        // Only cache requests that explicitly opt-in via ICacheableRequest<TResponse>.
        if (request is not ICacheableRequest<TResponse> cacheableRequest)
            return await next();

        var key = cacheableRequest.CacheKey;

        if (_cache.TryGetValue(key, out TResponse? cached))
            return cached!;

        var response = await next();
        var duration = cacheableRequest.CacheDuration ?? _options.DefaultDuration;
        _cache.Set(key, response, duration);
        return response;
    }
}
