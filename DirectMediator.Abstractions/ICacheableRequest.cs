namespace DirectMediator;

/// <summary>
/// Opt-in marker interface for requests whose responses should be cached by
/// <see cref="CachingBehavior{TRequest,TResponse}"/>.
/// </summary>
/// <typeparam name="TResponse">The response type produced by this request.</typeparam>
/// <example>
/// <code>
/// public record GetProductQuery(int Id) : ICacheableRequest&lt;Product&gt;
/// {
///     public string CacheKey =&gt; $"product:{Id}";
///     public TimeSpan? CacheDuration =&gt; TimeSpan.FromMinutes(10); // null = use default
/// }
/// </code>
/// </example>
public interface ICacheableRequest<TResponse> : IRequest<TResponse>
{
    /// <summary>The cache key used to store and retrieve the cached response.</summary>
    string CacheKey { get; }

    /// <summary>
    /// How long to cache the response. When <c>null</c> the
    /// <see cref="CachingBehaviorOptions.DefaultDuration"/> is used (default: 5 minutes).
    /// </summary>
    TimeSpan? CacheDuration { get; }
}
