using DirectMediator;
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