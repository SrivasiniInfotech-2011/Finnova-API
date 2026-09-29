using FluentValidation;

namespace Finnova.Service.DocumentNumberControl.Queries.GetNumberingSchemesPaged;

public class GetNumberingSchemesPagedQueryValidator : AbstractValidator<GetNumberingSchemesPagedQuery>
{
    public GetNumberingSchemesPagedQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);           // R4.5
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);      // R4.5
    }
}
