using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Contracts.LoanAccounts;

public sealed record LoanAccountSearchRequest(
    string? Search, KycStatus? KycStatus, bool? KycFlagged, AccountStatus? AccountStatus,
    Guid? LineOfBusinessId, Guid? BranchId, string? ProductCode, string? SchemeCode,
    bool? HasPendingKycResolution, int Page = 1, int PageSize = 20);

public sealed record LoanAccountListItemResponse(
    Guid Id, string ApplicationNumber, string AccountNumber, string AccountStatus,
    string CustomerCode, string CustomerName, string? MobileMasked, string? City,
    string KycStatus, bool KycFlagged, string? KycSource, string? KycMode, DateTime? KycCheckedAtUtc,
    bool HasPendingKycResolution, DateTime OpenedAt);

public sealed record KycCheckResultsResponse(
    string PanValidation, string PanNameMatch, string AadhaarValidation, string AadhaarAddressMatch);

public sealed record KycHistoryEntryResponse(
    string Source, string KycStatus, KycCheckResultsResponse Checks, string? Reason, string? PreviousStatus,
    string Actor, DateTime OccurredAtUtc);

public sealed record KycResolutionRequestResponse(
    Guid Id, Guid AccountId, string Status, Guid MakerUserId, string MakerName, string Reason,
    string PreviousKycStatus, DateTime SubmittedAtUtc,
    Guid? CheckerUserId, string? CheckerName, string? DecisionComment, DateTime? DecidedAtUtc);

public sealed record LoanAccountResponse(
    Guid Id, Guid ApplicationId, string ApplicationNumber, string AccountNumber, string AccountName,
    string AccountStatus, Guid CustomerId, string CustomerCode, string CustomerName,
    string? MobileMasked, string? City, string? PanMasked, string? AadhaarMasked,
    Guid LineOfBusinessId, Guid BranchId, string ProductCode, string SchemeCode,
    decimal FinanceAmount, decimal IrrPercent, int Tenure, string RepaymentFrequency, DateOnly DisbursementDate,
    string KycStatus, bool KycFlagged, string? KycSource, DateTime? KycCheckedAtUtc,
    KycCheckResultsResponse KycChecks, Guid? KycVerificationSessionId,
    IReadOnlyList<KycHistoryEntryResponse> KycHistory,
    KycResolutionRequestResponse? PendingKycResolution,
    IReadOnlyList<KycResolutionRequestResponse> KycResolutionRequests,
    string? CreationNotes, DateTime OpenedAt, DateTime CreatedAt, DateTime UpdatedAt, string RowVersion);

public sealed record UpdateLoanAccountRequest(string? AccountName, string? Status, string RowVersion);
public sealed record SubmitKycResolutionRequest(string Reason);
public sealed record DecideKycResolutionRequest(string Comment);
public sealed record RecheckKycRequest(string? Reason);
public sealed record ReprocessRequest(string Reason);

public sealed record AccountQueueStatusResponse(
    IReadOnlyDictionary<string, int> Counts, DateTime? OldestNonCompletedAtUtc, double? OldestNonCompletedAgeMinutes);

public sealed record DeadLetterItemResponse(
    Guid RequestId, string ApplicationNumber, string? FailureReason, int AttemptCount, DateTime LastAttemptAtUtc);