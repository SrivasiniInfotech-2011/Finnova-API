using FluentValidation;

namespace Finnova.Service.Assets.Assets.Queries.GetAssetsPaged;

public class GetAssetsPagedQueryValidator : AbstractValidator<GetAssetsPagedQuery>
{
    public GetAssetsPagedQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1)
            .WithMessage("Page must be greater than or equal to 1.");         // R5.5
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100)
            .WithMessage("Page size must be between 1 and 100.");             // R5.5
    }
}
