using FluentValidation;

namespace Finnova.Service.Lookup.Commands.CreateLookupValue;

public class CreateLookupValueCommandValidator : AbstractValidator<CreateLookupValueCommand>
{
    public CreateLookupValueCommandValidator()
    {
        RuleFor(x => x.Module).NotEmpty().MaximumLength(50);
        RuleFor(x => x.LookupType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Lookup Code is required.")
            .MaximumLength(50).WithMessage("Lookup Code must not exceed 50 characters.");
        RuleFor(x => x.Value)
            .NotEmpty().WithMessage("Value is required.")
            .MaximumLength(200).WithMessage("Value must not exceed 200 characters.");
        RuleFor(x => x.DisplayOrder)
            .InclusiveBetween(0, 9999).WithMessage("Display Order must be between 0 and 9999.");
    }
}
