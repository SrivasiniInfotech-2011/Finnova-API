namespace Finnova.Models.Domain.Exceptions;

/// <summary>
/// Thrown for drawee-bank domain validation failures surfaced outside FluentValidation
/// (e.g. an unparseable challan Format Pattern / Validation Expression) (R6.8). Maps to
/// ERR-DRB-400 by type.
/// </summary>
public class DraweeBankValidationException : Exception
{
    public const string ErrorCode = "ERR-DRB-400";

    public DraweeBankValidationException(string message)
        : base(message)
    {
    }
}
