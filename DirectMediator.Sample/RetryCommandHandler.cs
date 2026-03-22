namespace DirectMediator.Sample;

/// <summary>
/// Handler for RetryCommand that simulates transient failures.
/// </summary>
public class RetryCommandHandler : IRequestHandler<RetryCommand, string>
{
    private static int _callCount = 0;

    public async Task<string> Handle(RetryCommand request, CancellationToken cancellationToken)
    {
        _callCount++;
        
        // Simulate a flaky external service that fails on first few calls
        if (request.FailCount > 0 && _callCount <= request.FailCount)
        {
            await Task.Delay(10, cancellationToken); // Simulate network delay
            throw new TransientFailureException(
                $"Transient failure (attempt {_callCount}/{request.FailCount + 1}). Retrying...");
        }

        // Reset for next command
        _callCount = 0;
        
        return $"Success: Processed {request.ProductName} after {request.FailCount} transient failures";
    }
}

/// <summary>
/// Exception that indicates a transient failure that can be retried.
/// </summary>
public class TransientFailureException : Exception
{
    public TransientFailureException(string message) : base(message) { }
}
