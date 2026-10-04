using FluentValidation;

namespace Finnova.Service.UserManagement.Commands.CreateFunctionalGroup;

public class CreateFunctionalGroupCommandValidator : AbstractValidator<CreateFunctionalGroupCommand>
{
    public CreateFunctionalGroupCommandValidator()
    {
        RuleFor(x => x.RoleCenterName)
            .NotEmpty().WithMessage("Please select the Role Center Name")      // R6.7
            .MaximumLength(100);
    }
}
