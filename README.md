# DirectMediator

A zero-reflection mediator for .NET powered by **C# source generators**. DirectMediator generates all dispatcher and publisher code at compile time, so there is no runtime reflection, no dictionary lookups, and no dynamic dispatch — just direct, strongly-typed method calls.

---

## Features

- ⚡ **Zero reflection** — all routing is generated at compile time
- 🔒 **Compile-time safety** — errors for duplicate or missing handlers are reported as build errors
- 💉 **DI-first** — single `AddDirectMediator()` call registers every handler and dispatcher
- 📦 **Lightweight** — core depends only on `Microsoft.Extensions.*` abstractions (`DependencyInjection`, `Logging.Abstractions`, `Caching.Abstractions`); enabling built-in behaviors may require additional implementation packages (e.g., logging providers, `Microsoft.Extensions.Caching.Memory` + `AddMemoryCache()`)
- 🔀 **CQRS-ready** — first-class support for Commands, Queries, and Notifications
- 🎯 **Unified interface** — single `IMediator` combining Send and Publish for easy injection and mocking
- 🔗 **Compile-time pipeline** — `IPipelineBehavior<TRequest, TResponse>` chains are built **once at construction** (no per-dispatch reflection or service location)
- 📋 **Built-in behaviors** — opt-in `LoggingBehavior`, `PerformanceBehavior`, `CachingBehavior`, and `ValidationBehavior` ready to use
- 💾 **Response caching** — implement `ICacheableRequest<TResponse>` on any request (command or query) to get automatic in-memory caching with a per-request configurable TTL
- ✅ **Request validation** — integrate [FluentValidation](https://docs.fluentvalidation.net/) via `AddDirectMediatorValidation()`; validators run before the handler and throw `FluentValidation.ValidationException` on failure

---

## How It Works

DirectMediator uses an [Incremental Source Generator](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview) (`DirectMediator.Generator`) to inspect your project at build time. It:

1. Discovers every class that implements `IRequestHandler<TRequest, TResponse>` or `INotificationHandler<TNotification>`.
2. Emits compile-time diagnostics if handlers are duplicated or missing.
3. Generates dispatchers (`CommandDispatcher`, `QueryDispatcher`, `NotificationPublisher`), a unified `Mediator`, and an `AddDirectMediator()` extension method — all inside the `DirectMediator.Generated` namespace.

Because the routing code is generated as plain C# `switch` expressions, the JIT can inline and optimize it just like hand-written code.

---

## Installation

Add the **DirectMediator** NuGet package to your project:

```xml
<PackageReference Include="DirectMediator" Version="1.0.1" />
```

Or via the .NET CLI:

```bash
dotnet add package DirectMediator
```

---

## Quick Start

### 1. Define a Command

A command performs a side-effectful operation and returns no meaningful value (`Unit`).

```csharp
using DirectMediator;

public record CreateOrderCommand(string Product) : ICommand;
```

### 2. Implement the Command Handler

```csharp
using DirectMediator;

public class CreateOrderHandler : IRequestHandler<CreateOrderCommand, Unit>
{
    public Task<Unit> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Order created: {request.Product}");
        return Task.FromResult(Unit.Value);
    }
}
```

### 3. Define a Query

A query reads data and returns a typed result.

```csharp
using DirectMediator;

public record GetOrderQuery(int Id) : IQuery<string>;
```

### 4. Implement the Query Handler

```csharp
using DirectMediator;

public class GetOrderHandler : IRequestHandler<GetOrderQuery, string>
{
    public Task<string> Handle(GetOrderQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult($"Order #{request.Id}");
    }
}
```

### 5. Define a Notification

A notification broadcasts an event to multiple handlers.

```csharp
using DirectMediator;

public record OrderCreatedNotification(string Product) : INotification;
```

### 6. Implement a Notification Handler

```csharp
using DirectMediator;

public class OrderCreatedHandler : INotificationHandler<OrderCreatedNotification>
{
    public Task Handle(OrderCreatedNotification notification, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Notification received: {notification.Product}");
        return Task.CompletedTask;
    }
}
```

### 7. Register and Use

Call `AddDirectMediator()` once during startup. The source generator automatically includes every handler it discovers.

```csharp
using DirectMediator;
using DirectMediator.Generated;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// Registers all handlers, CommandDispatcher, QueryDispatcher, NotificationPublisher, and IMediator
services.AddDirectMediator();

var provider = services.BuildServiceProvider();

// --- Recommended: inject the unified IMediator interface ---
var mediator = provider.GetRequiredService<IMediator>();

// Send a command (returns Unit)
await mediator.Send(new CreateOrderCommand("Tile"));

// Execute a query (returns the typed response)
string result = await mediator.Send(new GetOrderQuery(123));
Console.WriteLine($"Query result: {result}");

// Publish a notification
await mediator.Publish(new OrderCreatedNotification("Tile"));
```

The individual dispatchers are still available if you need to inject only part of the API:

```csharp
var commandDispatcher = provider.GetRequiredService<CommandDispatcher>();
var queryDispatcher   = provider.GetRequiredService<QueryDispatcher>();
var publisher         = provider.GetRequiredService<NotificationPublisher>();
```

---

## Core Concepts

### Commands

A **command** represents an intention to change state. It implements `ICommand`, which in turn extends `IRequest<Unit>`. Commands produce no return value — the `Unit` type serves as a stand-in for `void`.

```
ICommand  ──extends──▶  IRequest<Unit>
```

Commands are dispatched through `ICommandDispatcher.Send<TCommand>(command, cancellationToken)`.

### Queries

A **query** retrieves data without modifying state. It implements `IQuery<TResponse>`, which extends `IRequest<TResponse>`.

```
IQuery<TResponse>  ──extends──▶  IRequest<TResponse>
```

Queries are dispatched through the generated `QueryDispatcher.Query(query, cancellationToken)` method, which is typed to the specific request/response pair.

### Notifications

A **notification** broadcasts an event to zero or more handlers. It implements `INotification`. Multiple handlers for the same notification type are all invoked in sequence.

Notifications are published through `INotificationPublisher.Publish<TNotification>(notification, cancellationToken)`.

### Handlers

| Interface | Purpose |
|-----------|---------|
| `IRequestHandler<TRequest, TResponse>` | Handles both commands (`TResponse = Unit`) and queries |
| `INotificationHandler<TNotification>` | Handles a specific notification type |

### Unit

`Unit` is a value type used as the return type for commands (equivalent to `void`). Access the singleton value via `Unit.Value`.

### IMediator

`IMediator` is the unified injectable abstraction. It combines command dispatch, query dispatch, and notification publishing into a single interface, which is especially useful for:

- **Controller / service injection** — inject one interface instead of three
- **Unit testing** — mock only `IMediator` rather than multiple dispatchers

```csharp
public interface IMediator : INotificationPublisher
{
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}
```

Commands produce a `Unit` result; queries produce their typed response. Both go through the same `Send` method.

```csharp
// In a controller or service:
public class OrdersController
{
    private readonly IMediator _mediator;
    public OrdersController(IMediator mediator) => _mediator = mediator;

    public async Task Create(string product)
        => await _mediator.Send(new CreateOrderCommand(product));

    public async Task<string> Get(int id)
        => await _mediator.Send(new GetOrderQuery(id));
}
```

### Pipeline Behaviors

Pipeline behaviors let you add cross-cutting concerns that wrap every request passing through the mediator — logging, validation, caching, exception handling, and more. They are inspired by ASP.NET Core middleware.

The pipeline chain is **built once at dispatcher construction time** as a delegate chain. There is no per-dispatch reflection or `GetServices` call; dispatching is a direct delegate invocation.

#### Custom behavior

Implement `IPipelineBehavior<TRequest, TResponse>` and register it with DI:

```csharp
using DirectMediator;

public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        CancellationToken cancellationToken,
        RequestHandlerDelegate<TResponse> next)
    {
        // pre-processing (e.g., validate request)
        var response = await next();
        // post-processing
        return response;
    }
}
```

Register with DI (open-generic covers all request types):

```csharp
services.AddDirectMediator();
// ⚠️ Behaviors must be registered as singletons — the pipeline is built once at
// dispatcher construction time, so behaviors live for the lifetime of the singleton.
services.AddSingleton(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
```

Multiple behaviors execute in registration order (first registered = outermost wrapper).

#### Built-in behaviors

DirectMediator ships four ready-to-use behaviors in the `DirectMediator.Abstractions` package:

| Behavior | Description |
|----------|-------------|
| `LoggingBehavior<TRequest,TResponse>` | Logs `Handling` / `Handled` messages and errors via `ILogger<TRequest>` |
| `PerformanceBehavior<TRequest,TResponse>` | Logs a warning when a request exceeds a configurable threshold (default: 500 ms) |
| `CachingBehavior<TRequest,TResponse>` | Caches responses in `IMemoryCache` for requests that implement `ICacheableRequest<TResponse>`; non-cacheable requests pass through unchanged |
| `ValidationBehavior<TRequest,TResponse>` | Runs all registered `IValidator<TRequest>` instances before the handler; throws `ValidationException` if any rule fails; requests with no validators pass through unchanged |

Opt-in with the provided extension methods:

```csharp
services.AddMemoryCache();                   // required if using AddDirectMediatorCaching()
services.AddDirectMediator()
        .AddDirectMediatorLogging()              // ILogger-based request tracing
        .AddDirectMediatorPerformanceBehavior()  // warns on slow requests
        .AddDirectMediatorCaching()              // in-memory response caching (default TTL: 5 min)
        .AddDirectMediatorValidation();          // FluentValidation request validation
```

Override the global default TTL by passing a `defaultCacheDuration` to `AddDirectMediatorCaching()`:

```csharp
services.AddMemoryCache();                   // required if using AddDirectMediatorCaching()
services.AddDirectMediator()
        .AddDirectMediatorCaching(defaultCacheDuration: TimeSpan.FromMinutes(10));
```

Adjust the slow-request threshold per-instance via the constructor:

```csharp
// Register with a custom 200 ms threshold for CreateOrderCommand
services.AddSingleton<IPipelineBehavior<CreateOrderCommand, Unit>>(sp =>
    new PerformanceBehavior<CreateOrderCommand, Unit>(
        sp.GetRequiredService<ILogger<CreateOrderCommand>>(),
        slowThresholdMs: 200));
```

#### Opting in to caching per request

Implement `ICacheableRequest<TResponse>` on any query to have its response cached automatically:

```csharp
public record GetProductQuery(int ProductId) : ICacheableRequest<Product>
{
    // Unique key used to store/retrieve the cached value
    public string CacheKey => $"product:{ProductId}";

    // Per-request TTL; null = use the default configured in AddDirectMediatorCaching()
    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(10);
}
```

To invalidate a cache entry, call `IMemoryCache.Remove(key)` with the same key used by the request.

```csharp
_cache.Remove($"product:{productId}");
```

#### Request Validation with FluentValidation

`ValidationBehavior<TRequest, TResponse>` integrates [FluentValidation](https://docs.fluentvalidation.net/) into the DirectMediator pipeline. It runs every registered `IValidator<TRequest>` before the handler is invoked. If any rule fails a `FluentValidation.ValidationException` is thrown and the handler is never called. Requests that have no registered validators pass through unchanged.

**Installation** — add the FluentValidation package to your project:

```xml
<PackageReference Include="FluentValidation" Version="11.*" />
```

Or via the .NET CLI:

```bash
dotnet add package FluentValidation
```

**Define a validator** for a request:

```csharp
using FluentValidation;

public record CreateOrderCommand(string Product) : ICommand;

public class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.Product).NotEmpty().WithMessage("Product name is required.");
        RuleFor(x => x.Product).MaximumLength(100).WithMessage("Product name must not exceed 100 characters.");
    }
}
```

**Register** the validator and enable the behavior:

```csharp
using FluentValidation;

// Register individual validators (singleton-safe because validators are stateless)
services.AddSingleton<IValidator<CreateOrderCommand>, CreateOrderCommandValidator>();

// Enable the validation behavior
services.AddDirectMediator()
        .AddDirectMediatorValidation();
```

> **Thread-safety note:** Because dispatchers are singletons and the pipeline is built once at construction, `ValidationBehavior` is registered as a **singleton**. Any `IValidator<T>` instances it captures must therefore be thread-safe. Standard FluentValidation `AbstractValidator<T>` subclasses are stateless and safe to register as singletons.

**Handle validation failures** in your application code:

```csharp
using FluentValidation;

try
{
    await mediator.Send(new CreateOrderCommand(""));
}
catch (ValidationException ex)
{
    foreach (var failure in ex.Errors)
        Console.WriteLine($"{failure.PropertyName}: {failure.ErrorMessage}");
}
```

**Multiple validators** for the same request type are all executed; failures from every validator are aggregated into a single `ValidationException`:

```csharp
services.AddSingleton<IValidator<CreateOrderCommand>, CreateOrderCommandValidator>();
services.AddSingleton<IValidator<CreateOrderCommand>, AnotherCreateOrderCommandValidator>();
```

---

## Dependency Injection

The generated `AddDirectMediator()` extension method on `IServiceCollection`:

- Registers every discovered handler as **transient**
- Registers `CommandDispatcher`, `QueryDispatcher`, and `NotificationPublisher` as **singletons**
- Registers `IMediator` (implemented by `Mediator`) as a **singleton**

```csharp
services.AddDirectMediator();
```

All dispatchers and the unified mediator are available from the DI container:

```csharp
// Unified interface (recommended)
var mediator = provider.GetRequiredService<IMediator>();

// Individual dispatchers (still fully supported)
var commandDispatcher = provider.GetRequiredService<CommandDispatcher>();
var queryDispatcher   = provider.GetRequiredService<QueryDispatcher>();
var publisher         = provider.GetRequiredService<NotificationPublisher>();
```

---

## Compile-Time Diagnostics

The source generator validates your handler registrations at build time and reports the following errors:

| Code | Description |
|------|-------------|
| `FM001` | Multiple handlers found for the same **command** type |
| `FM002` | Multiple handlers found for the same **query** type |
| `FM003` | No handler found for a **notification** type |

These errors appear as standard MSBuild/IDE errors — you will see them in the Error List before your application ever runs.

---

## Generated Code

At build time, the generator emits a file called `DirectMediator.Generated.g.cs` inside the `DirectMediator.Generated` namespace. It contains:

- **`CommandDispatcher`** — implements `ICommandDispatcher`, routes each command type through the behavior pipeline to its handler via a `switch` expression.
- **`QueryDispatcher`** — implements `IQueryDispatcherMarker`, exposes a strongly-typed `Query(...)` method per query type, wrapped in the behavior pipeline.
- **`NotificationPublisher`** — implements `INotificationPublisher`, fans out each notification to all registered handlers via a `switch` statement.
- **`Mediator`** — implements `IMediator`, provides a unified `Send<TResponse>` that dispatches both commands and queries through the behavior pipeline, plus `Publish` for notifications.
- **`DirectMediatorServiceCollectionExtensions`** — provides the `AddDirectMediator()` extension method.

Example (abbreviated) for the sample project:

```csharp
// Auto-generated — do not edit
namespace DirectMediator.Generated
{
    public sealed class CommandDispatcher : ICommandDispatcher
    {
        private readonly CreateOrderHandler _createOrderHandler;
        // Pre-built delegate chain — built ONCE at construction, zero per-dispatch overhead
        private readonly System.Func<CreateOrderCommand, CancellationToken, Task<Unit>> _createOrderHandlerPipeline;

        public CommandDispatcher(
            CreateOrderHandler createOrderHandler,
            IEnumerable<IPipelineBehavior<CreateOrderCommand, Unit>> createOrderCommandBehaviors = null!)
        {
            _createOrderHandler = createOrderHandler;
            _createOrderHandlerPipeline = BuildPipeline<CreateOrderCommand, Unit>(
                createOrderHandler, createOrderCommandBehaviors);
        }

        public Task Send<TCommand>(TCommand command, CancellationToken ct = default)
            where TCommand : ICommand
        {
            return command switch
            {
                CreateOrderCommand c => (Task)_createOrderHandlerPipeline(c, ct),
                _ => throw new InvalidOperationException(...)
            };
        }

        // Chains behaviors into a delegate once; first-registered = outermost wrapper.
        private static System.Func<TReq, CancellationToken, Task<TResp>> BuildPipeline<TReq, TResp>(
            IRequestHandler<TReq, TResp> handler,
            IEnumerable<IPipelineBehavior<TReq, TResp>> behaviors)
            where TReq : IRequest<TResp>
        {
            var list = new List<IPipelineBehavior<TReq, TResp>>(
                behaviors ?? Enumerable.Empty<IPipelineBehavior<TReq, TResp>>());
            System.Func<TReq, CancellationToken, Task<TResp>> chain = (req, ct) => handler.Handle(req, ct);
            for (var i = list.Count - 1; i >= 0; i--)
            {
                var b = list[i];
                var inner = chain;
                chain = (req, ct) => b.Handle(req, ct, () => inner(req, ct));
            }
            return chain;
        }
    }

    public sealed class Mediator : IMediator
    {
        // constructor omitted for brevity — same pattern as CommandDispatcher

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            return request switch
            {
                CreateOrderCommand r => (Task<TResponse>)(object)_createOrderHandlerPipeline(r, ct),
                GetOrderQuery r      => (Task<TResponse>)(object)_getOrderHandlerPipeline(r, ct),
                _ => throw new InvalidOperationException(...)
            };
        }

        public async Task Publish<TNotification>(TNotification notification, CancellationToken ct = default)
            where TNotification : INotification { /* switch over notification handlers */ }
    }
}
```

---

## Project Structure

```
DirectMediator/
├── DirectMediator.Abstractions/        # Public interfaces, value types, and built-in behaviors
│   ├── IRequest.cs                   # Base request interface
│   ├── ICommand.cs                   # Command marker interface
│   ├── IQuery.cs                     # Query interface
│   ├── IRequestHandler.cs            # Handler interface
│   ├── INotification.cs              # Notification marker interface
│   ├── INotificationHandler.cs       # Notification handler interface
│   ├── ICommandDispatcher.cs         # Command dispatcher interface
│   ├── INotificationPublisher.cs     # Notification publisher interface
│   ├── IQueryDispatcherMarker.cs     # Query dispatcher marker interface
│   ├── IMediator.cs                  # Unified mediator interface (Send + Publish)
│   ├── IPipelineBehavior.cs          # Pipeline behavior interface
│   ├── RequestHandlerDelegate.cs     # Delegate used in pipeline behaviors
│   ├── LoggingBehavior.cs            # Built-in: logs Handling/Handled/Error via ILogger
│   ├── PerformanceBehavior.cs        # Built-in: warns when request exceeds configurable threshold
│   ├── CachingBehavior.cs            # Built-in: caches responses via IMemoryCache for ICacheableRequest
│   ├── ICacheableRequest.cs          # Opt-in marker for cacheable requests (CacheKey + CacheDuration)
│   ├── CachingBehaviorOptions.cs     # Default TTL options for CachingBehavior
│   ├── ValidationBehavior.cs         # Built-in: validates requests via IValidator<TRequest> (FluentValidation)
│   ├── BehaviorServiceCollectionExtensions.cs  # AddDirectMediatorLogging() / AddDirectMediatorPerformanceBehavior() / AddDirectMediatorCaching() / AddDirectMediatorValidation()
│   └── Unit.cs                       # Unit value type
│
├── DirectMediator.Generator/           # Roslyn incremental source generator
│   └── MediatorGenerator.cs          # Discovers handlers, validates, and emits code
│
├── DirectMediator/                     # Main NuGet package project
│   └── DirectMediator.csproj
│
├── DirectMediator.Sample/              # Example console application
│   ├── CreateOrderCommand.cs
│   ├── CreateOrderHandler.cs
│   ├── GetOrderQuery.cs
│   ├── GetOrderHandler.cs
│   ├── OrderCreatedNotification.cs
│   ├── OrderCreatedHandler.cs
│   └── Program.cs
│
└── DirectMediator.Tests/               # Unit tests (xUnit)
    ├── CommandDispatcherTests.cs
    ├── QueryDispatcherTests.cs
    ├── NotificationPublisherTests.cs
    ├── PipelineBehaviorTests.cs        # Custom behaviors + built-in LoggingBehavior/PerformanceBehavior
    ├── ValidationBehaviorTests.cs      # ValidationBehavior + AddDirectMediatorValidation()
    └── MediatorTests.cs
```

---

## License

This project is open source. See the repository for license details.
