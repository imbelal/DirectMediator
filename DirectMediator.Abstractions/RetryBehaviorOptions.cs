using System;

namespace DirectMediator;

/// <summary>
/// Configuration options for the <see cref="RetryBehavior{TRequest,TResponse}"/> pipeline behavior.
/// Defines retry policies including maximum attempts, delay strategies, and exception filtering.
/// </summary>
public class RetryBehaviorOptions
{
    /// <summary>
    /// Gets or sets the maximum number of retry attempts. Default is 3.
    /// Set to 0 to disable retries.
    /// </summary>
    public int MaxRetryCount { get; set; } = 3;

    /// <summary>
    /// Gets or sets the base delay between retry attempts. Default is 1 second.
    /// </summary>
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets the maximum delay between retry attempts. Default is 30 seconds.
    /// This caps the exponential backoff to prevent excessively long delays.
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the backoff multiplier for exponential backoff. Default is 2.0.
    /// A value of 2.0 means each retry waits twice as long as the previous one.
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Gets or sets the jitter percentage (0.0 to 1.0) to add randomness to delays.
    /// Default is 0.1 (10% jitter). This helps prevent thundering herd problems.
    /// Set to 0 to disable jitter.
    /// </summary>
    public double JitterFactor { get; set; } = 0.1;

    /// <summary>
    /// Gets or sets the retry strategy to use. Default is <see cref="RetryStrategy.ExponentialBackoffWithJitter"/>.
    /// </summary>
    public RetryStrategy Strategy { get; set; } = RetryStrategy.ExponentialBackoffWithJitter;

    /// <summary>
    /// Gets or sets a predicate that determines whether an exception should trigger a retry.
    /// If null, all exceptions will trigger retries up to <see cref="MaxRetryCount"/>.
    /// </summary>
    /// <remarks>
    /// Example: <c>ex => ex is HttpRequestException || ex is TimeoutException</c>
    /// </remarks>
    public Func<Exception, bool>? ShouldRetryOnException { get; set; }

    /// <summary>
    /// Gets or sets a callback invoked before each retry attempt.
    /// Can be used for logging, metrics, or custom handling.
    /// </summary>
    /// <remarks>
    /// Parameters: (requestType, attemptNumber, delay, exception)
    /// </remarks>
    public Action<Type, int, TimeSpan, Exception>? OnRetry { get; set; }

    /// <summary>
    /// Gets or sets a callback invoked when all retry attempts have been exhausted.
    /// </summary>
    /// <remarks>
    /// Parameters: (requestType, totalAttempts, finalException)
    /// </remarks>
    public Action<Type, int, Exception>? OnRetryExhausted { get; set; }

    /// <summary>
    /// Validates the options and throws <see cref="ArgumentException"/> if any settings are invalid.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when options contain invalid values.</exception>
    public void Validate()
    {
        if (MaxRetryCount < 0)
            throw new ArgumentException("MaxRetryCount must be non-negative.", nameof(MaxRetryCount));

        if (BaseDelay <= TimeSpan.Zero)
            throw new ArgumentException("BaseDelay must be positive.", nameof(BaseDelay));

        if (MaxDelay <= TimeSpan.Zero)
            throw new ArgumentException("MaxDelay must be positive.", nameof(MaxDelay));

        if (MaxDelay < BaseDelay)
            throw new ArgumentException("MaxDelay must be greater than or equal to BaseDelay.", nameof(MaxDelay));

        if (BackoffMultiplier < 1.0)
            throw new ArgumentException("BackoffMultiplier must be at least 1.0.", nameof(BackoffMultiplier));

        if (JitterFactor < 0.0 || JitterFactor > 1.0)
            throw new ArgumentException("JitterFactor must be between 0.0 and 1.0.", nameof(JitterFactor));
    }
}

/// <summary>
/// Defines the retry delay calculation strategies available for <see cref="RetryBehavior{TRequest,TResponse}"/>.
/// </summary>
public enum RetryStrategy
{
    /// <summary>
    /// Fixed delay between all retry attempts. Uses <see cref="RetryBehaviorOptions.BaseDelay"/> for every retry.
    /// </summary>
    FixedDelay,

    /// <summary>
    /// Linear backoff where delay increases by <see cref="RetryBehaviorOptions.BaseDelay"/> each attempt.
    /// Delay = BaseDelay * attemptNumber
    /// </summary>
    LinearBackoff,

    /// <summary>
    /// Exponential backoff where delay doubles (or uses configured multiplier) each attempt.
    /// Delay = BaseDelay * (Multiplier ^ attemptNumber)
    /// </summary>
    ExponentialBackoff,

    /// <summary>
    /// Exponential backoff with jitter to prevent thundering herd problems.
    /// This is the recommended strategy for production scenarios.
    /// </summary>
    ExponentialBackoffWithJitter
}
