namespace DirectMediator;

/// <summary>
/// Options for <see cref="CachingBehavior{TRequest,TResponse}"/>.
/// Register via <see cref="BehaviorServiceCollectionExtensions.AddDirectMediatorCaching"/>.
/// </summary>
public sealed class CachingBehaviorOptions
{
    /// <summary>
    /// Default cache duration used when a request's <see cref="ICacheableRequest{TResponse}.CacheDuration"/>
    /// returns <c>null</c>. Defaults to 5 minutes.
    /// </summary>
    public TimeSpan DefaultDuration { get; set; } = TimeSpan.FromMinutes(5);
}
