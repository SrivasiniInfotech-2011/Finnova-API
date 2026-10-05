using FluentValidation;

namespace Finnova.Service.DraweeBank.Commands.CreateChallanRule;

public class CreateChallanRuleCommandValidator : AbstractValidator<CreateChallanRuleCommand>
{
    public CreateChallanRuleCommandValidator()
    {
        RuleFor(x => x.DraweeBankId).NotEmpty();

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
