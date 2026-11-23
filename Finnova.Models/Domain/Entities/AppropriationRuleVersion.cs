using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

public sealed class AppropriationRuleVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RuleId { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Guid? LineOfBusinessId { get; set; }
    public AppropriationDirection Direction { get; set; }
    public bool FullDue { get; set; }
    public bool IsActive { get; set; }
    public int VersionNumber { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string NormalizedRequestHash { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ActivatedAtUtc { get; set; }
    public DateTime? DeactivatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public List<AppropriationRuleDetail> Details { get; set; } = new();
}

public sealed class AppropriationRuleDetail
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RuleVersionId { get; set; }
    public int SequenceNumber { get; set; }
    public AppropriationDueType DueType { get; set; }
    public string CashFlowDueTypeCode { get; set; } = string.Empty;
    public decimal Percentage { get; set; }
    public AppropriationRuleVersion RuleVersion { get; set; } = null!;
}

public sealed class AppropriationRuleAuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RuleId { get; set; }
    public Guid RuleVersionId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? LineOfBusinessId { get; set; }
    public AppropriationRuleOperation Operation { get; set; }
    public string Actor { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string NormalizedRequestHash { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class RuleMutationIdempotency
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string IdempotencyKey { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string NormalizedRequestHash { get; set; } = string.Empty;
    public Guid RuleId { get; set; }
    public Guid RuleVersionId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}