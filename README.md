# FastMediator

A zero-reflection mediator for .NET powered by **C# source generators**. FastMediator generates all dispatcher and publisher code at compile time, so there is no runtime reflection, no dictionary lookups, and no dynamic dispatch — just direct, strongly-typed method calls.

---

## Features

- ⚡ **Zero reflection** — all routing is generated at compile time
- 🔒 **Compile-time safety** — errors for duplicate or missing handlers are reported as build errors
- 💉 **DI-first** — single `AddFastMediator()` call registers every handler and dispatcher
- 📦 **Lightweight** — no external runtime dependencies beyond `Microsoft.Extensions.DependencyInjection`
- 🔀 **CQRS-ready** — first-class support for Commands, Queries, and Notifications

---

## How It Works

FastMediator uses an [Incremental Source Generator](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview) (`FastMediator.Generator`) to inspect your project at build time. It:

1. Discovers every class that implements `IRequestHandler<TRequest, TResponse>` or `INotificationHandler<TNotification>`.
2. Emits compile-time diagnostics if handlers are duplicated or missing.
3. Generates a `CommandDispatcher`, a `QueryDispatcher`, a `NotificationPublisher`, and an `AddFastMediator()` extension method — all inside the `FastMediator.Generated` namespace.

Because the routing code is generated as plain C# `switch` expressions, the JIT can inline and optimize it just like hand-written code.

---

## Installation

Add the **FastMediator** NuGet package to your project:

```xml
<PackageReference Include="FastMediator" Version="1.0.0" />
```

Or via the .NET CLI:

```bash
dotnet add package FastMediator
```

---

## Quick Start

### 1. Define a Command

A command performs a side-effectful operation and returns no meaningful value (`Unit`).

```csharp
using FastMediator;

public record CreateOrderCommand(string Product) : ICommand;
```

### 2. Implement the Command Handler

```csharp
using FastMediator;

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
using FastMediator;

public record GetOrderQuery(int Id) : IQuery<string>;
```

### 4. Implement the Query Handler

```csharp
using FastMediator;

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
using FastMediator;

public record OrderCreatedNotification(string Product) : INotification;
```

### 6. Implement a Notification Handler

```csharp
using FastMediator;

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

Call `AddFastMediator()` once during startup. The source generator automatically includes every handler it discovers.

```csharp
using FastMediator.Generated;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// Registers all handlers, CommandDispatcher, QueryDispatcher, and NotificationPublisher
services.AddFastMediator();

var provider = services.BuildServiceProvider();

var commandDispatcher = provider.GetRequiredService<CommandDispatcher>();
var queryDispatcher   = provider.GetRequiredService<QueryDispatcher>();
var publisher         = provider.GetRequiredService<NotificationPublisher>();

// Send a command
await commandDispatcher.Send(new CreateOrderCommand("Tile"));

// Execute a query
string result = await queryDispatcher.Query(new GetOrderQuery(123));
Console.WriteLine($"Query result: {result}");

// Publish a notification
await publisher.Publish(new OrderCreatedNotification("Tile"));
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

---

## Dependency Injection

The generated `AddFastMediator()` extension method on `IServiceCollection`:

- Registers every discovered handler as **transient**
- Registers `CommandDispatcher`, `QueryDispatcher`, and `NotificationPublisher` as **singletons**

```csharp
services.AddFastMediator();
```

All three dispatchers are available directly from the DI container:

```csharp
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

At build time, the generator emits a file called `FastMediator.Generated.g.cs` inside the `FastMediator.Generated` namespace. It contains:

- **`CommandDispatcher`** — implements `ICommandDispatcher`, routes each command type to its handler via a `switch` expression.
- **`QueryDispatcher`** — implements `IQueryDispatcherMarker`, exposes a strongly-typed `Query(...)` method per query type.
- **`NotificationPublisher`** — implements `INotificationPublisher`, fans out each notification to all registered handlers via a `switch` statement.
- **`FastMediatorServiceCollectionExtensions`** — provides the `AddFastMediator()` extension method.

Example (abbreviated) for the sample project:

```csharp
// Auto-generated — do not edit
namespace FastMediator.Generated
{
    public sealed class CommandDispatcher : ICommandDispatcher
    {
        private readonly CreateOrderHandler _createOrderHandler;

        public CommandDispatcher(CreateOrderHandler createOrderHandler)
            => _createOrderHandler = createOrderHandler;

        public Task Send<TCommand>(TCommand command, CancellationToken ct = default)
            where TCommand : ICommand
        {
            return command switch
            {
                CreateOrderCommand c => _createOrderHandler.Handle(c, ct),
                _ => throw new InvalidOperationException($"No handler found for command type '{typeof(TCommand).Name}'")
            };
        }
    }

    public sealed class QueryDispatcher : IQueryDispatcherMarker
    {
        private readonly GetOrderHandler _getOrderHandler;

        public QueryDispatcher(GetOrderHandler getOrderHandler)
            => _getOrderHandler = getOrderHandler;

        public Task<string> Query(GetOrderQuery query, CancellationToken ct = default)
            => _getOrderHandler.Handle(query, ct);
    }

    public sealed class NotificationPublisher : INotificationPublisher
    {
        private readonly OrderCreatedHandler _orderCreatedHandler;

        public NotificationPublisher(OrderCreatedHandler orderCreatedHandler)
            => _orderCreatedHandler = orderCreatedHandler;

        public async Task Publish<TNotification>(TNotification notification, CancellationToken ct = default)
            where TNotification : INotification
        {
            switch (notification)
            {
                case OrderCreatedNotification n:
                    await _orderCreatedHandler.Handle(n, ct);
                    break;
                default:
                    throw new InvalidOperationException($"No handlers found for notification type '{typeof(TNotification).Name}'");
            }
        }
    }
}
```

---

## Project Structure

```
FastMediator/
├── FastMediator.Abstractions/        # Public interfaces and value types
│   ├── IRequest.cs                   # Base request interface
│   ├── ICommand.cs                   # Command marker interface
│   ├── IQuery.cs                     # Query interface
│   ├── IRequestHandler.cs            # Handler interface
│   ├── INotification.cs              # Notification marker interface
│   ├── INotificationHandler.cs       # Notification handler interface
│   ├── ICommandDispatcher.cs         # Command dispatcher interface
│   ├── INotificationPublisher.cs     # Notification publisher interface
│   ├── IQueryDispatcherMarker.cs     # Query dispatcher marker interface
│   └── Unit.cs                       # Unit value type
│
├── FastMediator.Generator/           # Roslyn incremental source generator
│   └── MediatorGenerator.cs          # Discovers handlers, validates, and emits code
│
├── FastMediator/                     # Main NuGet package project
│   └── FastMediator.csproj
│
├── FastMediator.Sample/              # Example console application
│   ├── CreateOrderCommand.cs
│   ├── CreateOrderHandler.cs
│   ├── GetOrderQuery.cs
│   ├── GetOrderHandler.cs
│   ├── OrderCreatedNotification.cs
│   ├── OrderCreatedHandler.cs
│   └── Program.cs
│
└── FastMediator.Tests/               # Unit tests (xUnit)
    ├── CommandDispatcherTests.cs
    ├── QueryDispatcherTests.cs
    └── NotificationPublisherTests.cs
```

---

## License

This project is open source. See the repository for license details.
