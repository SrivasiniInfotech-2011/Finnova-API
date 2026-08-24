using FluentValidation;

namespace Finnova.Service.Branches.Commands.CreateBranch;

public class CreateBranchCommandValidator : AbstractValidator<CreateBranchCommand>
{
    public CreateBranchCommandValidator()
    {
        RuleFor(x => x.BranchType)
            .NotEmpty().WithMessage("Branch Type is required.")
            .MaximumLength(20).WithMessage("Branch Type must not exceed 20 characters.")
            .Must(value => Enum.TryParse<Finnova.Models.Domain.Enums.BranchType>(value, ignoreCase: true, out _))
            .WithMessage("Branch Type must be one of: HEAD, REGIONAL, BRANCH, SERVICE, ATM, EXTENSION.");

        RuleFor(x => x.CorporateCode)
            .NotEmpty().WithMessage("Corporate Code is required.")
            .MaximumLength(3).WithMessage("Corporate Code must not exceed 3 characters.");

        RuleFor(x => x.StateCode)
            .NotEmpty().WithMessage("State Code is required.")
            .MaximumLength(3).WithMessage("State Code must not exceed 3 characters.");

        RuleFor(x => x.BranchCode)
            .NotEmpty().WithMessage("Branch Code is required.")
            .MaximumLength(3).WithMessage("Branch Code must not exceed 3 characters.");

        RuleFor(x => x.BranchName)
            .NotEmpty().WithMessage("Branch Name is required.")
            .MaximumLength(50).WithMessage("Branch Name must not exceed 50 characters.");

        RuleFor(x => x.Address)
            .NotEmpty().WithMessage("Address is required.")
            .MaximumLength(100).WithMessage("Address must not exceed 100 characters.");

        RuleFor(x => x.Landmark)
            .NotEmpty().WithMessage("Landmark is required.")
            .MaximumLength(100).WithMessage("Landmark must not exceed 100 characters.");

        RuleFor(x => x.State)
            .NotEmpty().WithMessage("State is required.");

        RuleFor(x => x.Country)
            .NotEmpty().WithMessage("Country is required.");
    }
}
