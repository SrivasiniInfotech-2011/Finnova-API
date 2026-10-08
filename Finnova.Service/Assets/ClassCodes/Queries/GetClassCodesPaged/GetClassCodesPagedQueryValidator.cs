using FluentValidation;

namespace Finnova.Service.Assets.ClassCodes.Queries.GetClassCodesPaged;

public class GetClassCodesPagedQueryValidator : AbstractValidator<GetClassCodesPagedQuery>
{
    public GetClassCodesPagedQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1)
            .WithMessage("Page must be greater than or equal to 1.");             // R2.10
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100)
            .WithMessage("Page size must be between 1 and 100.");                 // R2.10
    }
}
