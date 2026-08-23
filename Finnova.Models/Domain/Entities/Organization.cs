using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

public class Organization
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Basic Info
    public string Code { get; set; } = string.Empty;          // max 5 chars
    public string Name { get; set; } = string.Empty;          // max 100 chars
    public ConstitutionType ConstitutionType { get; set; } = ConstitutionType.PrivateLtd;
    public string? Description { get; set; }

    // Registration Info
    public string? CeoName { get; set; }
    public DateTime? RegistrationDate { get; set; }
    public string? RegistrationNumber { get; set; }           // License/Registration Number
    public string? PanNumber { get; set; }                    // max 20 chars
    public string? GstNumber { get; set; }                    // max 30 chars

    // Corporate Address
    public string? CorporateAddress { get; set; }
    public string? CorporateCity { get; set; }
    public string? CorporateState { get; set; }
    public string? CorporateCountry { get; set; }
    public string? CorporatePincode { get; set; }

    // Communication Address
    public string? CommunicationAddress { get; set; }
    public string? CommunicationCity { get; set; }
    public string? CommunicationState { get; set; }
    public string? CommunicationCountry { get; set; }
    public string? CommunicationPincode { get; set; }

    // Contact Details
    public string? Telephone { get; set; }
    public string? Mobile { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }

    // Accounting
    public string AccountingCurrency { get; set; } = "INR";   // Default: Indian Rupee

    // Status & Audit
    public OrganizationStatus Status { get; set; } = OrganizationStatus.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<User> Users { get; set; } = new List<User>();
}
