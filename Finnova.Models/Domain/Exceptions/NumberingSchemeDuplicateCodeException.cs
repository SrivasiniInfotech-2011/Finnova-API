namespace Finnova.Models.Domain.Exceptions;

/// <summary>
/// Thrown when a scheme create is attempted with a code that already exists. Maps to ERR-DCN-409.
/// </summary>
public class NumberingSchemeDuplicateCodeException : Exception
{
    public const string ErrorCode = "ERR-DCN-409";

    public NumberingSchemeDuplicateCodeException()
        : base("Scheme code must be unique")
    {
    }
}
