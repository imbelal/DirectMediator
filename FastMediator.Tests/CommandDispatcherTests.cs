using FastMediator;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class CommandDispatcherTests
{
    private record TestCommand(string Name) : ICommand;

    [Fact]
    public async Task CommandDispatcher_SendsCommand_HandlerCalled()
    {
        var handlerCalled = false;

        var services = new ServiceCollection();
        services.AddTransient<IRequestHandler<TestCommand, Unit>>(sp =>
            new TestHandler(() => handlerCalled = true));
        services.AddSingleton<CommandDispatcher>();

        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<CommandDispatcher>();

        await dispatcher.Send(new TestCommand("Test"), default);

        Assert.True(handlerCalled, "Handler should have been called");
    }

    private class TestHandler : IRequestHandler<TestCommand, Unit>
    {
        private readonly Action _callback;
        public TestHandler(Action callback) => _callback = callback;

        public Task<Unit> Handle(TestCommand request, CancellationToken cancellationToken)
        {
            _callback();
            return Task.FromResult(Unit.Value);
        }
    }
}
