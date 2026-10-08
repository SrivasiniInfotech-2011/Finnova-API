using FluentValidation;

namespace Finnova.Service.Assets.Assets.Commands.CreateAsset;

public class CreateAssetCommandValidator : AbstractValidator<CreateAssetCommand>
{
    public CreateAssetCommandValidator()
    {
        RuleFor(x => x.ClassCodeId).NotEmpty().WithMessage("Asset Category is required.");   // R4.3
        RuleFor(x => x.TypeCodeId).NotEmpty().WithMessage("Asset Type is required.");        // R4.3
        RuleFor(x => x.Description).NotEmpty().WithMessage("Asset Code Description is required.")
            .MaximumLength(200).WithMessage("Asset Code Description must not exceed 200 characters."); // R4.3, R4.7

        RuleFor(x => x.BookDepreciationRate).Must(BeValidRate)
            .WithMessage("Book Depreciation Rate % must be between 0 and 100 with at most 2 decimal places."); // R4.5
        RuleFor(x => x.StockDepreciationRate).Must(BeValidRate)
            .WithMessage("Stock Depreciation Rate % must be between 0 and 100 with at most 2 decimal places."); // R4.5
        RuleFor(x => x.GuidelineLimit).GreaterThanOrEqualTo(0)
            .WithMessage("Guideline Limit must be greater than or equal to 0.");             // R4.6
    }

    // Pure, directly unit/property-testable (R4.5).
    public static bool BeValidRate(decimal r)
        => r >= 0m && r <= 100m && decimal.Round(r, 2) == r;
}
