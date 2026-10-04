using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

/// <summary>
/// A provisioned individual user in the User Management master (FS §9). Named UserAccount (not
/// User) to avoid clashing with the existing authentication User entity. India-only: a single
/// English Name. Maps to table "user_accounts".
/// </summary>
public class UserAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserCode { get; set; } = string.Empty;     // generated, 4-6, uppercase, unique (R2)
    public string Name { get; set; } = string.Empty;         // max 50, English, mandatory (R3.2/3.3)
    public string PasswordHash { get; set; } = string.Empty; // PBKDF2 "salt.hash" (never plaintext) (R3.4/3.5)

    public DateTime DateOfJoining { get; set; }              // defaults to system date (R3.7)
    public string Designation { get; set; } = string.Empty;  // max 40, Lookup-backed (R3.9/3.13)
    public string Department { get; set; } = string.Empty;   // max 40, Lookup-backed (R3.10/3.14)
    public string? MobileNumber { get; set; }                // numeric, <=12, optional (R4.1-4.3)
    public string? Email { get; set; }                       // <=60, format rules, optional (R4.4-4.6)
    public UserType UserType { get; set; }                   // Corporate | Branch (R3.11/3.12)
    public bool IsActive { get; set; } = true;               // default active (R3.15)

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Access assignments + branch associations hang off the user (and off groups) via FKs below.
    public ICollection<UserAccessAssignment> AccessAssignments { get; set; } = new List<UserAccessAssignment>();
    public ICollection<UserBranchAssociation> BranchAssociations { get; set; } = new List<UserBranchAssociation>();
}
