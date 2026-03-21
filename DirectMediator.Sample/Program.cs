using DirectMediator.Generated;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// ✅ Auto-register all handlers + dispatchers
services.AddDirectMediator();

var provider = services.BuildServiceProvider();

var commandDispatcher = provider.GetRequiredService<CommandDispatcher>();
var queryDispatcher = provider.GetRequiredService<QueryDispatcher>();
var publisher = provider.GetRequiredService<NotificationPublisher>();

// Use them
await commandDispatcher.Send(new CreateOrderCommand("Tile"), default);
var result = await queryDispatcher.Query(new GetOrderQuery(123), default);
Console.WriteLine($"Query result: {result}");
await publisher.Publish(new OrderCreatedNotification("Tile"), default);
