namespace DirectMediator;

/// <summary>
/// Unified mediator interface. Combines command dispatch, query dispatch,
/// and notification publishing into a single injectable abstraction.
/// </summary>
public interface IMediator : INotificationPublisher
{
    /// <summary>
    /// Sends a request (command or query) through the pipeline and returns the response.
    /// Commands return <see cref="Unit"/>; queries return their typed response.
    /// </summary>
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}
