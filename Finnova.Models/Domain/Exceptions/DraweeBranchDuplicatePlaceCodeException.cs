namespace Finnova.Models.Domain.Exceptions;

/// <summary>
/// Thrown when two or more submitted branches share a Place Code (trimmed, case-insensitive)
/// within the same drawee bank (R3.3). Maps to ERR-DRB-409 by type.
/// </summary>
public class DraweeBranchDuplicatePlaceCodeException : Exception
{
    public const string ErrorCode = "ERR-DRB-409";

    public DraweeBranchDuplicatePlaceCodeException()
        : base("Place code must be unique within the bank")
    {
    }
}
