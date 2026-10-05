using FluentValidation;

namespace Finnova.Service.DraweeBank.Queries.GetDraweeBanksPaged;

/// <summary>Validates paging bounds (R8.7, R8.8). A blank/whitespace search term is left
/// unconstrained and treated as "no filter" by the repository (R8.2).</summary>
public class GetDraweeBanksPagedQueryValidator : AbstractValidator<GetDraweeBanksPagedQuery>
{
    public GetDraweeBanksPagedQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
