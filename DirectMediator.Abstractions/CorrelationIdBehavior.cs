using Microsoft.Extensions.Logging;

namespace DirectMediator;

/// <summary>
/// Built-in pipeline behavior that assigns a correlation ID to each request for distributed tracing.
/// The correlation ID is generated if not provided, and flows through the pipeline via
/// <see cref="ICorrelationContext"/>.
/// Register via <see cref="BehaviorServiceCollectionExtensions.AddDirectMediatorCorrelationId"/>.
/// </summary>
public class CorrelationIdBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private const string CorrelationIdHeader = "X-Correlation-Id";
    private readonly ICorrelationContext _correlationContext;
    private readonly ILogger<CorrelationIdBehavior<TRequest, TResponse>> _logger;

    public CorrelationIdBehavior(ICorrelationContext correlationContext, ILogger<CorrelationIdBehavior<TRequest, TResponse>> logger)
    {
        _correlationContext = correlationContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<TResponse> Handle(
        TRequest request,
        CancellationToken cancellationToken,
        RequestHandlerDelegate<TResponse> next)
    {
        // Generate new correlation ID or propagate from context
        var correlationId = Guid.NewGuid().ToString("N");
        var parentCorrelationId = _correlationContext.CorrelationId;

        // Set correlation ID for this request
        _correlationContext.SetCorrelationId(correlationId, parentCorrelationId);

        // Log with correlation ID for traceability
        _logger.LogDebug(
            "[CorrelationId] Processing {RequestType} with CorrelationId: {CorrelationId}, Parent: {ParentCorrelationId}",
            typeof(TRequest).Name,
            correlationId,
            parentCorrelationId ?? "(none)");

        var response = await next();

        _logger.LogDebug(
            "[CorrelationId] Completed {RequestType} with CorrelationId: {CorrelationId}",
            typeof(TRequest).Name,
            correlationId);

        return response;
    }
}

/// <summary>
/// Default implementation of <see cref="ICorrelationContext"/> that stores
/// correlation ID on the instance itself for request-scoped storage.
/// </summary>
public class CorrelationContext : ICorrelationContext
{
    private const string CorrelationIdHeader = "X-Correlation-Id";

    /// <summary>
    /// Gets or sets the current correlation ID. This is instance-level storage
    /// which persists as long as the CorrelationContext instance exists.
    /// </summary>
    public string CorrelationId { get; private set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the parent correlation ID for distributed tracing.
    /// </summary>
    public string? ParentCorrelationId { get; private set; }

    public void SetCorrelationId(string correlationId, string? parentCorrelationId = null)
    {
        CorrelationId = correlationId;
        ParentCorrelationId = parentCorrelationId;
    }

    internal static string? TryGetCorrelationIdFromHeader(IDictionary<string, string> headers)
    {
        return headers.TryGetValue(CorrelationIdHeader, out var correlationId) ? correlationId : null;
    }
}
