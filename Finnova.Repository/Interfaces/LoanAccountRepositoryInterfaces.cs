using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;

namespace Finnova.Repository.Interfaces;

public sealed record LoanAccountFilters(
    string? Search, KycStatus? KycStatus, bool? KycFlagged, AccountStatus? AccountStatus,
    Guid? LineOfBusinessId, Guid? BranchId, string? ProductCode, string? SchemeCode,
    bool? HasPendingKycResolution, int Page, int PageSize);

public sealed record LoanAccountRow(LoanAccount Account, string ApplicationNumber, string CustomerCode, bool HasPendingResolution);
public sealed record CustomerContact(string? Mobile, string? City);
public sealed record ApplicationFinanceSummary(decimal FinanceAmount, decimal IrrPercent, int Tenure, string RepaymentFrequency, DateOnly DisbursementDate);
public sealed record CustomerViewData(string CustomerCode, string Name, string? Mobile, string? City);

public sealed record LoanAccountViewData(
    LoanAccount Account, string ApplicationNumber, ApplicationFinanceSummary Finance, CustomerViewData Customer,
    string? Pan, string? Aadhaar,
    IReadOnlyList<AccountKycHistoryEntry> History, IReadOnlyList<AccountKycResolutionRequest> Resolutions);

public interface ILoanAccountRepository
{
    Task<(List<LoanAccountRow> Rows, int Total)> SearchAsync(LoanAccountFilters filters, CancellationToken ct);
    Task<Dictionary<Guid, CustomerContact>> GetContactsAsync(IReadOnlyCollection<Guid> customerIds, CancellationToken ct);
    /// <summary>Read-only view data. PAN and Aadhaar are returned raw so the service layer can mask them; they are never logged.</summary>
    Task<LoanAccountViewData?> GetViewDataAsync(Guid id, string panDocumentCode, string aadhaarDocumentCode, CancellationToken ct);
    Task<LoanAccount?> GetTrackedAsync(Guid id, CancellationToken ct);
}

/// <summary>Add methods do NOT save; callers save through <see cref="IApplicationUnitOfWork"/> so everything commits atomically.</summary>
public interface IAccountCreationRequestRepository
{
    Task AddAsync(AccountCreationRequest request, CancellationToken ct);
    Task<AccountCreationRequest?> GetTrackedAsync(Guid id, CancellationToken ct);
    Task<Dictionary<AccountRequestStatus, int>> CountByStatusAsync(CancellationToken ct);
    Task<DateTime?> OldestNonCompletedAsync(CancellationToken ct);
    Task<(List<AccountCreationRequest> Items, int Total)> GetDeadLettersAsync(int page, int pageSize, CancellationToken ct);
}

public interface IAccountKycJobRepository
{
    Task<AccountKycJob?> GetTrackedAsync(Guid accountId, CancellationToken ct);
    Task AddAsync(AccountKycJob job, CancellationToken ct);
}

public interface IAccountKycHistoryRepository
{
    Task AddAsync(AccountKycHistoryEntry entry, CancellationToken ct);
}

public interface IAccountKycResolutionRepository
{
    Task AddAsync(AccountKycResolutionRequest request, CancellationToken ct);
    Task<AccountKycResolutionRequest?> GetTrackedAsync(Guid id, CancellationToken ct);
    Task<AccountKycResolutionRequest?> GetPendingAsync(Guid accountId, CancellationToken ct);
}

/// <summary>Append-only: there is intentionally no update or delete method (R11.8).</summary>
public interface IAccountAuditRepository
{
    Task AddAsync(AccountAuditEntry entry, CancellationToken ct);
}

public interface IAppRoleRepository
{
    Task<IReadOnlyCollection<string>> GetRoleNamesForUserAsync(Guid userAccountId, CancellationToken ct);
}

public interface INumberingReferenceRepository
{
    /// <summary>The {LOB} token value for an active LOB, or null when it does not exist.</summary>
    Task<string?> GetLobTokenAsync(Guid lineOfBusinessId, CancellationToken ct);
    /// <summary>The {BRANCH} token value (locations.Code, Level 5) for an active branch, or null.</summary>
    Task<string?> GetBranchCodeAsync(Guid branchId, CancellationToken ct);
}