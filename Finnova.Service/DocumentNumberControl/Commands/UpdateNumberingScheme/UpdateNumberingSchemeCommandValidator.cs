using FluentValidation;

namespace Finnova.Service.DocumentNumberControl.Commands.UpdateNumberingScheme;

/// <summary>Field-level validation for updating a scheme's editable fields (R3.4).</summary>
public class UpdateNumberingSchemeCommandValidator : AbstractValidator<UpdateNumberingSchemeCommand>
{
    public UpdateNumberingSchemeCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Id is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");

        RuleFor(x => x.FormatTemplate)
            .NotEmpty().WithMessage("Format template is required.")
            .MaximumLength(100).WithMessage("Format template must not exceed 100 characters.");

        RuleFor(x => x.Prefix).MaximumLength(20).When(x => x.Prefix is not null);
        RuleFor(x => x.Suffix).MaximumLength(20).When(x => x.Suffix is not null);

        RuleFor(x => x.SeqIncrement)
            .GreaterThanOrEqualTo(1).WithMessage("Sequence increment must be at least 1.");

        RuleFor(x => x.SeqPadding)
            .InclusiveBetween(1, 18).WithMessage("Sequence padding must be between 1 and 18.");

        RuleFor(x => x.ResetRule).IsInEnum().WithMessage("Reset rule is invalid.");
    }
}
