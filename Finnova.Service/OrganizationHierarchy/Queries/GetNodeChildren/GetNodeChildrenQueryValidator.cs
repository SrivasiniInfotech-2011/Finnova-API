using FluentValidation;

namespace Finnova.Service.OrganizationHierarchy.Queries.GetNodeChildren;

public class GetNodeChildrenQueryValidator : AbstractValidator<GetNodeChildrenQuery>
{
    public GetNodeChildrenQueryValidator()
    {
        RuleFor(s => s).Must(s => s.NodeId != Guid.Empty).WithMessage("Node Id should not be Empty");
    }
}
