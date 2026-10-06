using FluentValidation;

namespace Finnova.Service.UserManagement.Commands.UpdateUserGroup;

public class UpdateUserGroupCommandValidator : AbstractValidator<UpdateUserGroupCommand>
{
    public UpdateUserGroupCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Please enter the User Group Name").MaximumLength(30);   // R5.2/5.3
        RuleFor(x => x.MemberUserCodes)
            .NotEmpty().WithMessage("Please enter the User Code & Name");                     // R5.6
    }
}
