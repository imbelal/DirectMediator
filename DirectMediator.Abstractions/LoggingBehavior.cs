using Microsoft.Extensions.Logging;

namespace DirectMediator;

/// <summary>
/// Built-in pipeline behavior that logs the start, end, and any exceptions for every request.
/// Register via <see cref="BehaviorServiceCollectionExtensions.AddDirectMediatorLogging"/>.
/// </summary>
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<TRequest> _logger;

    public LoggingBehavior(ILogger<TRequest> logger) => _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        CancellationToken cancellationToken,
        RequestHandlerDelegate<TResponse> next)
    {
        _logger.LogInformation("[DirectMediator] Handling {RequestType}", typeof(TRequest).Name);
        try
        {
            var response = await next();
            _logger.LogInformation("[DirectMediator] Handled  {RequestType}", typeof(TRequest).Name);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DirectMediator] Error handling {RequestType}", typeof(TRequest).Name);
            throw;
        }
    }
}
