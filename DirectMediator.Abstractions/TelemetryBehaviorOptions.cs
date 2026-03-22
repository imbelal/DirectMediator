using System;
using System.Diagnostics;

namespace DirectMediator;

/// <summary>
/// Configuration options for the TelemetryBehavior.
/// </summary>
public class TelemetryBehaviorOptions
{
    /// <summary>
    /// The name of the OpenTelemetry ActivitySource. Defaults to "DirectMediator".
    /// </summary>
    public string ActivitySourceName { get; set; } = "DirectMediator";

    /// <summary>
    /// Whether to enable tracing. Defaults to true.
    /// </summary>
    public bool EnableTracing { get; set; } = true;

    /// <summary>
    /// Whether to enable metrics. Defaults to true.
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// Custom ActivitySource instance. If provided, ActivitySourceName is ignored.
    /// </summary>
    public ActivitySource? ActivitySource { get; set; }

    /// <summary>
    /// Gets the ActivitySource to use for tracing.
    /// </summary>
    internal ActivitySource? GetActivitySource()
    {
        return ActivitySource ?? (EnableTracing ? new ActivitySource(ActivitySourceName) : null);
    }

    /// <summary>
    /// Validates the options.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ActivitySourceName))
        {
            throw new ArgumentException("ActivitySourceName cannot be null or empty", nameof(ActivitySourceName));
        }

        if (EnableTracing && ActivitySource == null && string.IsNullOrWhiteSpace(ActivitySourceName))
        {
            throw new ArgumentException("ActivitySourceName must be provided when EnableTracing is true and no ActivitySource is provided", nameof(ActivitySourceName));
        }
    }
}
