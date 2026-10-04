using FluentValidation;

namespace Finnova.Service.UserManagement.Commands.SaveUserAccess;

public class SaveUserAccessCommandValidator : AbstractValidator<SaveUserAccessCommand>
{
    public SaveUserAccessCommandValidator()
    {
        RuleFor(x => x.LineOfBusiness)
            .NotEmpty().WithMessage("Please select at least one Line of Business");   // R7.4

        RuleFor(x => x.BranchCodes)
            .NotEmpty().WithMessage("At least one branch association is required.");   // R9.6

        RuleForEach(x => x.Rows).ChildRules(row =>
            row.RuleFor(r => r.RoleCenterName)
                .NotEmpty().WithMessage("Please select the Role Center Name"));        // R8.2
    }
}
