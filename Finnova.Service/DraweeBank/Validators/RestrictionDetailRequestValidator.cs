using FluentValidation;
using Finnova.Models.Contracts.DraweeBanks;

namespace Finnova.Service.DraweeBank.Validators;

/// <summary>Validates a submitted restriction (R4.2 ClearingDays >= 0, R4.5 <= 365,
/// R4.3 EndDate >= StartDate).</summary>
public class RestrictionDetailRequestValidator : AbstractValidator<RestrictionDetailRequest>
{
    public RestrictionDetailRequestValidator()
    {
        RuleFor(x => x.ClearingDays)
            .InclusiveBetween(0, 365).WithMessage("Clearing Days must be between 0 and 365.");

        RuleFor(x => x)
            .Must(r => r.EndDate >= r.StartDate)
            .WithMessage("Restriction End Date must be on or after Start Date.");
    }
}
