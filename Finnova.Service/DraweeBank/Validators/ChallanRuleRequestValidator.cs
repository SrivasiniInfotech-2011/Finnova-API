using FluentValidation;
using Finnova.Models.Contracts.DraweeBanks;

namespace Finnova.Service.DraweeBank.Validators;

/// <summary>Validates an inline challan rule submitted on create (R6.3 required/lengths,
/// R6.8 parseable pattern/expression).</summary>
public class ChallanRuleRequestValidator : AbstractValidator<ChallanRuleRequest>
{
    public ChallanRuleRequestValidator()
    {
        RuleFor(x => x.RuleCode)
            .NotEmpty().WithMessage("Rule Code is required.")
            .MaximumLength(50).WithMessage("Rule Code must not exceed 50 characters.");

        RuleFor(x => x.FormatPattern)
            .NotEmpty().WithMessage("Format Pattern is required.")
            .MaximumLength(500).WithMessage("Format Pattern must not exceed 500 characters.")
            .Must(ChallanExpression.IsParseable).WithMessage("Format Pattern is not a valid expression.");

        RuleFor(x => x.ValidationExpression)
            .NotEmpty().WithMessage("Validation Expression is required.")
            .MaximumLength(500).WithMessage("Validation Expression must not exceed 500 characters.")
            .Must(ChallanExpression.IsParseable).WithMessage("Validation Expression is not a valid expression.");

        RuleFor(x => x.RoutingTarget)
            .NotEmpty().WithMessage("Routing Target is required.")
            .MaximumLength(200).WithMessage("Routing Target must not exceed 200 characters.");
    }
}
