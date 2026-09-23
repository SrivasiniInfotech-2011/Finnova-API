using FluentValidation;

namespace Finnova.Service.Lookup.Queries.GetLookupValuesPaged;

/// <summary>
/// Validates paging bounds and optional filter lengths (R1.7). Filters are only
/// length-checked when provided; a null filter is left unconstrained.
/// </summary>
public class GetLookupValuesPagedQueryValidator : AbstractValidator<GetLookupValuesPagedQuery>
{
    public GetLookupValuesPagedQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Module).MaximumLength(50).When(x => x.Module is not null);
        RuleFor(x => x.LookupType).MaximumLength(100).When(x => x.LookupType is not null);
    }
}
