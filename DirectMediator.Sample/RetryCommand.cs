using DirectMediator;

namespace DirectMediator.Sample;

/// <summary>
/// A command that simulates a flaky external service call.
/// This command will fail a configurable number of times before succeeding,
/// demonstrating the RetryBehavior pipeline behavior.
/// </summary>
public record RetryCommand(string ProductName, int FailCount = 0) : IRequest<string>;
