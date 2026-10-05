using FluentValidation;
using Finnova.Service.DraweeBank.Validators;

namespace Finnova.Service.DraweeBank.Commands.UpdateDraweeBank;

public class UpdateDraweeBankCommandValidator : AbstractValidator<UpdateDraweeBankCommand>
{
    public UpdateDraweeBankCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        // R5.2 — BankName required; R5.3 — max 150.
        RuleFor(x => x.BankName)
            .NotEmpty().WithMessage("Bank Name is required.")
            .MaximumLength(150).WithMessage("Bank Name must not exceed 150 characters.");

        RuleForEach(x => x.Branches).SetValidator(new DraweeBranchRequestValidator());
        When(x => x.Restriction != null, () =>
            RuleFor(x => x.Restriction!).SetValidator(new RestrictionDetailRequestValidator()));
    }
}
