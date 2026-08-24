using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

public class Branch
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Identification
    public BranchType BranchType { get; set; } = BranchType.BRANCH;
    public string CorporateCode { get; set; } = string.Empty;   // max 3 chars
    public string StateCode { get; set; } = string.Empty;       // max 3 chars
    public string BranchCode { get; set; } = string.Empty;      // max 3 chars
    public string BranchName { get; set; } = string.Empty;      // max 50 chars

    // Address
    public string Address { get; set; } = string.Empty;         // max 100 chars
    public string Landmark { get; set; } = string.Empty;        // max 100 chars
    public string State { get; set; } = string.Empty;           // from predefined list
    public string Country { get; set; } = "India";              // default: India
    public string? Pincode { get; set; }

    // Contact
    public string? Telephone { get; set; }
    public string? Mobile { get; set; }

    // Status flags (mandatory)
    public bool IsActive { get; set; } = true;
    public bool IsOperational { get; set; } = true;

    // Audit
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation — link to parent Organization
    public Guid? OrganizationId { get; set; }
    public Organization? Organization { get; set; }
}
