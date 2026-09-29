namespace Finnova.Models.Domain.Exceptions;

/// <summary>
/// Thrown when a delete targets an organization node that still has children. Leaf-only delete
/// is enforced by the service. Maps to error code ERR-ORG-409.
/// </summary>
public class OrganizationNodeHasChildrenException : Exception
{
    public OrganizationNodeHasChildrenException(Guid id)
        : base($"Cannot delete organization node '{id}' because it has children.")
    {
    }
}
