using DirectMediator;
using DirectMediator.Generated;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DirectMediator.Sample;

Console.WriteLine("=== DirectMediator Sample Application ===\n");

var services = new ServiceCollection();

// Configure logging
services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Information));

// ✅ Add memory cache (required for AddDirectMediatorCaching)
services.AddMemoryCache();

// ✅ Auto-register all handlers + dispatchers + IMediator
services.AddDirectMediator()
    .AddDirectMediatorLogging()              // Logs every request via ILogger
    .AddDirectMediatorPerformanceBehavior()  // Warns when a request exceeds threshold
    .AddDirectMediatorCaching(defaultCacheDuration: TimeSpan.FromMinutes(5))  // Response caching
    .AddDirectMediatorValidation()          // FluentValidation integration
    .AddDirectMediatorCorrelationId()        // Correlation ID for distributed tracing
    .AddDirectMediatorRetry(options =>       // Automatic retry for transient failures
    {
        options.MaxRetryCount = 3;
        options.BaseDelay = TimeSpan.FromMilliseconds(50);
        options.Strategy = RetryStrategy.ExponentialBackoffWithJitter;
        options.JitterFactor = 0.3;
        options.ShouldRetryOnException = ex => ex is TransientFailureException;
    });

// ✅ Register validators (required for ValidationBehavior)
services.AddSingleton<IValidator<CreateOrderCommand>, CreateOrderCommandValidator>();

var provider = services.BuildServiceProvider();

// --- Use the unified IMediator interface (recommended) ---
var mediator = provider.GetRequiredService<IMediator>();
var correlationContext = provider.GetRequiredService<ICorrelationContext>();

// ============================================================
// Example 1: Send a command (with validation)
// ============================================================
Console.WriteLine("--- Example 1: Command with Validation ---");

try
{
    // Valid command - should succeed
    await mediator.Send(new CreateOrderCommand("Tile"));
    Console.WriteLine($"  Correlation ID (after first request): {correlationContext.CorrelationId}");
    
    // Invalid command - should throw ValidationException
    await mediator.Send(new CreateOrderCommand(""));
}
catch (ValidationException ex)
{
    Console.WriteLine("Validation failed (expected for empty product name):");
    foreach (var error in ex.Errors)
    {
        Console.WriteLine($"  - {error.PropertyName}: {error.ErrorMessage}");
    }
}

// ============================================================
// Example 2: Execute a query
// ============================================================
Console.WriteLine("\n--- Example 2: Simple Query ---");
var queryResult = await mediator.Send(new GetOrderQuery(123));
Console.WriteLine($"Query result: {queryResult}");

// ============================================================
// Example 3: Cacheable query with response caching
// ============================================================
Console.WriteLine("\n--- Example 3: Cacheable Query ---");
var productQuery = new GetProductQuery(1);

// First call - should hit the handler
Console.WriteLine("First call to GetProductQuery(1):");
var product1 = await mediator.Send(productQuery);
Console.WriteLine($"  Result: {product1}");

// Second call - should use cached response
Console.WriteLine("Second call to GetProductQuery(1) (should be cached):");
var product2 = await mediator.Send(productQuery);
Console.WriteLine($"  Result: {product2}");

// Different ID - should hit the handler
Console.WriteLine("Call to GetProductQuery(2) (different key):");
var product3 = await mediator.Send(new GetProductQuery(2));
Console.WriteLine($"  Result: {product3}");

// ============================================================
// Example 4: Publish a notification
// ============================================================
Console.WriteLine("\n--- Example 4: Notification ---");
await mediator.Publish(new OrderCreatedNotification("Tile"));

// ============================================================
// Example 5: Using individual dispatchers (also supported)
// ============================================================
Console.WriteLine("\n--- Example 5: Individual Dispatchers ---");
var commandDispatcher = provider.GetRequiredService<CommandDispatcher>();
var queryDispatcher = provider.GetRequiredService<QueryDispatcher>();
var publisher = provider.GetRequiredService<NotificationPublisher>();

await commandDispatcher.Send(new CreateOrderCommand("Widget"), default);
var result = await queryDispatcher.Query(new GetOrderQuery(456), default);
Console.WriteLine($"QueryDispatcher result: {result}");
await publisher.Publish(new OrderCreatedNotification("Widget"), default);

// ============================================================
// Example 6: RetryBehavior - Automatic retry for transient failures
// ============================================================
Console.WriteLine("\n--- Example 6: RetryBehavior ---");

try
{
    // This command will fail 2 times before succeeding
    // The RetryBehavior will automatically retry with exponential backoff
    var retryResult = await mediator.Send(new RetryCommand("Test Product", FailCount: 2));
    Console.WriteLine($"  Retry command succeeded: {retryResult}");
}
catch (TransientFailureException ex)
{
    Console.WriteLine($"  Retry command failed after all retries: {ex.Message}");
}

Console.WriteLine("\n=== Sample completed successfully! ===");
