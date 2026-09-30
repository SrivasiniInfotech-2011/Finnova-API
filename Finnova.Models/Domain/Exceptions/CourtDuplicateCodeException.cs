namespace Finnova.Models.Domain.Exceptions;

/// <summary>
/// Thrown when a court create is attempted with a code that already exists. Maps to ERR-CRT-409.
/// </summary>
public class CourtDuplicateCodeException : Exception
{
    public const string ErrorCode = "ERR-CRT-409";

    public CourtDuplicateCodeException()
        : base("Court code must be unique")
    {
    }
}