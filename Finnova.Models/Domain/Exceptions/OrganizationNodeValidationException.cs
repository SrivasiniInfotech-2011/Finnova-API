namespace Finnova.Models.Domain.Exceptions;

/// <summary>
/// Thrown for structural-validity failures when creating or re-parenting an organization node:
/// self-parenting, cycles, exceeding the maximum depth, or referencing a non-existent parent.
/// Maps to error code ERR-ORG-400 in the SystemAdmin exception middleware.
/// </summary>
public class OrganizationNodeValidationException : Exception
{
    public OrganizationNodeValidationException(string message) : base(message)
    {
    }
}
