using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DirectMediator.Tests;

public class TelemetryBehaviorTests
{
    [Fact]
    public async Task TelemetryBehavior_SuccessfulRequest_IncrementsCounter()
    {
        // Arrange
        TelemetryBehavior<TestRequest, string>.ResetCounters();
        var options = new TelemetryBehaviorOptions { EnableMetrics = true };
        var behavior = CreateBehavior(options);

        async Task<string> Handler() => "success";

        // Act
        var result = await behavior.Handle(new TestRequest(), CancellationToken.None, Handler);

        // Assert
        Assert.Equal("success", result);
        Assert.Equal(1, TelemetryBehavior<TestRequest, string>.GetRequestCount());
        Assert.Equal(0, TelemetryBehavior<TestRequest, string>.GetErrorCount());
    }

    [Fact]
    public async Task TelemetryBehavior_FailedRequest_IncrementsErrorCounter()
    {
        // Arrange
        TelemetryBehavior<TestRequest, string>.ResetCounters();
        var options = new TelemetryBehaviorOptions { EnableMetrics = true };
        var behavior = CreateBehavior(options);

        async Task<string> Handler()
        {
            throw new InvalidOperationException("Test error");
        }

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.Handle(new TestRequest(), CancellationToken.None, Handler));

        Assert.Equal(1, TelemetryBehavior<TestRequest, string>.GetRequestCount());
        Assert.Equal(1, TelemetryBehavior<TestRequest, string>.GetErrorCount());
    }

    [Fact]
    public async Task TelemetryBehavior_DisabledMetrics_DoesNotIncrement()
    {
        // Arrange
        TelemetryBehavior<TestRequest, string>.ResetCounters();
        var options = new TelemetryBehaviorOptions { EnableMetrics = false };
        var behavior = CreateBehavior(options);

        async Task<string> Handler() => "success";

        // Act
        await behavior.Handle(new TestRequest(), CancellationToken.None, Handler);

        // Assert
        Assert.Equal(0, TelemetryBehavior<TestRequest, string>.GetRequestCount());
    }

    [Fact]
    public async Task TelemetryBehavior_WithTracing_CreatesActivity()
    {
        // Arrange
        TelemetryBehavior<TestRequest, string>.ResetCounters();
        using var activitySource = new System.Diagnostics.ActivitySource("TestSource");
        var options = new TelemetryBehaviorOptions
        {
            EnableTracing = true,
            ActivitySource = activitySource
        };
        var behavior = CreateBehavior(options);

        async Task<string> Handler() => "success";

        // Act
        var result = await behavior.Handle(new TestRequest(), CancellationToken.None, Handler);

        // Assert
        Assert.Equal("success", result);
    }

    [Fact]
    public void TelemetryBehaviorOptions_InvalidActivitySourceName_Throws()
    {
        // Arrange
        var options = new TelemetryBehaviorOptions { ActivitySourceName = "" };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Fact]
    public void TelemetryBehaviorOptions_ValidOptions_NoThrow()
    {
        // Arrange
        var options = new TelemetryBehaviorOptions
        {
            ActivitySourceName = "TestActivity",
            EnableTracing = true,
            EnableMetrics = true
        };

        // Act & Assert
        options.Validate(); // Should not throw
    }

    #region Helper Methods

    private TelemetryBehavior<TestRequest, string> CreateBehavior(TelemetryBehaviorOptions options)
    {
        var logger = NullLogger<TelemetryBehavior<TestRequest, string>>.Instance;
        return new TelemetryBehavior<TestRequest, string>(options, logger);
    }

    #endregion

    #region Test Types

    private class TestRequest : IRequest<string> { }

    #endregion
}
