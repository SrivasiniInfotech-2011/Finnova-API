using FluentValidation;

namespace Finnova.Service.Locations.Commands.CreateLocation;

public class CreateLocationCommandValidator : AbstractValidator<CreateLocationCommand>
{
    public CreateLocationCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Code is required.")
            .MaximumLength(20).WithMessage("Code must not exceed 20 characters.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");

        RuleFor(x => x.Level)
            .InclusiveBetween(1, 5).WithMessage("Level must be between 1 (Country) and 5 (Branch).");

        // Root nodes (level 1) must not have a parent; all others must.
        RuleFor(x => x.ParentId)
            .Null().When(x => x.Level == 1)
            .WithMessage("A Country (level 1) cannot have a parent.");

        RuleFor(x => x.ParentId)
            .NotNull().When(x => x.Level > 1)
            .WithMessage("A parent is required for levels below Country.");
    }
}
