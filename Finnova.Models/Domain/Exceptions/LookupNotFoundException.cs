namespace Finnova.Models.Domain.Exceptions;

/// <summary>
/// Thrown when an update or delete targets a lookup value that does not exist (R5.4).
/// </summary>
public class LookupNotFoundException : Exception
{
    public LookupNotFoundException(Guid id)
        : base($"Lookup value '{id}' was not found.")
    {
    }
}
