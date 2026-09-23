namespace Finnova.Models.Domain.Exceptions;

/// <summary>
/// Thrown when a lookup value is referenced by other records and therefore
/// cannot be deleted (R5.6).
/// </summary>
public class LookupInUseException : Exception
{
    public LookupInUseException()
        : base("Lookup value is in use and cannot be deleted.")
    {
    }
}
