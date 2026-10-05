using FluentValidation;
using Finnova.Service.DraweeBank.Validators;

namespace Finnova.Service.DraweeBank.Commands.CreateDraweeBank;

public class CreateDraweeBankCommandValidator : AbstractValidator<CreateDraweeBankCommand>
{
    public CreateDraweeBankCommandValidator()
    {
        // R1.3, R2.5 — BankCode required; R1.4, R2.5 — max 20.
        RuleFor(x => x.BankCode)
            .NotEmpty().WithMessage("Bank Code is required.")
            .MaximumLength(20).WithMessage("Bank Code must not exceed 20 characters.");

        // R1.3 — BankName required; R1.4 — max 150.
        RuleFor(x => x.BankName)
            .NotEmpty().WithMessage("Bank Name is required.")
            .MaximumLength(150).WithMessage("Bank Name must not exceed 150 characters.");

        // Each branch / restriction validated per its own rules (R3, R4). No challan rules are
        // accepted inline — they are managed through their own route.
        RuleForEach(x => x.Branches).SetValidator(new DraweeBranchRequestValidator());
        When(x => x.Restriction != null, () =>
            RuleFor(x => x.Restriction!).SetValidator(new RestrictionDetailRequestValidator()));
    }
}
