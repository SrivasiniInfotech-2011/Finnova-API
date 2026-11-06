using FluentValidation;
using Finnova.Models.Domain.Enums;

namespace Finnova.Service.Applications.Validation;

public static class ApplicationValidationRules
{
    public const decimal MaxFinanceAmount = 1000000000m;
    public const int MaxTenure = 600;
    public const int MaxSearchLength = 100;
    public static bool IsSupported(RepaymentFrequency value) => Enum.IsDefined(value);
    public static bool HasPaisaScale(decimal value) => decimal.Round(value, 2) == value;
}

public sealed class CreateApplicationCommandValidator : AbstractValidator<Applications.Commands.CreateApplicationCommand>
{
    public CreateApplicationCommandValidator()
    {
        RuleFor(x => x.LineOfBusinessId).NotEmpty(); RuleFor(x => x.CustomerId).NotEmpty(); RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.ProductCode).NotEmpty().MaximumLength(50); RuleFor(x => x.SchemeCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.FinanceAmount).GreaterThan(0).LessThanOrEqualTo(ApplicationValidationRules.MaxFinanceAmount).Must(ApplicationValidationRules.HasPaisaScale);
        RuleFor(x => x.IrrPercent).InclusiveBetween(0, 100); RuleFor(x => x.Tenure).InclusiveBetween(1, ApplicationValidationRules.MaxTenure);
        RuleFor(x => x.RepaymentFrequency).Must(ApplicationValidationRules.IsSupported); RuleFor(x => x.ProcessingCharges).GreaterThanOrEqualTo(0).Must(x => !x.HasValue || ApplicationValidationRules.HasPaisaScale(x.Value));
    }
}

public sealed class UpdateApplicationCommandValidator : AbstractValidator<Applications.Commands.UpdateApplicationCommand>
{
    public UpdateApplicationCommandValidator()
    {
        RuleFor(x => x.LineOfBusinessId).NotEmpty(); RuleFor(x => x.CustomerId).NotEmpty(); RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.ProductCode).NotEmpty().MaximumLength(50); RuleFor(x => x.SchemeCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.FinanceAmount).GreaterThan(0).LessThanOrEqualTo(ApplicationValidationRules.MaxFinanceAmount).Must(ApplicationValidationRules.HasPaisaScale);
        RuleFor(x => x.IrrPercent).InclusiveBetween(0, 100); RuleFor(x => x.Tenure).InclusiveBetween(1, ApplicationValidationRules.MaxTenure);
        RuleFor(x => x.RepaymentFrequency).Must(ApplicationValidationRules.IsSupported); RuleFor(x => x.ProcessingCharges).GreaterThanOrEqualTo(0).Must(x => !x.HasValue || ApplicationValidationRules.HasPaisaScale(x.Value));
        RuleFor(x => x.Id).NotEmpty();
    }
}

public sealed class GetApplicationsQueryValidator : AbstractValidator<Applications.Queries.GetApplicationsQuery>
{
    public GetApplicationsQueryValidator()
    {
        RuleFor(x => x.Search).MaximumLength(ApplicationValidationRules.MaxSearchLength);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1); RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
