namespace Finnova.Models.Domain.Exceptions;

/// <summary>
/// Thrown when an asset create/update references a Class/Type code that does not resolve to
/// an existing code-master row (R4.4). Maps to 400 (ERR-AST-400).
/// </summary>
public class AssetValidationException : Exception
{
    public const string ErrorCode = "ERR-AST-400";

    public AssetValidationException(string message) : base(message)
    {
    }
}
