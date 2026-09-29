using Finnova.Service.Nationality.Queries.GetNationalitiesPaged;
using FluentValidation;

namespace Finnova.Service.OrganizationHierarchy.Queries.GetOrganizationNodesPaged;

public class GetOrganizationNodesPagedQueryValidator : AbstractValidator<GetOrganizationNodesPagedQuery>
{
    public GetOrganizationNodesPagedQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
