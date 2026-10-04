using FluentValidation;
using Finnova.Service.UserManagement.Internal;

namespace Finnova.Service.UserManagement.Commands.CreateUserAccount;

public class CreateUserAccountCommandValidator : AbstractValidator<CreateUserAccountCommand>
{
    public CreateUserAccountCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Please enter the User Name")              // R3.2
            .MaximumLength(50).WithMessage("User Name must not exceed 50 characters.");   // R3.3

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Please enter the User Password");         // R3.4

        RuleFor(x => x.Designation)
            .NotEmpty().WithMessage("Please select the Designation")           // R3.9
            .MaximumLength(40).WithMessage("Designation must not exceed 40 characters.");   // R3.13

        RuleFor(x => x.Department)
            .NotEmpty().WithMessage("Please select the Department")            // R3.10
            .MaximumLength(40).WithMessage("Department must not exceed 40 characters.");    // R3.14

        RuleFor(x => x.UserType)
            .IsInEnum().WithMessage("Please select the User Type");            // R3.11/3.12

        RuleFor(x => x.MobileNumber)
            .Matches("^[0-9]{1,12}$").WithMessage("Special characters are not allowed in this field")
            .When(x => !string.IsNullOrEmpty(x.MobileNumber));                 // R4.1-4.3

        RuleFor(x => x.Email)
            .Must(EmailFormat.IsValid).WithMessage("Please enter a valid Email id")
            .When(x => !string.IsNullOrEmpty(x.Email));                        // R4.4/4.5
    }
}
