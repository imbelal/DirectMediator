namespace DirectMediator;

/// <summary>
/// Provides access to the current correlation ID for distributed tracing.
/// The correlation ID flows through the entire request pipeline and can be
/// used to correlate logs and track requests across multiple systems.
/// </summary>
public interface ICorrelationContext
{
    /// <summary>
    /// The unique correlation ID for the current request.
    /// This is typically a GUID that is either generated for the request
    /// or propagated from an incoming request header.
    /// </summary>
    string CorrelationId { get; }

    /// <summary>
    /// The parent correlation ID if this request was triggered by another request.
    /// Useful for creating hierarchical traces in distributed systems.
    /// </summary>
    string? ParentCorrelationId { get; }

    /// <summary>
    /// Sets the correlation ID for the current request scope.
    /// </summary>
    void SetCorrelationId(string correlationId, string? parentCorrelationId = null);
}
