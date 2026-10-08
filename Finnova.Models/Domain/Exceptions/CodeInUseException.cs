namespace Finnova.Models.Domain.Exceptions;

/// <summary>
/// Thrown when a hard-delete of a code-master record is attempted while one or more assets
/// reference it (R6.4). Maps to 409 (ERR-AST-409).
/// </summary>
public class CodeInUseException : Exception
{
    public const string ErrorCode = "ERR-AST-409";

    public CodeInUseException(string master)
        : base($"{master} is in use by one or more assets and cannot be deleted.")
    {
    }
}
