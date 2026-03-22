# DirectMediator v1.0.2 Release Notes

**Release Date:** March 22, 2026

---

## Highlights

DirectMediator v1.0.2 introduces a comprehensive set of production-ready pipeline behaviors including caching, validation, distributed tracing, retry mechanisms, and OpenTelemetry integration. This release also includes significant performance improvements to the test suite.

---

## New Features

### 1. Caching Infrastructure

Added a full caching infrastructure with the [`ICacheableRequest<TResponse>`](DirectMediator.Abstractions/ICacheableRequest.cs:17) interface and [`CachingBehavior<TRequest,TResponse>`](DirectMediator.Abstractions/CachingBehavior.cs:1).

#### [`CachingBehaviorOptions`](DirectMediator.Abstractions/CachingBehaviorOptions.cs:7)

Configuration options for default TTL:

- **`DefaultDuration`** - Default cache duration (default: 5 minutes)

#### Usage Example

```csharp
// Define a cacheable request
public record GetProductQuery(int Id) : ICacheableRequest<Product>
{
    public string CacheKey => $"product:{Id}";
    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(10); // null = use default
}

// Register in DI
services.AddMemoryCache();
services.AddDirectMediator()
        .AddDirectMediatorCaching(TimeSpan.FromMinutes(5));
```

---

### 2. Validation Behavior with FluentValidation Integration

Added [`ValidationBehavior<TRequest,TResponse>`](DirectMediator.Abstractions/ValidationBehavior.cs:19) for request validation using FluentValidation.

#### Features

