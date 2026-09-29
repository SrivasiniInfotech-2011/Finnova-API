namespace Finnova.Models.Domain.Exceptions;

/// <summary>
/// Thrown when an organization node create is attempted with a code that already exists.
/// Maps to error code ERR-ORG-409 in the SystemAdmin exception middleware.
/// </summary>
public class OrganizationNodeDuplicateCodeException : Exception
{
    /// <summary>Stable error code returned to clients.</summary>
    public const string ErrorCode = "ERR-ORG-409";

    public OrganizationNodeDuplicateCodeException()
        : base("Node code must be unique")
    {
    }
}
