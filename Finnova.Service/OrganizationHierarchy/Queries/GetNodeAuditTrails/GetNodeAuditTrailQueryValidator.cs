using FluentValidation;

namespace Finnova.Service.OrganizationHierarchy.Queries.GetNodeAuditTrails;

public class GetNodeAuditTrailQueryValidator:AbstractValidator<GetNodeAuditTrailQuery>
{
    public GetNodeAuditTrailQueryValidator()
    {
        RuleFor(s => s).Must(s => s.NodeId != Guid.Empty).WithMessage("Node Id should not be Empty");
    }
}
