using FluentValidation;
using Finnova.Service.UserManagement.Internal;

namespace Finnova.Service.UserManagement.Commands.UpdateUserAccount;

public class UpdateUserAccountCommandValidator : AbstractValidator<UpdateUserAccountCommand>
{
    public UpdateUserAccountCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Please enter the User Name")              // R3.2
            .MaximumLength(50).WithMessage("User Name must not exceed 50 characters.");   // R3.3

        RuleFor(x => x.Designation)
            .NotEmpty().WithMessage("Please select the Designation").MaximumLength(40);   // R3.9/3.13

        RuleFor(x => x.Department)
            .NotEmpty().WithMessage("Please select the Department").MaximumLength(40);     // R3.10/3.14

        RuleFor(x => x.UserType).IsInEnum().WithMessage("Please select the User Type");    // R3.11/3.12

        RuleFor(x => x.MobileNumber)
            .Matches("^[0-9]{1,12}$").WithMessage("Special characters are not allowed in this field")
            .When(x => !string.IsNullOrEmpty(x.MobileNumber));                 // R4.1-4.3

        RuleFor(x => x.Email)
            .Must(EmailFormat.IsValid).WithMessage("Please enter a valid Email id")
            .When(x => !string.IsNullOrEmpty(x.Email));                        // R4.4/4.5
    }
}
