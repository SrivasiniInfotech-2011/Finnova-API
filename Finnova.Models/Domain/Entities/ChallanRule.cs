namespace Finnova.Models.Domain.Entities;

/// <summary>
/// A standalone challan rule associated with a drawee bank by DraweeBankId. NOT part of the
/// DraweeBank aggregate: it is never navigated from, loaded with, or returned on the bank.
/// Rule Code is unique within the owning bank (R6.4). Managed only through its own endpoints.
/// </summary>
public class ChallanRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DraweeBankId { get; set; }                       // owning bank (R6.1, R6.7)

    public string RuleCode { get; set; } = string.Empty;         // max 50, unique within bank (R6.3, R6.4)
    public string FormatPattern { get; set; } = string.Empty;    // max 500, parseable (R6.3, R6.8)
    public string ValidationExpression { get; set; } = string.Empty; // max 500, parseable (R6.3, R6.8)
    public string RoutingTarget { get; set; } = string.Empty;    // max 200 (R6.3)

    public bool IsActive { get; set; } = true;                   // default true (R6.2)

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
