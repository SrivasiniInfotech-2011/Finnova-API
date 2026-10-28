namespace Finnova.Models.Domain.Entities;

public sealed record CustomerCreditDetails
{
    public Guid CustomerId { get; init; }
    public string? LineOfBusiness { get; init; }
    public string? ProductGroup { get; init; }
    public decimal SanctionedAmount { get; init; }
    public decimal ApprovedAmount { get; init; }
    public decimal UtilizedAmount { get; init; }
    public decimal RemainingAmount { get; init; }
    public DateOnly? ValidUpTo { get; init; }
    public string? FacilityType { get; init; }
}
