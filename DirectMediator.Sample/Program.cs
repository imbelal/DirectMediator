using DirectMediator;
using DirectMediator.Generated;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// ✅ Auto-register all handlers + dispatchers + IMediator
services.AddDirectMediator();

var provider = services.BuildServiceProvider();

// --- Use the unified IMediator interface (recommended) ---
var mediator = provider.GetRequiredService<IMediator>();

await mediator.Send(new CreateOrderCommand("Tile"));
var queryResult = await mediator.Send(new GetOrderQuery(123));
Console.WriteLine($"IMediator query result: {queryResult}");
await mediator.Publish(new OrderCreatedNotification("Tile"));

// --- Or use the individual dispatchers directly (also supported) ---
var commandDispatcher = provider.GetRequiredService<CommandDispatcher>();
var queryDispatcher = provider.GetRequiredService<QueryDispatcher>();
var publisher = provider.GetRequiredService<NotificationPublisher>();

await commandDispatcher.Send(new CreateOrderCommand("Tile"), default);
var result = await queryDispatcher.Query(new GetOrderQuery(123), default);
Console.WriteLine($"Query result: {result}");
await publisher.Publish(new OrderCreatedNotification("Tile"), default);
