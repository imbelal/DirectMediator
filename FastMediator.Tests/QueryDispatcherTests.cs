using FastMediator;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class QueryDispatcherTests
{
    private record TestQuery(int Id) : IQuery<string>;

    [Fact]
    public async Task QueryDispatcher_SendsQuery_ReturnsExpectedResult()
    {
        var services = new ServiceCollection();
        services.AddTransient<IRequestHandler<TestQuery, string>>(sp =>
            new TestHandler());
        services.AddSingleton<QueryDispatcher>();

        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<QueryDispatcher>();

        var result = await dispatcher.Send<TestQuery, string>(new TestQuery(42), default);

        Assert.Equal("Result 42", result);
    }

    private class TestHandler : IRequestHandler<TestQuery, string>
    {
        public Task<string> Handle(TestQuery request, CancellationToken cancellationToken)
        {
            return Task.FromResult($"Result {request.Id}");
        }
    }
}
