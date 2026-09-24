using FluentValidation;

namespace Finnova.Service.Nationality.Queries.GetNationalitiesPaged;

/// <summary>
/// Validates paging bounds (R5.7, R5.8). Page must be >= 1 and PageSize must be
/// within 1..100 inclusive. A blank/whitespace search term is left unconstrained
/// and treated as "no filter" by the repository (R5.2).
/// </summary>
public class GetNationalitiesPagedQueryValidator : AbstractValidator<GetNationalitiesPagedQuery>
{
    public GetNationalitiesPagedQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
