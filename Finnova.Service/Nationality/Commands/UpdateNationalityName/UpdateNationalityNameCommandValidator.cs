using FluentValidation;

namespace Finnova.Service.Nationality.Commands.UpdateNationalityName;

public class UpdateNationalityNameCommandValidator : AbstractValidator<UpdateNationalityNameCommand>
{
    public UpdateNationalityNameCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id is required.");                  // R3 (target identity)

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")                 // R3.2
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters."); // R3.3
    }
}
