namespace Finnova.Models.Domain.Entities;

public sealed class FactoringSubLimit
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid CustomerId { get; private set; }
    public bool FactoringApplicable { get; private set; }
    public string? FactoringTypeCode { get; private set; }
    public decimal? Limit { get; private set; }
    public string? Covenant { get; private set; }
    public string? CovenantClause { get; private set; }
    public DateOnly? CutOffDate { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public void Initialize(
        bool factoringApplicable, string? factoringTypeCode, decimal? limit, string? covenant,
        string? covenantClause, DateOnly? cutOffDate)
    {
        FactoringApplicable = factoringApplicable;
        FactoringTypeCode = factoringTypeCode;
        Limit = limit;
        Covenant = covenant;
        CovenantClause = covenantClause;
        CutOffDate = cutOffDate;
        UpdatedAtUtc = DateTime.UtcNow;
    }
    public DateTime UpdatedAtUtc { get; private set; } = DateTime.UtcNow;
}
