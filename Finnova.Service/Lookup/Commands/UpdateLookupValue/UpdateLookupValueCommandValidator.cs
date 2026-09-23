using FluentValidation;

namespace Finnova.Service.Lookup.Commands.UpdateLookupValue;

public class UpdateLookupValueCommandValidator : AbstractValidator<UpdateLookupValueCommand>
{
    public UpdateLookupValueCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id is required.");

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
