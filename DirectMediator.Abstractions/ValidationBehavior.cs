using FluentValidation;

namespace DirectMediator;

/// <summary>
/// Built-in pipeline behavior that runs all registered <see cref="IValidator{TRequest}"/>
/// instances against the incoming request before passing it to the handler.
/// If any validators report failures a <see cref="ValidationException"/> is thrown and the
/// handler is never invoked.
/// Register via <see cref="BehaviorServiceCollectionExtensions.AddDirectMediatorValidation"/>.
/// </summary>
/// <remarks>
/// When no validators are registered for a request type the behavior passes through unchanged.
/// Validators must be registered in the DI container (e.g.
/// <c>services.AddSingleton&lt;IValidator&lt;MyRequest&gt;&gt;, MyRequestValidator&gt;()</c>)
/// before <c>AddDirectMediator()</c> so that they are captured at dispatcher-construction time.
/// </remarks>
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    /// <param name="validators">Zero or more validators registered for <typeparamref name="TRequest"/>.</param>
    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
        => _validators = validators;

    /// <inheritdoc/>
    public async Task<TResponse> Handle(
        TRequest request,
        CancellationToken cancellationToken,
        RequestHandlerDelegate<TResponse> next)
    {
        if (!_validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);

        var failures = (await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken))))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count > 0)
            throw new ValidationException(failures);

        return await next();
    }
}
