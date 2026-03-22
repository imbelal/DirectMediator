using Microsoft.Extensions.DependencyInjection;

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
}
