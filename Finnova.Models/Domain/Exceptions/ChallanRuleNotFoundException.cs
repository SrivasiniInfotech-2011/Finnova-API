namespace Finnova.Models.Domain.Exceptions;

/// <summary>
/// Thrown when a challan-rule update targets a rule id that does not exist (R6.6).
/// Maps to ERR-DRB-404.
/// </summary>
public class ChallanRuleNotFoundException : Exception
{
    public const string ErrorCode = "ERR-DRB-404";

    public ChallanRuleNotFoundException(Guid id)
        : base($"Challan rule '{id}' was not found.")
    {
    }
}
