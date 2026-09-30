namespace Finnova.Models.Domain.Exceptions;

/// <summary>Thrown when an operation targets a court that does not exist. Maps to ERR-CRT-404.</summary>
public class CourtNotFoundException : Exception
{
    public CourtNotFoundException(Guid id)
        : base($"Court '{id}' was not found.")
    {
    }
}