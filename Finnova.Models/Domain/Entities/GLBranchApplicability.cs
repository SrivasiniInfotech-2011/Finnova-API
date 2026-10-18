namespace Finnova.Models.Domain.Entities;

/// <summary>
/// Branch applicability is a set, not a scalar, so it is modeled as a child entity (R6.7).
/// Unique on (GLAccountId, BranchId).
/// </summary>
public class GLBranchApplicability
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GLAccountId { get; set; }
    public Guid BranchId { get; set; }
}
