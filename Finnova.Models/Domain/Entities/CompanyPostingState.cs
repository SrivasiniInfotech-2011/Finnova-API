namespace Finnova.Models.Domain.Entities;

/// <summary>Per-company "JV posting enabled" gate (R7.3/R7.4).</summary>
public class CompanyPostingState
{
    public Guid CompanyId { get; set; }            // primary key
    public bool JvPostingEnabled { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
