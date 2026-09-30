namespace Finnova.Models.Domain.Exceptions;

/// <summary>
/// Thrown when a create/update supplies per-type attribute keys that are not applicable to the
/// entity's Entity Type (R1.6, R4.3). Flows through the validation family -> 400 ERR-ENT-400.
/// </summary>
public class EntityInvalidAttributesException : Exception
{
    public EntityInvalidAttributesException(IEnumerable<string> inapplicableKeys)
        : base($"The following attributes are not applicable to this entity type: {string.Join(", ", inapplicableKeys)}")
    {
    }
}