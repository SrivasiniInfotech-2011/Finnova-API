using FluentValidation;
using Finnova.Models.Contracts.DraweeBanks;

namespace Finnova.Service.DraweeBank.Validators;

/// <summary>Validates a single submitted branch (R3.2 required, R3.6 six-digit PIN,
/// R3.4 EndDate >= StartDate when present).</summary>
public class DraweeBranchRequestValidator : AbstractValidator<DraweeBranchRequest>
{
    public DraweeBranchRequestValidator()
    {
        RuleFor(x => x.PlaceCode)
            .NotEmpty().WithMessage("Place Code is required.")
            .MaximumLength(20).WithMessage("Place Code must not exceed 20 characters.");

        RuleFor(x => x.PlaceName)
            .NotEmpty().WithMessage("Place Name is required.")
            .MaximumLength(150).WithMessage("Place Name must not exceed 150 characters.");

        RuleFor(x => x.Address)
            .MaximumLength(300).WithMessage("Address must not exceed 300 characters.");

        // Six-digit Indian PIN (R3.6).
        RuleFor(x => x.PostalCode)
            .Matches(@"^\d{6}$").WithMessage("Postal Code must be a six-digit PIN.");

        // EndDate on/after StartDate when present (R3.4). Null EndDate = open-ended (R3.5).
        RuleFor(x => x)
            .Must(b => b.EndDate == null || b.EndDate.Value >= b.StartDate)
            .WithMessage("Branch End Date must be on or after Start Date.");
    }
}
