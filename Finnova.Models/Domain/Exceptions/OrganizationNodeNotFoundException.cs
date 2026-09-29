namespace Finnova.Models.Domain.Exceptions;

/// <summary>
/// Thrown when an update targets a organization node that does not exist (R3.4).
/// Maps to error code ERR-NAT-404.
/// </summary>
public class OrganizationNodeNotFoundException : Exception
{
    public OrganizationNodeNotFoundException(Guid id)
        : base($"Organization node '{id}' was not found.")
    {
    }
}
