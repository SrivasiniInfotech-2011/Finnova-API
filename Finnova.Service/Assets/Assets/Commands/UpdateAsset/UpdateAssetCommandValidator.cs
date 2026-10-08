using FluentValidation;
using Finnova.Service.Assets.Assets.Commands.CreateAsset;

namespace Finnova.Service.Assets.Assets.Commands.UpdateAsset;

public class UpdateAssetCommandValidator : AbstractValidator<UpdateAssetCommand>
{
    public UpdateAssetCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ClassCodeId).NotEmpty().WithMessage("Asset Category is required.");
        RuleFor(x => x.TypeCodeId).NotEmpty().WithMessage("Asset Type is required.");
        RuleFor(x => x.Description).NotEmpty().WithMessage("Asset Code Description is required.")
            .MaximumLength(200).WithMessage("Asset Code Description must not exceed 200 characters.");
        RuleFor(x => x.BookDepreciationRate).Must(CreateAssetCommandValidator.BeValidRate)
            .WithMessage("Book Depreciation Rate % must be between 0 and 100 with at most 2 decimal places.");
        RuleFor(x => x.StockDepreciationRate).Must(CreateAssetCommandValidator.BeValidRate)
            .WithMessage("Stock Depreciation Rate % must be between 0 and 100 with at most 2 decimal places.");
        RuleFor(x => x.GuidelineLimit).GreaterThanOrEqualTo(0)
            .WithMessage("Guideline Limit must be greater than or equal to 0.");
    }
}
