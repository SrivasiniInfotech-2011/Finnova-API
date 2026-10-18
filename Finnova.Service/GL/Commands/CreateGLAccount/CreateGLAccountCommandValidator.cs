using FluentValidation;
using Finnova.Models.Domain.Enums;
using Finnova.Service.GL.Internal;

namespace Finnova.Service.GL.Commands.CreateGLAccount;

public class CreateGLAccountCommandValidator : AbstractValidator<CreateGLAccountCommand>
{
    public CreateGLAccountCommandValidator()
    {
        RuleFor(x => x.Request.CompanyId).NotEmpty().WithMessage("Company is required.");

        RuleFor(x => x.Request.GLCode)
            .NotEmpty().WithMessage("GL code is required.")
            .Must(GLCodeRules.IsNineNumericDigits)
            .WithMessage("GL code must be exactly nine numeric digits.");   // R1.5

        RuleFor(x => x.Request.Name)
            .NotEmpty().WithMessage("GL name is required.")
            .Must(n => n == null || n.Trim().Length <= 150)
            .WithMessage("GL name must not exceed 150 characters.");        // R1.14

        RuleFor(x => x.Request.GLType).IsInEnum().WithMessage("GL type is invalid.");

        // Conditional-mandatory sub-ledger / tax (R1.11)
        RuleFor(x => x.Request.SubLedgerType)
            .NotEmpty().When(x => x.Request.GLType == GLType.Control)
            .WithMessage("Sub-ledger type is required for Control accounts.");
        RuleFor(x => x.Request.TaxType)
            .NotEmpty().When(x => x.Request.GLType == GLType.Tax)
            .WithMessage("Tax type is required for Tax accounts.");

        RuleFor(x => x.Request.CurrencyId).NotEmpty().WithMessage("Currency is required.");
        RuleFor(x => x.Request.EffectiveFrom).NotEmpty().WithMessage("Effective From is required."); // R1.12

        // R1.13 — Effective To >= Effective From (assumption)
        RuleFor(x => x.Request)
            .Must(r => r.EffectiveTo is null || r.EffectiveTo >= r.EffectiveFrom)
            .WithMessage("Effective To must be on or after Effective From.");
    }
}
