using FluentValidation;

namespace Finnova.Service.Nationality.Commands.CreateNationality;

public class CreateNationalityCommandValidator : AbstractValidator<CreateNationalityCommand>
{
    public CreateNationalityCommandValidator()
    {
        // R1.3, R2.5 — Code required; R1.4, R2.5 — max 10 characters.
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Nationality Code is required.")
            .MaximumLength(10).WithMessage("Nationality Code must not exceed 10 characters.");

        // R1.3 — Name required; R1.4 — max 100 characters.
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");
    }
}
