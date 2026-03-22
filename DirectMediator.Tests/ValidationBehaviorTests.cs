using DirectMediator;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

// -------------------------------------------------------------------
// Validators used only by ValidationBehaviorTests.
// -------------------------------------------------------------------
public class ValidatedRequest : IRequest<string>
{
    public string? Name { get; init; }
}

public class ValidatedRequestValidator : AbstractValidator<ValidatedRequest>
{
    public ValidatedRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required.");
    }
}

// -------------------------------------------------------------------
// Tests exercise ValidationBehavior<TRequest,TResponse> in isolation —
// no generated dispatchers needed.
// -------------------------------------------------------------------
public class ValidationBehaviorTests
{
    private static ValidationBehavior<ValidatedRequest, string> BuildBehavior(
        params IValidator<ValidatedRequest>[] validators)
        => new ValidationBehavior<ValidatedRequest, string>(validators);

    [Fact]
    public async Task ValidationBehavior_PassesThrough_WhenNoValidatorsRegistered()
    {
        var behavior = BuildBehavior(); // zero validators
        var handlerCalled = false;
        RequestHandlerDelegate<string> next = () =>
        {
            handlerCalled = true;
            return Task.FromResult("ok");
        };

        var result = await behavior.Handle(new ValidatedRequest { Name = "test" }, default, next);

        Assert.True(handlerCalled);
        Assert.Equal("ok", result);
    }

    [Fact]
    public async Task ValidationBehavior_PassesThrough_WhenRequestIsValid()
    {
        var behavior = BuildBehavior(new ValidatedRequestValidator());
        var handlerCalled = false;
        RequestHandlerDelegate<string> next = () =>
        {
            handlerCalled = true;
            return Task.FromResult("ok");
        };

        var result = await behavior.Handle(new ValidatedRequest { Name = "Alice" }, default, next);

        Assert.True(handlerCalled);
        Assert.Equal("ok", result);
    }

    [Fact]
    public async Task ValidationBehavior_ThrowsValidationException_WhenRequestIsInvalid()
    {
        var behavior = BuildBehavior(new ValidatedRequestValidator());
        RequestHandlerDelegate<string> next = () => Task.FromResult("should not reach");

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => behavior.Handle(new ValidatedRequest { Name = null }, default, next));

        Assert.Contains(ex.Errors, e => e.PropertyName == "Name");
    }

    [Fact]
    public async Task ValidationBehavior_DoesNotCallHandler_WhenValidationFails()
    {
        var behavior = BuildBehavior(new ValidatedRequestValidator());
        var handlerCalled = false;
        RequestHandlerDelegate<string> next = () =>
        {
            handlerCalled = true;
            return Task.FromResult("ok");
        };

        await Assert.ThrowsAsync<ValidationException>(
            () => behavior.Handle(new ValidatedRequest { Name = string.Empty }, default, next));

        Assert.False(handlerCalled);
    }

    [Fact]
    public async Task ValidationBehavior_AggregatesFailures_FromMultipleValidators()
    {
        var validator1 = new ValidatedRequestValidator();

        // A second validator that adds its own distinct failure.
        var validator2 = new InlineValidator<ValidatedRequest>();
        validator2.RuleFor(x => x.Name).Must(_ => false).WithMessage("Second rule failed.");

        var behavior = BuildBehavior(validator1, validator2);
        RequestHandlerDelegate<string> next = () => Task.FromResult("ok");

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => behavior.Handle(new ValidatedRequest { Name = null }, default, next));

        Assert.True(ex.Errors.Count() >= 2);
    }

    [Fact]
    public void AddDirectMediatorValidation_RegistersValidationBehavior()
    {
        var services = new ServiceCollection();
        services.AddDirectMediatorValidation();

        var provider = services.BuildServiceProvider();
        var behaviors = provider.GetServices(typeof(IPipelineBehavior<ValidatedRequest, string>));

        Assert.Contains(behaviors, b => b is ValidationBehavior<ValidatedRequest, string>);
    }
}
