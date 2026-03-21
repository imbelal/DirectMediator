using DirectMediator;
using DirectMediator.Generated;
using Microsoft.Extensions.DependencyInjection;

// Handler must be public/top-level so the source generator can reference it in generated code
public record TestCommand(string Name) : ICommand;
public class TestCommandHandler : IRequestHandler<TestCommand, Unit>
{
    private readonly Action _callback;
    public TestCommandHandler(Action callback) => _callback = callback;

    public Task<Unit> Handle(TestCommand request, CancellationToken cancellationToken)
    {
        _callback();
        return Task.FromResult(Unit.Value);
    }
}

public class CommandDispatcherTests
{
    [Fact]
    public async Task CommandDispatcher_SendsCommand_HandlerCalled()
    {
        var handlerCalled = false;

        var services = new ServiceCollection();
        // Register the concrete handler type so the generated CommandDispatcher can resolve it
        services.AddTransient<TestCommandHandler>(sp =>
            new TestCommandHandler(() => handlerCalled = true));
        services.AddSingleton<CommandDispatcher>();

        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<CommandDispatcher>();

        await dispatcher.Send(new TestCommand("Test"), default);

        Assert.True(handlerCalled, "Handler should have been called");
    }
}
