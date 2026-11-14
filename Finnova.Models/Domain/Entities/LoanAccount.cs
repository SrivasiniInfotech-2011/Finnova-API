using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

/// <summary>
/// Loan Account produced from a Processed Application (application-account-creation).
/// Separate from the generic <see cref="Account"/> entity; mapped to table "loan_accounts".
/// </summary>
public class LoanAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public AccountType Type { get; set; } = AccountType.Loan;
    public AccountStatus Status { get; set; } = AccountStatus.Active;
    public decimal Balance { get; set; }
    public string Currency { get; set; } = "INR";
    public string? BranchCode { get; set; }
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // ---- Origination references (set by the AccountCreation_Worker) ----
    public Guid? ApplicationId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? LineOfBusinessId { get; set; }
    public Guid? BranchId { get; set; }
    public string? ProductCode { get; set; }
    public string? SchemeCode { get; set; }
    public string? CustomerNameSnapshot { get; set; }
    public string? CreationNotes { get; set; }

    // KYC outcome. KycFlagged is true whenever KycStatus is anything other than Passed.
    public KycStatus? KycStatus { get; set; }
    public bool KycFlagged { get; set; }
    public KycSource? KycSource { get; set; }
    public DateTime? KycCheckedAtUtc { get; set; }
    public Guid? KycVerificationSessionId { get; set; }
    public KycCheckResult? KycPanValidation { get; set; }
    public KycCheckResult? KycPanNameMatch { get; set; }
    public KycCheckResult? KycAadhaarValidation { get; set; }
    public KycCheckResult? KycAadhaarAddressMatch { get; set; }
    public string? KycProviderReference { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}