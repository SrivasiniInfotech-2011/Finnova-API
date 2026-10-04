using FluentValidation;

namespace Finnova.Service.UserManagement.Commands.CreateUserGroup;

public class CreateUserGroupCommandValidator : AbstractValidator<CreateUserGroupCommand>
{
    public CreateUserGroupCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Please enter the User Group Name")        // R5.2
            .MaximumLength(30).WithMessage("User Group Name must not exceed 30 characters.");   // R5.3

        RuleFor(x => x.MemberUserCodes)
            .NotEmpty().WithMessage("Please enter the User Code & Name");       // R5.6
    }
}
