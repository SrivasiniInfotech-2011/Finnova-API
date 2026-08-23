using FluentValidation;

namespace Finnova.Service.Organizations.Commands.CreateOrganization;

public class CreateOrganizationCommandValidator : AbstractValidator<CreateOrganizationCommand>
{
    public CreateOrganizationCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Company Code is required.")
            .MaximumLength(5).WithMessage("Company Code must not exceed 5 characters.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Company Name is required.")
            .MaximumLength(100).WithMessage("Company Name must not exceed 100 characters.");

        RuleFor(x => x.ConstitutionType)
            .NotEmpty().WithMessage("Constitution Type is required.")
            .Must(value => Enum.TryParse<Finnova.Models.Domain.Enums.ConstitutionType>(value, ignoreCase: true, out _))
            .WithMessage("Constitution Type must be one of: PrivateLtd, PublicLtd, LLP, Partnership.");

        RuleFor(x => x.PanNumber)
            .MaximumLength(20).WithMessage("PAN Number must not exceed 20 characters.")
            .When(x => !string.IsNullOrEmpty(x.PanNumber));

        RuleFor(x => x.GstNumber)
            .MaximumLength(30).WithMessage("GST Number must not exceed 30 characters.")
            .When(x => !string.IsNullOrEmpty(x.GstNumber));

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Invalid email format.")
            .When(x => !string.IsNullOrEmpty(x.Email));

        RuleFor(x => x.AccountingCurrency)
            .NotEmpty().WithMessage("Accounting Currency is required.");
    }
}
