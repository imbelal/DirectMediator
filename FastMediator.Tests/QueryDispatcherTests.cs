using FastMediator;
using FastMediator.Generated;
using Microsoft.Extensions.DependencyInjection;

// Handler must be public/top-level so the source generator can reference it in generated code
public record TestQuery(int Id) : IQuery<string>;
public class TestQueryHandler : IRequestHandler<TestQuery, string>
{
    public Task<string> Handle(TestQuery request, CancellationToken cancellationToken)
        => Task.FromResult($"Result {request.Id}");
}

public class QueryDispatcherTests
{
    [Fact]
    public async Task QueryDispatcher_SendsQuery_ReturnsExpectedResult()
    {
        var services = new ServiceCollection();
        // Register the concrete handler type so the generated QueryDispatcher can resolve it
        services.AddTransient<TestQueryHandler>();
        services.AddSingleton<QueryDispatcher>();

        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<QueryDispatcher>();

        // Generated QueryDispatcher exposes a typed Query() method per registered query type
        var result = await dispatcher.Query(new TestQuery(42), default);

        Assert.Equal("Result 42", result);
    }
}
