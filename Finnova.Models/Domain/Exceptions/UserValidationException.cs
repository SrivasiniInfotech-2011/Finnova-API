namespace Finnova.Models.Domain.Exceptions;

/// <summary>
/// Domain validation that is not expressible in a static FluentValidation rule — e.g. missing
/// prerequisite reference (R1.6), LOB not linked to Role Codes (R7.5), missing branch (R9.6),
/// missing Role Center (R8.2). Maps to ERR-USR-400.
/// </summary>
public class UserValidationException : Exception
{
    public const string ErrorCode = "ERR-USR-400";
    public UserValidationException(string message) : base(message) { }
}
