using Microsoft.Extensions.DependencyInjection;

namespace DirectMediator;

/// <summary>
/// Extension methods for opting in to built-in DirectMediator pipeline behaviors.
/// </summary>
public static class BehaviorServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="LoggingBehavior{TRequest,TResponse}"/> as an open-generic
    /// <see cref="IPipelineBehavior{TRequest,TResponse}"/> so it wraps every request.
    /// Requires <c>services.AddLogging()</c> to be configured.
    /// </summary>
    public static IServiceCollection AddDirectMediatorLogging(this IServiceCollection services)
    {
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        return services;
    }

    /// <summary>
    /// Registers <see cref="PerformanceBehavior{TRequest,TResponse}"/> as an open-generic
    /// <see cref="IPipelineBehavior{TRequest,TResponse}"/> so it monitors every request.
    /// Requires <c>services.AddLogging()</c> to be configured.
    /// </summary>
    public static IServiceCollection AddDirectMediatorPerformanceBehavior(this IServiceCollection services)
    {
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
        return services;
    }
}
