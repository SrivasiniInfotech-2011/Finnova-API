namespace Finnova.Models.Domain.Entities;

/// <summary>
/// Minimal read-only F&amp;A bank master row used as the Bank Transfer deposit bank (Assumption A8).
/// Maintenance is out of scope; a future F&amp;A Bank Master feature is expected to own this table.
/// </summary>
public class FaBankAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string BankName { get; set; } = string.Empty;
    public string BankBranch { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string MicrCode { get; set; } = string.Empty;       // nine digits
    public string GlCode { get; set; } = string.Empty;         // nine digits
    public string? SlCode { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}