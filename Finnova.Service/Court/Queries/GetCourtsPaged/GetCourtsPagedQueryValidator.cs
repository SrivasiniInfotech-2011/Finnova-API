using FluentValidation;

namespace Finnova.Service.Court.Queries.GetCourtsPaged;

public class GetCourtsPagedQueryValidator : AbstractValidator<GetCourtsPagedQuery>
{
    public GetCourtsPagedQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);           // R3.5
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);      // R3.5
    }
}