- Runs all registered [`IValidator<TRequest>`](https://docs.fluentvalidation.net/) instances before the handler is invoked
- Throws [`ValidationException`](https://docs.fluentvalidation.net/en/latest/validation-exceptions.html) if any validation fails
- Passes through unchanged when no validators are registered

#### Usage Example

```csharp
// Create a validator
public class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0);
        RuleFor(x => x.Quantity).InclusiveBetween(1, 100);
        RuleFor(x => x.CustomerEmail).EmailAddress();
    }
}

// Register validator and behavior
services.AddSingleton<IValidator<CreateOrderCommand>, CreateOrderCommandValidator>();
services.AddDirectMediator()
        .AddDirectMediatorValidation();
```

---

### 3. Correlation ID Behavior for Distributed Tracing

Added [`CorrelationIdBehavior<TRequest,TResponse>`](DirectMediator.Abstractions/CorrelationIdBehavior.cs:11) for distributed tracing with unique correlation IDs.

#### Features

- Generates unique correlation IDs for each request
- Propagates parent correlation ID for distributed tracing
- Uses [`ICorrelationContext`](DirectMediator.Abstractions/ICorrelationContext.cs:1) for request-scoped storage
- Logs correlation IDs for traceability

#### Usage Example

```csharp
// Register in DI
services.AddDirectMediator()
        .AddDirectMediatorCorrelationId();

// Access correlation ID in handlers or services
public class MyHandler : IRequestHandler<MyRequest, MyResponse>
{
    private readonly ICorrelationContext _correlationContext;
    
    public async Task<MyResponse> Handle(MyRequest request, CancellationToken ct)
    {
        var correlationId = _correlationContext.CorrelationId;
        var parentId = _correlationContext.ParentCorrelationId;
        
        // Use for logging, HTTP headers, etc.
    }
}
```

---

### 4. Retry Behavior with Automatic Retry

Added [`RetryBehavior<TRequest,TResponse>`](DirectMediator.Abstractions/RetryBehavior.cs:14) with automatic retry, exponential backoff, and jitter.

#### [`RetryBehaviorOptions`](DirectMediator.Abstractions/RetryBehaviorOptions.cs:9)

- **`MaxRetryCount`** - Maximum retry attempts (default: 3)
- **`BaseDelay`** - Base delay between retries (default: 1 second)
- **`MaxDelay`** - Maximum delay cap (default: 30 seconds)
- **`BackoffMultiplier`** - Exponential multiplier (default: 2.0)
- **`JitterFactor`** - Randomness factor 0.0-1.0 (default: 0.1)
- **`Strategy`** - Retry strategy: `FixedDelay`, `LinearBackoff`, `ExponentialBackoff`, `ExponentialBackoffWithJitter`
- **`ShouldRetryOnException`** - Predicate to filter which exceptions trigger retries
- **`OnRetry`** - Callback before each retry
- **`OnRetryExhausted`** - Callback when all retries are exhausted

#### Usage Example

```csharp
// Default configuration (3 retries, exponential backoff with jitter)
services.AddDirectMediator()
        .AddDirectMediatorRetry();

// Custom configuration
services.AddDirectMediator()
        .AddDirectMediatorRetry(options =>
        {
            options.MaxRetryCount = 5;
            options.BaseDelay = TimeSpan.FromSeconds(2);
            options.Strategy = RetryStrategy.ExponentialBackoffWithJitter;
            options.JitterFactor = 0.2;
            options.ShouldRetryOnException = ex => 
                ex is HttpRequestException || 
                ex is TimeoutException ||
                ex is IOException;
            options.OnRetry = (type, attempt, delay, ex) => 
                Console.WriteLine($"Retrying {type.Name} attempt {attempt} after {delay}");
            options.OnRetryExhausted = (type, attempts, ex) => 
                Console.WriteLine($"All retries exhausted for {type.Name}");
        });
```

---

### 5. Telemetry Behavior with OpenTelemetry Integration

Added [`TelemetryBehavior<TRequest,TResponse>`](DirectMediator.Abstractions/TelemetryBehavior.cs:13) with OpenTelemetry integration.

#### [`TelemetryBehaviorOptions`](DirectMediator.Abstractions/TelemetryBehaviorOptions.cs:9)

- **`ActivitySourceName`** - OpenTelemetry ActivitySource name (default: "DirectMediator")
- **`EnableTracing`** - Enable distributed tracing (default: true)
- **`EnableMetrics`** - Enable metrics collection (default: true)
- **`ActivitySource`** - Custom ActivitySource instance

#### Features

- Distributed tracing via `System.Diagnostics.ActivitySource`
- Request duration metrics
- Error counting and reporting
- Activity tags with request type and library name

#### Usage Example

```csharp
// Default configuration
services.AddDirectMediator()
        .AddDirectMediatorTelemetry();

// Custom configuration
services.AddDirectMediator()
        .AddDirectMediatorTelemetry(options =>
        {
            options.ActivitySourceName = "MyApp.DirectMediator";
            options.EnableTracing = true;
            options.EnableMetrics = true;
        });

// Using custom ActivitySource
var activitySource = new ActivitySource("MyApp");
services.AddDirectMediator()
        .AddDirectMediatorTelemetry(options =>
        {
            options.ActivitySource = activitySource;
        });
```

---

## Improvements

### Test Performance

- **Test execution time reduced from ~3 minutes to 12 seconds (93% improvement)**
- Achieved through optimized test parallelization and reduced artificial delays in tests

### Code Quality

- Cleaned up unnecessary `using` statements across the codebase
- Added comprehensive unit tests for all new behaviors

---

## Breaking Changes

**None** - This release is fully backward compatible with v1.0.1.

---

## Full Changelog

### Added
- `ICacheableRequest<TResponse>` interface for cacheable request markers
- `CachingBehavior<TRequest,TResponse>` for response caching
- `CachingBehaviorOptions` for default TTL configuration
- `ValidationBehavior<TRequest,TResponse>` with FluentValidation integration
- `CorrelationIdBehavior<TRequest,TResponse>` for distributed tracing
- `CorrelationContext` implementation of `ICorrelationContext`
- `RetryBehavior<TRequest,TResponse>` with automatic retry support
- `RetryBehaviorOptions` with exponential backoff and jitter
- `RetryStrategy` enum (FixedDelay, LinearBackoff, ExponentialBackoff, ExponentialBackoffWithJitter)
- `TelemetryBehavior<TRequest,TResponse>` with OpenTelemetry integration
- `TelemetryBehaviorOptions` for tracing and metrics configuration
- Comprehensive unit tests for all behaviors

### Improved
- Test execution time reduced by 93%
- Code cleanup: removed unnecessary using statements

---

## Upgrade Guide

To upgrade from v1.0.1, simply update your package reference. All new features are opt-in:

```csharp
// Add any new behaviors you want to use
services.AddDirectMediator()
        .AddDirectMediatorCaching()
        .AddDirectMediatorValidation()
        .AddDirectMediatorCorrelationId()
        .AddDirectMediatorRetry()
        .AddDirectMediatorTelemetry();
```

---

## Dependencies

- **FluentValidation** - Required for `ValidationBehavior`
- **Microsoft.Extensions.Caching.Memory** - Required for `CachingBehavior`
- **System.Diagnostics.DiagnosticSource** - Required for `TelemetryBehavior` (included in .NET 6+)

---

## Links

- [NuGet Package](https://www.nuget.org/packages/DirectMediator/)
- [Documentation](https://github.com/your-repo/DirectMediator#readme)
- [Sample Application](DirectMediator.Sample/)
