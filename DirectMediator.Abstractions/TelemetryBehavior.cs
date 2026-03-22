using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DirectMediator;

/// <summary>
/// Pipeline behavior that instruments requests with OpenTelemetry tracing and metrics.
/// </summary>
/// <typeparam name="TRequest">The request type being handled.</typeparam>
/// <typeparam name="TResponse">The response type returned by the handler.</typeparam>
public class TelemetryBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly TelemetryBehaviorOptions _options;
    private readonly ILogger<TelemetryBehavior<TRequest, TResponse>> _logger;
    private readonly ActivitySource? _activitySource;
    
    // Metrics - using simple counters without external dependencies
    // In production, you would use System.Diagnostics.Metrics.Meter
    private static int _requestCount;
    private static int _errorCount;

    /// <summary>
    /// Creates a new instance of <see cref="TelemetryBehavior{TRequest,TResponse}"/> with the specified options.
    /// </summary>
    /// <param name="options">The telemetry behavior configuration options.</param>
    /// <param name="logger">The logger for diagnostic output.</param>
    public TelemetryBehavior(TelemetryBehaviorOptions options, ILogger<TelemetryBehavior<TRequest, TResponse>> logger)
    {
        _options = options;
        _logger = logger;
        _options.Validate();
        
        _activitySource = _options.GetActivitySource();
    }

    /// <inheritdoc/>
    public async Task<TResponse> Handle(
        TRequest request,
        CancellationToken cancellationToken,
        RequestHandlerDelegate<TResponse> next)
    {
        var requestType = typeof(TRequest).Name;
        var startTime = DateTime.UtcNow;
        
        Activity? activity = null;
        
        // Start activity if tracing is enabled
        if (_options.EnableTracing && _activitySource != null)
        {
            activity = _activitySource.StartActivity(
                name: requestType,
                kind: ActivityKind.Internal,
                tags: new ActivityTagsCollection
                {
                    ["request.type"] = requestType,
                    ["mediator.library"] = "DirectMediator"
                });
        }

        try
        {
            var response = await next();
            
            // Record success metrics
            RecordMetrics(requestType, success: true, startTime);
            
            // Set activity status to OK
            activity?.SetStatus(ActivityStatusCode.Ok);
            
            return response;
        }
        catch (Exception ex)
        {
            // Record failure metrics
            RecordMetrics(requestType, success: false, startTime);
            
            // Set activity status to Error
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            
            _logger.LogError(ex, "[Telemetry] Request {RequestType} failed", requestType);
            throw;
        }
        finally
        {
            activity?.Stop();
        }
    }

    /// <summary>
    /// Records metrics for the request.
    /// </summary>
    private void RecordMetrics(string requestType, bool success, DateTime startTime)
    {
        var elapsed = DateTime.UtcNow - startTime;
        
        if (_options.EnableMetrics)
        {
            // Increment counters (simplified - production would use proper metrics)
            System.Threading.Interlocked.Increment(ref _requestCount);
            
            if (!success)
            {
                System.Threading.Interlocked.Increment(ref _errorCount);
            }
            
            _logger.LogDebug(
                "[Telemetry] {RequestType} completed in {ElapsedMs}ms - Success: {Success}",
                requestType,
                elapsed.TotalMilliseconds,
                success);
        }
    }

    /// <summary>
    /// Gets the current request count (for testing).
    /// </summary>
    public static int GetRequestCount() => System.Threading.Volatile.Read(ref _requestCount);

    /// <summary>
    /// Gets the current error count (for testing).
    /// </summary>
    public static int GetErrorCount() => System.Threading.Volatile.Read(ref _errorCount);

    /// <summary>
    /// Resets counters (for testing).
    /// </summary>
    public static void ResetCounters()
    {
        System.Threading.Volatile.Write(ref _requestCount, 0);
        System.Threading.Volatile.Write(ref _errorCount, 0);
    }
}
