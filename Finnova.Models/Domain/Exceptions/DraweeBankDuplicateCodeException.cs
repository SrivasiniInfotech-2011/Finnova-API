namespace Finnova.Models.Domain.Exceptions;

/// <summary>
/// Thrown when a drawee bank is created (or a different bank is updated) with a Bank Code that
/// already exists (case-insensitive, trimmed) (R2.2, R2.3). Maps to ERR-DRB-409 by type.
/// </summary>
public class DraweeBankDuplicateCodeException : Exception
{
    public const string ErrorCode = "ERR-DRB-409";

    public DraweeBankDuplicateCodeException()
        : base("Bank code must be unique")
    {
    }
}
