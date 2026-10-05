namespace Finnova.Models.Domain.Entities;

/// <summary>
/// A branch/place under a drawee bank. Place Code is unique within the owning bank (R3.3).
/// EndDate null => open-ended effective period (R3.5). PostalCode is a six-digit Indian PIN (R3.6).
/// </summary>
public class DraweeBranch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DraweeBankId { get; set; }                  // owner FK (R3.1)

    public string PlaceCode { get; set; } = string.Empty;   // max 20, unique within bank (R3.2, R3.3)
    public string PlaceName { get; set; } = string.Empty;   // max 150, single English name (R3.2)
    public string Address { get; set; } = string.Empty;     // max 300 (R3.1)
    public string PostalCode { get; set; } = string.Empty;  // six-digit numeric PIN (R3.6)

    public DateTime StartDate { get; set; }                 // R3.1
    public DateTime? EndDate { get; set; }                  // null => open-ended (R3.4, R3.5)
}
