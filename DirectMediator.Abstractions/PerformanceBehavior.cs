using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace DirectMediator;

/// <summary>
/// Built-in pipeline behavior that measures request handling time and logs a warning when
/// a request exceeds <paramref name="slowThresholdMs"/> milliseconds.
/// Register via <see cref="BehaviorServiceCollectionExtensions.AddDirectMediatorPerformanceBehavior"/>.
/// </summary>
public class PerformanceBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<TRequest> _logger;
    private readonly long _slowThresholdMs;

    /// <param name="logger">Logger for the warning message.</param>
    /// <param name="slowThresholdMs">Requests taking longer than this many milliseconds emit a warning. Default: 500 ms.</param>
    public PerformanceBehavior(ILogger<TRequest> logger, long slowThresholdMs = 500)
    {
        _logger = logger;
        _slowThresholdMs = slowThresholdMs;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        CancellationToken cancellationToken,
        RequestHandlerDelegate<TResponse> next)
    {
        var sw = Stopwatch.StartNew();
        var response = await next();
        sw.Stop();

        if (sw.ElapsedMilliseconds > _slowThresholdMs)
            _logger.LogWarning(
                "[DirectMediator] Slow request detected: {RequestType} took {ElapsedMs} ms (threshold: {ThresholdMs} ms)",
                typeof(TRequest).Name, sw.ElapsedMilliseconds, _slowThresholdMs);

        return response;
    }
}
