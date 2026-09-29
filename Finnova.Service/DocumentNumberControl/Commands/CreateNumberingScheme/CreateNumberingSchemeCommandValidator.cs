using Finnova.Models.Domain.Enums;
using FluentValidation;

namespace Finnova.Service.DocumentNumberControl.Commands.CreateNumberingScheme;

/// <summary>
/// Field-level validation for creating a scheme (R1.5-R1.9). Template-token validity (R1.12/1.13)
/// and scope/scope-id consistency (R1.10/1.11) are enforced in the handler as typed structural
/// failures so they map to ERR-DCN-400 by type.
/// </summary>
public class CreateNumberingSchemeCommandValidator : AbstractValidator<CreateNumberingSchemeCommand>
{
    public CreateNumberingSchemeCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Scheme code is required.")
            .MaximumLength(30).WithMessage("Scheme code must not exceed 30 characters.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");

        RuleFor(x => x.DocumentType)
            .NotEmpty().WithMessage("Document type is required.")
            .MaximumLength(50).WithMessage("Document type must not exceed 50 characters.");

        RuleFor(x => x.FormatTemplate)
            .NotEmpty().WithMessage("Format template is required.")
            .MaximumLength(100).WithMessage("Format template must not exceed 100 characters.");

        RuleFor(x => x.Prefix).MaximumLength(20).When(x => x.Prefix is not null);
        RuleFor(x => x.Suffix).MaximumLength(20).When(x => x.Suffix is not null);

        RuleFor(x => x.SeqStart)
            .GreaterThanOrEqualTo(0).When(x => x.SeqStart.HasValue)
            .WithMessage("Sequence start must be zero or greater.");

        RuleFor(x => x.SeqIncrement)
            .GreaterThanOrEqualTo(1).When(x => x.SeqIncrement.HasValue)
            .WithMessage("Sequence increment must be at least 1.");

        RuleFor(x => x.SeqPadding)
            .InclusiveBetween(1, 18).When(x => x.SeqPadding.HasValue)
            .WithMessage("Sequence padding must be between 1 and 18.");

        RuleFor(x => x.ResetRule)
            .IsInEnum().WithMessage("Reset rule is invalid.");

        RuleFor(x => x.Scope)
            .Must(s => !s.HasValue || Enum.IsDefined(typeof(NumberScope), s.Value))
            .WithMessage("Scope is invalid.");
    }
}
