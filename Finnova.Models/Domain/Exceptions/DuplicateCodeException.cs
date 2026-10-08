namespace Finnova.Models.Domain.Exceptions;

/// <summary>
/// Thrown when a code-master create/update would duplicate a Code within its own master
/// (case-insensitive, trimmed) (R1.2, R2.3). Carries the user-facing message and lets the
/// middleware map it to 409 (ERR-AST-409) by type.
/// </summary>
public class DuplicateCodeException : Exception
{
    public const string ErrorCode = "ERR-AST-409";

    public DuplicateCodeException(string master)
        : base($"{master} already exists.")
    {
    }
}
