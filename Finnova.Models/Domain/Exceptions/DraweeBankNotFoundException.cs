namespace Finnova.Models.Domain.Exceptions;

/// <summary>
/// Thrown when an update, a challan-rule create, or a challan-rule update targets a drawee bank
/// that does not exist (R5.4, R6.7). Maps to ERR-DRB-404.
/// </summary>
public class DraweeBankNotFoundException : Exception
{
    public const string ErrorCode = "ERR-DRB-404";

    public DraweeBankNotFoundException(Guid id)
        : base($"Drawee bank '{id}' was not found.")
    {
    }
}
