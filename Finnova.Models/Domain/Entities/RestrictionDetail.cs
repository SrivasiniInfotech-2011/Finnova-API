namespace Finnova.Models.Domain.Entities;

/// <summary>
/// Per-bank clearing-day / effective-period constraint. Optional: a bank may have none (R4.4).
/// ClearingDays in [0, 365] (R4.2, R4.5). EndDate on or after StartDate (R4.3).
/// </summary>
public class RestrictionDetail
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DraweeBankId { get; set; }                  // owner FK (R4.1)

    public int ClearingDays { get; set; }                   // >= 0 and <= 365 (R4.2, R4.5)
    public DateTime StartDate { get; set; }                 // R4.1
    public DateTime EndDate { get; set; }                   // on/after StartDate (R4.3)
}
