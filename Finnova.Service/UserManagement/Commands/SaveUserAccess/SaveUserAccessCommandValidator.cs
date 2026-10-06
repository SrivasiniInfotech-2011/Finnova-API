using FluentValidation;

namespace Finnova.Service.UserManagement.Commands.SaveUserAccess;

public class SaveUserAccessCommandValidator : AbstractValidator<SaveUserAccessCommand>
{
    public SaveUserAccessCommandValidator()
    {
        RuleFor(x => x.LineOfBusinessId)
            .NotEmpty().WithMessage("Please select at least one Line of Business");   // R7.4

        RuleFor(x => x.Branches)
            .NotEmpty().WithMessage("At least one branch association is required.");   // R9.6

        RuleForEach(x => x.Branches)
            .Must(b => b.IsAll || b.LocationId.HasValue)
            .WithMessage("The selected branch location was not found.");               // R9 (null LocationId on a non-ALL selection)

        RuleForEach(x => x.Rows).ChildRules(row =>
            row.RuleFor(r => r.RoleCenterName)
                .NotEmpty().WithMessage("Please select the Role Center Name"));        // R8.2
    }
}
