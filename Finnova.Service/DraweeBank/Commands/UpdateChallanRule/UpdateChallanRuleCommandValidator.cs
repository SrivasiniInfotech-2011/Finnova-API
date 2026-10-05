using FluentValidation;

namespace Finnova.Service.DraweeBank.Commands.UpdateChallanRule;

public class UpdateChallanRuleCommandValidator : AbstractValidator<UpdateChallanRuleCommand>
{
    public UpdateChallanRuleCommandValidator()
    {
        RuleFor(x => x.DraweeBankId).NotEmpty();
        RuleFor(x => x.RuleId).NotEmpty();

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
