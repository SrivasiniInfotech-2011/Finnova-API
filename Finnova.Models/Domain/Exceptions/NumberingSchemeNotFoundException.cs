namespace Finnova.Models.Domain.Exceptions;

/// <summary>
/// Thrown when an operation targets a numbering scheme that does not exist. Maps to ERR-DCN-404.
/// </summary>
public class NumberingSchemeNotFoundException : Exception
{
    public NumberingSchemeNotFoundException(Guid id)
        : base($"Numbering scheme '{id}' was not found.")
    {
    }

    public NumberingSchemeNotFoundException(string documentType)
        : base($"No numbering scheme matches document type '{documentType}' for the requested scope.")
    {
    }
}
