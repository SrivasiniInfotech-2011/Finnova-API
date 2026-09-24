namespace Finnova.Models.Domain.Exceptions;

/// <summary>
/// Thrown when an update targets a nationality that does not exist (R3.4).
/// Maps to error code ERR-NAT-404.
/// </summary>
public class NationalityNotFoundException : Exception
{
    public NationalityNotFoundException(Guid id)
        : base($"Nationality '{id}' was not found.")
    {
    }
}
