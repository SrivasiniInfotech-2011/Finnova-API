using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

/// <summary>
/// Aggregate root of the GL Master (FINNOVA-31..38), scoped to an operating company. Four-level
/// hierarchy (L1..L4); only L4 leaves are postable. India-only platform: one English Name.
/// </summary>
public class GLAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompanyId { get; set; }                      // scopes all uniqueness checks

    public string GLCode { get; set; } = string.Empty;       // exactly 9 numeric digits; unique per company; non-reusable
    public string Name { get; set; } = string.Empty;         // English; max 150; unique per company (case-insensitive)

    public GLLevel Level { get; set; }                       // L1..L4
    public Guid? ParentGLId { get; set; }                    // null only for L1

    public AccountNature AccountNature { get; set; }         // derived from parent (R1.9)
    public NormalBalance NormalBalance { get; set; }         // derived from nature (R1.10)

    public GLType GLType { get; set; }                       // Normal/Control/Tax
    public string? SubLedgerType { get; set; }               // mandatory when GLType = Control (R1.11)
    public string? TaxType { get; set; }                     // mandatory when GLType = Tax (R1.11)

    public bool IsPostable { get; set; }                     // true only for L4 (R1.2)
    public Guid CurrencyId { get; set; }                     // GL master currency

    public bool AllowManualJV { get; set; }                  // non-structural
    public bool CostCentreMandatory { get; set; }            // non-structural
    public bool ProfitCentreMandatory { get; set; }          // non-structural
    public string? ReportingLine { get; set; }               // non-structural

    public DateTime EffectiveFrom { get; set; }              // required; not in closed period (R1.12)
    public DateTime? EffectiveTo { get; set; }               // >= EffectiveFrom (R1.13)

    public GLStatus Status { get; set; } = GLStatus.Draft;   // R1.1
    public string? MakerId { get; set; }                     // last maker
    public string? CheckerId { get; set; }                   // last checker
    public int AuthorizedVersionNumber { get; set; }         // starts 0; +1 per authorized modification (R4.5)
    public string? RejectionReason { get; set; }             // last rejection reason (<=500)

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<GLBranchApplicability> BranchApplicabilities { get; set; } = new();
}
