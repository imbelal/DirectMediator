using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DirectMediator.Tests;

public class RetryBehaviorTests
{
    private readonly ServiceProvider _serviceProvider;

    public RetryBehaviorTests()
    {
        var services = new ServiceCollection();
        _serviceProvider = services.BuildServiceProvider();
    }

    #region Success Tests

    [Fact]
    public async Task RetryBehavior_SucceedsOnFirstAttempt_NoRetries()
    {
        // Arrange
        var callCount = 0;
        var options = new RetryBehaviorOptions { MaxRetryCount = 3 };
        var behavior = CreateBehavior(options);

        async Task<string> Handler() 
        { 
            callCount++; 
            return "success"; 
        }

        // Act
        var result = await behavior.Handle(new TestRequest(), CancellationToken.None, Handler);

        // Assert
        Assert.Equal("success", result);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task RetryBehavior_SucceedsAfterOneRetry_RetriesOnce()
    {
        // Arrange
        var callCount = 0;
        var options = new RetryBehaviorOptions { MaxRetryCount = 3 };
        var behavior = CreateBehavior(options);

        async Task<string> Handler()
        {
            callCount++;
            if (callCount < 2)
                throw new TransientException("Transient error");
            return "success";
        }

        // Act
        var result = await behavior.Handle(new TestRequest(), CancellationToken.None, Handler);

        // Assert
        Assert.Equal("success", result);
        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task RetryBehavior_SucceedsAfterMultipleRetries_RetriesCorrectNumber()
    {
        // Arrange
        var callCount = 0;
        var options = new RetryBehaviorOptions { MaxRetryCount = 3 };
        var behavior = CreateBehavior(options);

        async Task<string> Handler()
        {
            callCount++;
            if (callCount < 4)
                throw new TransientException("Transient error");
            return "success";
        }

        // Act
        var result = await behavior.Handle(new TestRequest(), CancellationToken.None, Handler);

        // Assert
        Assert.Equal("success", result);
        Assert.Equal(4, callCount);
    }

    #endregion

    #region Exhausted Retry Tests

    [Fact]
    public async Task RetryBehavior_AllRetriesExhausted_ThrowsException()
    {
        // Arrange
        var callCount = 0;
        var options = new RetryBehaviorOptions { MaxRetryCount = 3, BaseDelay = TimeSpan.FromMilliseconds(1) };
        var behavior = CreateBehavior(options);

        async Task<string> Handler()
        {
            callCount++;
            throw new TransientException("Always fails");
        }

        // Act & Assert
        await Assert.ThrowsAsync<TransientException>(() => 
            behavior.Handle(new TestRequest(), CancellationToken.None, Handler));

        // Assert - should have initial attempt + 3 retries = 4 calls
        Assert.Equal(4, callCount);
    }

    [Fact]
    public async Task RetryBehavior_DisabledRetries_NoRetries()
    {
        // Arrange
        var callCount = 0;
        var options = new RetryBehaviorOptions { MaxRetryCount = 0 };
        var behavior = CreateBehavior(options);

        async Task<string> Handler()
        {
            callCount++;
            throw new TransientException("Always fails");
        }

        // Act & Assert
        await Assert.ThrowsAsync<TransientException>(() =>
            behavior.Handle(new TestRequest(), CancellationToken.None, Handler));

        Assert.Equal(1, callCount);
    }

    #endregion

    #region Non-Retryable Exception Tests

    [Fact]
    public async Task RetryBehavior_NonRetryableException_ThrowsImmediately()
    {
        // Arrange
        var callCount = 0;
        var options = new RetryBehaviorOptions 
        { 
            MaxRetryCount = 3,
            ShouldRetryOnException = ex => ex is TransientException
        };
        var behavior = CreateBehavior(options);

        async Task<string> Handler()
        {
            callCount++;
            throw new NonTransientException("Non-retryable");
        }

        // Act & Assert
        await Assert.ThrowsAsync<NonTransientException>(() =>
            behavior.Handle(new TestRequest(), CancellationToken.None, Handler));

        // Should not retry for non-retryable exception
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task RetryBehavior_FilteredRetry_SomeExceptionsRetrySomeDont()
    {
        // Arrange
        var callCount = 0;
        var options = new RetryBehaviorOptions 
        { 
            MaxRetryCount = 3,
            ShouldRetryOnException = ex => ex is TransientException
        };
        var behavior = CreateBehavior(options);

        var exceptions = new Queue<Exception>(new Exception[] 
        { 
            new NonTransientException("First"),
            new TransientException("Second"),
            new TransientException("Third")
        });

        async Task<string> Handler()
        {
            callCount++;
            if (exceptions.Count > 0)
                throw exceptions.Dequeue();
            return "success";
        }

        // Act & Assert - First exception is non-retryable, should throw immediately
        await Assert.ThrowsAsync<NonTransientException>(() =>
            behavior.Handle(new TestRequest(), CancellationToken.None, Handler));

        Assert.Equal(1, callCount);
    }

    #endregion

    #region Backoff Strategy Tests

    [Fact]
    public async Task RetryBehavior_FixedDelayStrategy_UsesConstantDelay()
    {
        // Arrange
        var delays = new List<TimeSpan>();
        var options = new RetryBehaviorOptions
        {
            MaxRetryCount = 3,
            BaseDelay = TimeSpan.FromMilliseconds(100),
            Strategy = RetryStrategy.FixedDelay
        };
        var behavior = CreateBehaviorWithDelayTracking(options, delays);

        var attempt = 0;
        async Task<string> Handler()
        {
            attempt++;
            if (attempt <= 3)
                throw new TransientException("Fail");
            return "success";
        }

        // Act
        try
        {
            await behavior.Handle(new TestRequest(), CancellationToken.None, Handler);
        }
        catch { }

        // Assert - Fixed delay should be used for all retries
        Assert.Equal(3, delays.Count);
        foreach (var delay in delays)
        {
            Assert.Equal(100, delay.TotalMilliseconds);
        }
    }

    [Fact]
    public async Task RetryBehavior_LinearBackoffStrategy_IncreasesLinearly()
    {
        // Arrange
        var delays = new List<TimeSpan>();
        var options = new RetryBehaviorOptions
        {
            MaxRetryCount = 3,
            BaseDelay = TimeSpan.FromMilliseconds(100),
            Strategy = RetryStrategy.LinearBackoff
        };
        var behavior = CreateBehaviorWithDelayTracking(options, delays);

        var attempt = 0;
        async Task<string> Handler()
        {
            attempt++;
            if (attempt <= 3)
                throw new TransientException("Fail");
            return "success";
        }

        // Act
        try
        {
            await behavior.Handle(new TestRequest(), CancellationToken.None, Handler);
        }
        catch { }

        // Assert - Linear backoff: 100ms, 200ms, 300ms
        Assert.Equal(3, delays.Count);
        Assert.Equal(100, delays[0].TotalMilliseconds);
        Assert.Equal(200, delays[1].TotalMilliseconds);
        Assert.Equal(300, delays[2].TotalMilliseconds);
    }

    [Fact]
    public async Task RetryBehavior_ExponentialBackoffStrategy_IncreasesExponentially()
    {
        // Arrange
        var delays = new List<TimeSpan>();
        var options = new RetryBehaviorOptions
        {
            MaxRetryCount = 3,
            BaseDelay = TimeSpan.FromMilliseconds(100),
            BackoffMultiplier = 2.0,
            Strategy = RetryStrategy.ExponentialBackoff
        };
        var behavior = CreateBehaviorWithDelayTracking(options, delays);

        var attempt = 0;
        async Task<string> Handler()
        {
            attempt++;
            if (attempt <= 3)
                throw new TransientException("Fail");
            return "success";
        }

        // Act
        try
        {
            await behavior.Handle(new TestRequest(), CancellationToken.None, Handler);
        }
        catch { }

        // Assert - Exponential backoff: 100ms, 200ms, 400ms
        Assert.Equal(3, delays.Count);
        Assert.Equal(100, delays[0].TotalMilliseconds);
        Assert.Equal(200, delays[1].TotalMilliseconds);
        Assert.Equal(400, delays[2].TotalMilliseconds);
    }

    [Fact]
    public async Task RetryBehavior_ExponentialWithJitter_HasSomeRandomness()
    {
        // Arrange
        var delays = new List<TimeSpan>();
        var options = new RetryBehaviorOptions
        {
            MaxRetryCount = 3,
            BaseDelay = TimeSpan.FromMilliseconds(100),
            BackoffMultiplier = 2.0,
            JitterFactor = 0.5, // 50% jitter
            Strategy = RetryStrategy.ExponentialBackoffWithJitter
        };
        var behavior = CreateBehaviorWithDelayTracking(options, delays);

        var attempt = 0;
        async Task<string> Handler()
        {
            attempt++;
            if (attempt <= 3)
                throw new TransientException("Fail");
            return "success";
        }

        // Act
        try
        {
            await behavior.Handle(new TestRequest(), CancellationToken.None, Handler);
        }
        catch { }

        // Assert - Delays should be within expected range with jitter
        Assert.Equal(3, delays.Count);
        
        // First retry: base 100ms with 50% jitter (50ms) = 50-150ms range
        Assert.InRange(delays[0].TotalMilliseconds, 50, 150);
    }

    [Fact]
    public async Task RetryBehavior_MaxDelay_CapsExcessiveDelays()
    {
        // Arrange
        var delays = new List<TimeSpan>();
        var options = new RetryBehaviorOptions
        {
            MaxRetryCount = 5,
            BaseDelay = TimeSpan.FromMilliseconds(100),
            MaxDelay = TimeSpan.FromMilliseconds(500),
            Strategy = RetryStrategy.ExponentialBackoff
        };
        var behavior = CreateBehaviorWithDelayTracking(options, delays);

        var attempt = 0;
        async Task<string> Handler()
        {
            attempt++;
            if (attempt <= 5)
                throw new TransientException("Fail");
            return "success";
        }

        // Act
        try
        {
            await behavior.Handle(new TestRequest(), CancellationToken.None, Handler);
        }
        catch { }

        // Assert - All delays should be capped at 500ms
        foreach (var delay in delays)
        {
            Assert.InRange(delay.TotalMilliseconds, 0, 500);
        }
    }

    #endregion

    #region Callback Tests

    [Fact]
    public async Task RetryBehavior_OnRetryCallback_InvokedOnEachRetry()
    {
        // Arrange
        var retryCallbacks = new List<(int attempt, TimeSpan delay)>();
        var options = new RetryBehaviorOptions
        {
            MaxRetryCount = 3,
            BaseDelay = TimeSpan.FromMilliseconds(1),
            OnRetry = (type, attempt, delay, ex) => retryCallbacks.Add((attempt, delay))
        };
        var behavior = CreateBehavior(options);

        var attempt = 0;
        async Task<string> Handler()
        {
            attempt++;
            if (attempt <= 3)
                throw new TransientException("Fail");
            return "success";
        }

        // Act
        try
        {
            await behavior.Handle(new TestRequest(), CancellationToken.None, Handler);
        }
        catch { }

        // Assert - Should have 3 retry callbacks (not including initial attempt)
        Assert.Equal(3, retryCallbacks.Count);
        Assert.Equal(1, retryCallbacks[0].attempt);
        Assert.Equal(2, retryCallbacks[1].attempt);
        Assert.Equal(3, retryCallbacks[2].attempt);
    }

    [Fact]
    public async Task RetryBehavior_OnRetryExhaustedCallback_InvokedWhenExhausted()
    {
        // Arrange
        var exhaustedCallbacks = new List<(int attempts, Exception ex)>();
        var options = new RetryBehaviorOptions
        {
            MaxRetryCount = 3,
            BaseDelay = TimeSpan.FromMilliseconds(1),
            OnRetryExhausted = (type, attempts, ex) => exhaustedCallbacks.Add((attempts, ex))
        };
        var behavior = CreateBehavior(options);

        async Task<string> Handler()
        {
            throw new TransientException("Always fails");
        }

        // Act & Assert
        await Assert.ThrowsAsync<TransientException>(() =>
            behavior.Handle(new TestRequest(), CancellationToken.None, Handler));

        // Assert - Should have one exhausted callback
        Assert.Single(exhaustedCallbacks);
        Assert.Equal(4, exhaustedCallbacks[0].attempts); // 1 initial + 3 retries
        Assert.IsType<TransientException>(exhaustedCallbacks[0].ex);
    }

    #endregion

    #region Options Validation Tests

    [Fact]
    public void RetryBehaviorOptions_InvalidMaxRetryCount_Throws()
    {
        var options = new RetryBehaviorOptions { MaxRetryCount = -1 };
        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Fact]
    public void RetryBehaviorOptions_InvalidBaseDelay_Throws()
    {
        var options = new RetryBehaviorOptions { BaseDelay = TimeSpan.Zero };
        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Fact]
    public void RetryBehaviorOptions_InvalidMaxDelay_Throws()
    {
        var options = new RetryBehaviorOptions { MaxDelay = TimeSpan.Zero };
        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Fact]
    public void RetryBehaviorOptions_MaxDelayLessThanBaseDelay_Throws()
    {
        var options = new RetryBehaviorOptions 
        { 
            BaseDelay = TimeSpan.FromSeconds(10), 
            MaxDelay = TimeSpan.FromSeconds(1) 
        };
        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Fact]
    public void RetryBehaviorOptions_InvalidJitterFactor_Throws()
    {
        var options = new RetryBehaviorOptions { JitterFactor = 1.5 };
        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Fact]
    public void RetryBehaviorOptions_ValidOptions_NoThrow()
    {
        var options = new RetryBehaviorOptions
        {
            MaxRetryCount = 3,
            BaseDelay = TimeSpan.FromSeconds(1),
            MaxDelay = TimeSpan.FromSeconds(30),
            BackoffMultiplier = 2.0,
            JitterFactor = 0.1
        };
        options.Validate(); // Should not throw
    }

    #endregion

    #region Helper Methods

    private RetryBehavior<TestRequest, string> CreateBehavior(RetryBehaviorOptions options)
    {
        var logger = NullLogger<RetryBehavior<TestRequest, string>>.Instance;
        return new RetryBehavior<TestRequest, string>(options, logger);
    }

    private RetryBehavior<TestRequest, string> CreateBehaviorWithDelayTracking(
        RetryBehaviorOptions options, 
        List<TimeSpan> delays)
    {
        var logger = NullLogger<RetryBehavior<TestRequest, string>>.Instance;
        
        // Wrap the original OnRetry to capture delays
        var originalOnRetry = options.OnRetry;
        options.OnRetry = (type, attempt, delay, ex) =>
        {
            delays.Add(delay);
            originalOnRetry?.Invoke(type, attempt, delay, ex);
        };

        return new RetryBehavior<TestRequest, string>(options, logger);
    }

    #endregion

    #region Test Types

    private class TestRequest : IRequest<string> { }

    private class TransientException : Exception
    {
        public TransientException(string message) : base(message) { }
    }

    private class NonTransientException : Exception
    {
        public NonTransientException(string message) : base(message) { }
    }

    #endregion
}
