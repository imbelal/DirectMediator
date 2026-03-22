using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DirectMediator;

/// <summary>
/// Extension methods for opting in to built-in DirectMediator pipeline behaviors.
/// </summary>
/// <remarks>
/// Because <c>AddDirectMediator()</c> registers dispatchers as singletons, and the pipeline
/// delegate chain is built once at dispatcher construction time, all behaviors injected into
/// the constructor are captured for the lifetime of the singleton. For this reason built-in
/// behaviors are registered as singletons (they are stateless and thread-safe). Custom
/// behaviors must also be singleton-safe when added to the pipeline.
/// </remarks>
public static class BehaviorServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="LoggingBehavior{TRequest,TResponse}"/> as an open-generic
    /// <see cref="IPipelineBehavior{TRequest,TResponse}"/> (singleton) so it wraps every request.
    /// Requires <c>services.AddLogging()</c> to be configured.
    /// </summary>
    public static IServiceCollection AddDirectMediatorLogging(this IServiceCollection services)
    {
        services.AddSingleton(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        return services;
    }

    /// <summary>
    /// Registers <see cref="PerformanceBehavior{TRequest,TResponse}"/> as an open-generic
    /// <see cref="IPipelineBehavior{TRequest,TResponse}"/> (singleton) so it monitors every request.
    /// Requires <c>services.AddLogging()</c> to be configured.
    /// </summary>
    public static IServiceCollection AddDirectMediatorPerformanceBehavior(this IServiceCollection services)
    {
        services.AddSingleton(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
        return services;
    }

    /// <summary>
    /// Registers <see cref="CachingBehavior{TRequest,TResponse}"/> as an open-generic
    /// <see cref="IPipelineBehavior{TRequest,TResponse}"/> (singleton) so it automatically
    /// caches responses for any request implementing <see cref="ICacheableRequest{TResponse}"/>.
    /// Non-cacheable requests pass through unchanged.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="defaultCacheDuration">
    /// Default TTL used when a request's <see cref="ICacheableRequest{TResponse}.CacheDuration"/>
    /// returns <c>null</c>. Omit to use the default of 5 minutes.
    /// </param>
    /// <remarks>
    /// Requires <see cref="Microsoft.Extensions.Caching.Memory.IMemoryCache"/> to be registered.
    /// Call <c>services.AddMemoryCache()</c> before or after this method.
    /// <code>
    /// services.AddMemoryCache();
    /// services.AddDirectMediator()
    ///         .AddDirectMediatorCaching();
    /// </code>
    /// </remarks>
    public static IServiceCollection AddDirectMediatorCaching(
        this IServiceCollection services,
        TimeSpan? defaultCacheDuration = null)
    {
        services.TryAddSingleton(sp => new CachingBehaviorOptions
        {
            DefaultDuration = defaultCacheDuration ?? TimeSpan.FromMinutes(5)
        });
        services.AddSingleton(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));
        return services;
    }

    /// <summary>
    /// Registers <see cref="ValidationBehavior{TRequest,TResponse}"/> as an open-generic
    /// <see cref="IPipelineBehavior{TRequest,TResponse}"/> (singleton) so it validates every
    /// request before it reaches its handler.
    /// </summary>
    /// <remarks>
    /// Validators are optional. When one or more <see cref="IValidator{T}"/> instances are
    /// registered for a request type they are all executed and any failures cause a
    /// <see cref="FluentValidation.ValidationException"/> to be thrown before the handler is
    /// invoked. Requests with no registered validators pass through unchanged.
    /// <para>
    /// Because the validation behavior is registered as a singleton in the DirectMediator
    /// pipeline, any <see cref="IValidator{T}"/> instances it uses are effectively long-lived
    /// and may be invoked concurrently from multiple threads. Validators must therefore be
    /// thread-safe and must not depend on scoped services. Register validators with a
    /// singleton-safe lifetime (for example, as singletons or as stateless transients that
    /// do not capture scoped dependencies).
    /// </para>
    /// <code>
    /// services.AddSingleton&lt;IValidator&lt;MyCommand&gt;, MyCommandValidator&gt;();
    /// services.AddDirectMediator()
    ///         .AddDirectMediatorValidation();
    /// </code>
    /// </remarks>
    public static IServiceCollection AddDirectMediatorValidation(this IServiceCollection services)
    {
        services.AddSingleton(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        return services;
    }

    /// <summary>
    /// Registers <see cref="CorrelationIdBehavior{TRequest,TResponse}"/> as an open-generic
    /// <see cref="IPipelineBehavior{TRequest,TResponse}"/> (singleton) so it assigns and propagates
    /// a correlation ID through the pipeline for distributed tracing.
    /// </summary>
    /// <remarks>
    /// The behavior uses <see cref="ICorrelationContext"/> to store the correlation ID, which
    /// flows through AsyncLocal for request-scoped storage. Correlation IDs can be propagated
    /// from incoming requests via headers (e.g., "X-Correlation-Id") or generated automatically.
    /// <code>
    /// services.AddDirectMediator()
    ///         .AddDirectMediatorCorrelationId();
    /// </code>
    /// </remarks>
    public static IServiceCollection AddDirectMediatorCorrelationId(this IServiceCollection services)
    {
        services.TryAddSingleton<ICorrelationContext, CorrelationContext>();
        services.AddSingleton(typeof(IPipelineBehavior<,>), typeof(CorrelationIdBehavior<,>));
        return services;
    }

    /// <summary>
    /// Registers <see cref="RetryBehavior{TRequest,TResponse}"/> as an open-generic
    /// <see cref="IPipelineBehavior{TRequest,TResponse}"/> (singleton) so it automatically
    /// retries failed requests based on configurable policies.
    /// </summary>
    /// <remarks>
    /// The retry behavior wraps request execution and catches transient failures, automatically
    /// retrying based on user-configured policies. It supports multiple retry strategies:
    /// <list type="bullet">
    ///     <item>FixedDelay - same delay between retries</item>
    ///     <item>LinearBackoff - delay increases linearly</item>
    ///     <item>ExponentialBackoff - delay doubles each retry</item>
    ///     <item>ExponentialBackoffWithJitter - recommended for production</item>
    /// </list>
    /// <para>
    /// Requires <c>services.AddLogging()</c> to be configured.
    /// </para>
    /// <code>
    /// // Default options (3 retries, exponential backoff with jitter)
    /// services.AddDirectMediator()
    ///         .AddDirectMediatorRetry();
    ///
    /// // Custom options
    /// services.AddDirectMediator()
    ///         .AddDirectMediatorRetry(options =>
    ///         {
    ///             options.MaxRetryCount = 5;
    ///             options.BaseDelay = TimeSpan.FromSeconds(2);
    ///             options.Strategy = RetryStrategy.ExponentialBackoffWithJitter;
    ///             options.ShouldRetryOnException = ex => ex is HttpRequestException || ex is TimeoutException;
    ///         });
    /// </code>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Optional action to configure retry options.</param>
    public static IServiceCollection AddDirectMediatorRetry(
        this IServiceCollection services,
        Action<RetryBehaviorOptions>? configureOptions = null)
    {
        var options = new RetryBehaviorOptions();
        configureOptions?.Invoke(options);

        services.TryAddSingleton(options);
        services.AddSingleton(typeof(IPipelineBehavior<,>), typeof(RetryBehavior<,>));
        return services;
    }

    /// <summary>
    /// Registers <see cref="TelemetryBehavior{TRequest,TResponse}"/> as an open-generic
    /// <see cref="IPipelineBehavior{TRequest,TResponse}"/> (singleton) so it instruments
    /// every request with OpenTelemetry tracing and metrics.
    /// </summary>
    /// <remarks>
    /// The telemetry behavior provides:
    /// <list type="bullet">
    ///     <item>Distributed tracing via System.Diagnostics.ActivitySource</item>
    ///     <item>Request duration metrics</item>
    ///     <item>Error counting</item>
    /// </list>
    /// <para>
    /// Requires <c>services.AddLogging()</c> to be configured.
    /// </para>
    /// <code>
    /// // Default options
    /// services.AddDirectMediator()
    ///         .AddDirectMediatorTelemetry();
    ///
    /// // Custom options
    /// services.AddDirectMediator()
    ///         .AddDirectMediatorTelemetry(options =>
    ///         {
    ///             options.ActivitySourceName = "MyApp";
    ///             options.EnableTracing = true;
    ///             options.EnableMetrics = true;
    ///         });
    /// </code>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Optional action to configure telemetry options.</param>
    public static IServiceCollection AddDirectMediatorTelemetry(
        this IServiceCollection services,
        Action<TelemetryBehaviorOptions>? configureOptions = null)
    {
        var options = new TelemetryBehaviorOptions();
        configureOptions?.Invoke(options);

        services.TryAddSingleton(options);
        services.AddSingleton(typeof(IPipelineBehavior<,>), typeof(TelemetryBehavior<,>));
        return services;
    }
}
