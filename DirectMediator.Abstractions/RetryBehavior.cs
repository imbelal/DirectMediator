using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DirectMediator;

/// <summary>
/// Pipeline behavior that automatically retries failed requests based on configurable policies.
/// Supports multiple retry strategies including fixed delay, exponential backoff, and jittered backoff.
/// </summary>
/// <typeparam name="TRequest">The request type being handled.</typeparam>
/// <typeparam name="TResponse">The response type returned by the handler.</typeparam>
public class RetryBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly RetryBehaviorOptions _options;
    private readonly ILogger<RetryBehavior<TRequest, TResponse>> _logger;
    private readonly Random _random = new();

    /// <summary>
    /// Creates a new instance of <see cref="RetryBehavior{TRequest,TResponse}"/> with the specified options.
    /// </summary>
    /// <param name="options">The retry behavior configuration options.</param>
    /// <param name="logger">The logger for diagnostic output.</param>
    public RetryBehavior(RetryBehaviorOptions options, ILogger<RetryBehavior<TRequest, TResponse>> logger)
    {
        _options = options;
        _logger = logger;
        _options.Validate();
    }

    /// <inheritdoc/>
    public async Task<TResponse> Handle(
        TRequest request,
        CancellationToken cancellationToken,
        RequestHandlerDelegate<TResponse> next)
    {
        // If retries are disabled, execute once
        if (_options.MaxRetryCount <= 0)
        {
            return await next();
        }

        Exception? lastException = null;
        var attemptNumber = 0;

        while (true)
        {
            try
            {
                // First attempt (or retry after failure)
                return await next();
            }
            catch (Exception ex)
            {
                lastException = ex;
                attemptNumber++;

                // Check if this exception type should trigger a retry
                // We check this BEFORE checking if we've exhausted retries
                if (!ShouldRetry(ex, attemptNumber))
                {
                    _logger.LogWarning(
                        ex,
                        "[RetryBehavior] Exception {ExceptionType} is not retryable for {RequestType}",
                        ex.GetType().Name,
                        typeof(TRequest).Name);
                    throw;
                }

                // Check if we've exhausted all retries
                if (attemptNumber > _options.MaxRetryCount)
                {
                    _logger.LogError(
                        ex,
                        "[RetryBehavior] Retry attempts exhausted for {RequestType}. Total attempts: {AttemptCount}",
                        typeof(TRequest).Name,
                        attemptNumber);

                    _options.OnRetryExhausted?.Invoke(typeof(TRequest), attemptNumber, ex);
                    throw;
                }

                // Calculate delay for this retry attempt
                var delay = CalculateDelay(attemptNumber);

                _logger.LogWarning(
                    ex,
                    "[RetryBehavior] Attempt {AttemptNumber}/{MaxRetries} failed for {RequestType}. Retrying in {Delay}ms",
                    attemptNumber,
                    _options.MaxRetryCount,
                    typeof(TRequest).Name,
                    delay.TotalMilliseconds);

                _options.OnRetry?.Invoke(typeof(TRequest), attemptNumber, delay, ex);

                // Wait before retrying
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Determines whether the exception should trigger a retry attempt.
    /// </summary>
    private bool ShouldRetry(Exception exception, int attemptNumber)
    {
        // If no custom predicate, retry all exceptions
        if (_options.ShouldRetryOnException == null)
            return true;

        // Use custom predicate to determine if we should retry
        try
        {
            return _options.ShouldRetryOnException(exception);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RetryBehavior] Error in ShouldRetryOnException predicate");
            return false;
        }
    }

    /// <summary>
    /// Calculates the delay before the next retry attempt based on the configured strategy.
    /// </summary>
    /// <param name="attemptNumber">The current attempt number (1-based).</param>
    /// <returns>The delay before the next retry.</returns>
    private TimeSpan CalculateDelay(int attemptNumber)
    {
        var baseDelay = _options.BaseDelay;
        var maxDelay = _options.MaxDelay;

        TimeSpan delay = _options.Strategy switch
        {
            RetryStrategy.FixedDelay => baseDelay,
            RetryStrategy.LinearBackoff => TimeSpan.FromTicks(baseDelay.Ticks * attemptNumber),
            RetryStrategy.ExponentialBackoff => CalculateExponentialDelay(attemptNumber),
            RetryStrategy.ExponentialBackoffWithJitter => CalculateExponentialDelayWithJitter(attemptNumber),
            _ => baseDelay
        };

        // Ensure delay doesn't exceed max
        if (delay > maxDelay)
            delay = maxDelay;

        return delay;
    }

    /// <summary>
    /// Calculates exponential backoff delay without jitter.
    /// </summary>
    private TimeSpan CalculateExponentialDelay(int attemptNumber)
    {
        // Exponential: delay = baseDelay * (multiplier ^ (attemptNumber - 1))
        var delayMs = _options.BaseDelay.TotalMilliseconds * Math.Pow(_options.BackoffMultiplier, attemptNumber - 1);
        return TimeSpan.FromMilliseconds(delayMs);
    }

    /// <summary>
    /// Calculates exponential backoff delay with jitter to prevent thundering herd.
    /// </summary>
    private TimeSpan CalculateExponentialDelayWithJitter(int attemptNumber)
    {
        // Base exponential delay
        var delayMs = _options.BaseDelay.TotalMilliseconds * Math.Pow(_options.BackoffMultiplier, attemptNumber - 1);

        // Apply jitter: delay +/- (delay * jitterFactor)
        if (_options.JitterFactor > 0)
        {
            var jitterRange = delayMs * _options.JitterFactor;
            var jitter = (_random.NextDouble() * 2 - 1) * jitterRange; // -jitterRange to +jitterRange
            delayMs += jitter;
        }

        // Ensure delay is positive
        if (delayMs < 0)
            delayMs = _options.BaseDelay.TotalMilliseconds;

        return TimeSpan.FromMilliseconds(delayMs);
    }
}
