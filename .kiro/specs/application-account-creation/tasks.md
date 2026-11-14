# Implementation Plan: Application Account Creation

## Overview

This file is the **copy-and-paste execution script** for the Application Account Creation feature (`requirements.md`, `design.md` in this folder). Nothing here has been applied, built, or tested. Apply the tasks in order; each sub-task is marked **CREATE** (new file, full content) or **MODIFY** (exact snippet plus where it goes). Paths are relative to:

- Backend: `e:\Finnova\Finnova-API` (solution `Finnova.Backend.slnx`)
- Frontend: `E:\Finnova\Finnova-UI\Finnova-UI`
- Worker: `e:\Finnova\Finnova-API\workers\account-creation-worker`

Conventions verified against the existing code: file-scoped namespaces, `Finnova.Models` / `Finnova.Repository` / `Finnova.Service` layering, MediatR + FluentValidation (`ValidationBehavior`), repositories registered in `Finnova.Repository.DependencyInjection.AddFinnovaRepository`, snake_case or PascalCase table names as already used (`accounts`, `applications`, `Customers`, `CustomerAddresses`, `CustomerDocumentReferences`, `IndividualCustomerDetails`, `document_master`, `locations`, `lines_of_business`), enums stored as strings, India-only and English-only values, React 18 + MUI + shared axios + service/mock/real/toggle pattern, Vitest + React Testing Library.

Decisions recorded while grounding the tasks in the code (flag any you want changed):

- **LOB token:** `lines_of_business` has no code column, so `{LOB}` renders the LOB name reduced to upper-case letters and digits, at most 10 characters (`LobTokenFormatter`). `{BRANCH}` renders `locations.Code` (Level 5 branch).
- **Branch** means a `locations` row with `Level = 5` (same as `BranchReferenceAdapter`).
- **KycMode in responses** equals the stored `KycSource` (`Bypass`, `Internal`, `Aggregator`, `Manual`).
- **Retry schedule:** delays `1,5,30` minutes are configured, and `max attempts = 3` (R2.7). With the default of 3 attempts the third failure dead-letters, so the 30-minute delay applies only if `ACW_MAX_ATTEMPTS` is raised.
- **SystemUser and service credential** are created by an idempotent startup seeder in `Finnova.SystemAdminService` (the PBKDF2 hash needs a secret from secure configuration, so it cannot be a static `HasData` row). The roles are seeded by `HasData`.
- **Gateway internal block** is a small middleware in `Finnova.ApiGateway/Program.cs` that returns 404 for any path containing `/internal/` (a route-level `DenyAll` policy would need an authentication scheme the gateway does not have). The SystemAdmin host also rejects `/internal/**` when forwarded headers are present.
- **Tests are not marked optional.** The workspace rule requires tests with every feature.

## Tasks

- [ ] 1. Backend domain: enums, entities, and extensions
  - [ ] 1.1 CREATE `Finnova.Models/Domain/Enums/AccountCreationEnums.cs`
    - _Requirements: 5.4, 6.2, 9.1, 10.1_

```csharp
namespace Finnova.Models.Domain.Enums;

public enum AccountRequestStatus { Pending = 0, Processing = 1, Retry = 2, Completed = 3, DeadLetter = 4 }

public enum KycStatus { Pending = 0, Passed = 1, Failed = 2, PartiallyVerified = 3, Unavailable = 4, Bypassed = 5 }

public enum KycCheckResult { Passed = 0, Failed = 1, NotChecked = 2, Unavailable = 3, Bypassed = 4 }

public enum KycSource { Bypass = 0, Internal = 1, Aggregator = 2, Manual = 3 }

public enum KycMode { Bypass = 0, Internal = 1, Aggregator = 2 }

public enum KycResolutionStatus { Pending = 0, Approved = 1, Rejected = 2 }
```

  - [ ] 1.2 CREATE `Finnova.Models/Domain/Entities/LoanAccount.cs`
    - New dedicated entity for Loan Accounts, stored in its own table `loan_accounts`. The existing `Account` entity, its `accounts` table, `IAccountRepository`, `AccountsController`, and `Transaction` are **not changed** (R8.20). `BankAccount` is not used as a name because it is already the Customer bank-account child entity.
    - Column names mirror the existing `Account` so `AccountType`/`AccountStatus` and the account-number rules are reused. Loan-specific reference columns are nullable in the CLR type only to keep the query joins simple; the worker always sets them on creation.
    - _Requirements: 3.1, 3.2, 5.10, 8.18_

```csharp
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
```

  - [ ] 1.3 CREATE `Finnova.Models/Domain/Entities/AccountCreationRequest.cs`
    - _Requirements: 1.1, 1.2, 2.1, 2.2_

```csharp
using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

/// <summary>Outbox row inserted when an Application becomes Processed; claimed by the Python worker.</summary>
public sealed class AccountCreationRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ApplicationId { get; set; }
    public string ApplicationNumber { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public Guid? InitiatedByUserId { get; set; }
    public AccountRequestStatus Status { get; set; } = AccountRequestStatus.Pending;
    public int AttemptCount { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
    public string? ClaimedBy { get; set; }
    public DateTime? LeaseExpiresAtUtc { get; set; }
    public string? FailureReason { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
}
```

  - [ ] 1.4 CREATE `Finnova.Models/Domain/Entities/AccountKycJob.cs`
    - _Requirements: 5.1, 9.11_

```csharp
using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

/// <summary>Phase 2 work item (the KYC Check) for one Account. Re-armed by a KYC re-check.</summary>
public sealed class AccountKycJob
{
    public Guid AccountId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public AccountRequestStatus Status { get; set; } = AccountRequestStatus.Pending;
    public int AttemptCount { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
    public string? ClaimedBy { get; set; }
    public DateTime? LeaseExpiresAtUtc { get; set; }
    public string? FailureReason { get; set; }
    public string? RecheckRequestedBy { get; set; }
    public string? RecheckReason { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
```

  - [ ] 1.5 CREATE `Finnova.Models/Domain/Entities/AccountKycHistoryEntry.cs`
    - _Requirements: 5.10, 9.6, 11.4_

```csharp
using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

/// <summary>Append-only KYC outcome record. Never stores PAN or Aadhaar values.</summary>
public sealed class AccountKycHistoryEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public KycSource Source { get; set; }
    public KycStatus KycStatus { get; set; }
    public KycCheckResult PanValidation { get; set; }
    public KycCheckResult PanNameMatch { get; set; }
    public KycCheckResult AadhaarValidation { get; set; }
    public KycCheckResult AadhaarAddressMatch { get; set; }
    public Guid? VerificationSessionId { get; set; }
    public string? ProviderReference { get; set; }
    public KycStatus? PreviousStatus { get; set; }
    public string? Reason { get; set; }
    public string? Comment { get; set; }
    public Guid? ResolutionRequestId { get; set; }
    public Guid? MakerUserId { get; set; }
    public Guid? CheckerUserId { get; set; }
    public string Actor { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
```

  - [ ] 1.6 CREATE `Finnova.Models/Domain/Entities/AccountKycResolutionRequest.cs`
    - _Requirements: 9.1, 9.2, 9.3, 9.9, 9.13_

```csharp
using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

/// <summary>Maker-checker request to clear a KYC flag manually. At most one Pending per Account.</summary>
public sealed class AccountKycResolutionRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public KycResolutionStatus Status { get; set; } = KycResolutionStatus.Pending;
    public Guid MakerUserId { get; set; }
    public string MakerName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public KycStatus PreviousKycStatus { get; set; }
    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? CheckerUserId { get; set; }
    public string? CheckerName { get; set; }
    public string? DecisionComment { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
```

  - [ ] 1.7 CREATE `Finnova.Models/Domain/Entities/AccountAuditEntry.cs`
    - _Requirements: 11.7, 11.8_

```csharp
namespace Finnova.Models.Domain.Entities;

/// <summary>Immutable audit row. The repository exposes Add only (no update or delete).</summary>
public sealed class AccountAuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? AccountId { get; set; }
    public Guid? RequestId { get; set; }
    public string Action { get; set; } = string.Empty;   // Created|KycOutcome|Updated|StatusChanged|KycResolutionSubmitted|KycResolutionApproved|KycResolutionRejected|KycRecheck|Reprocessed|DeadLettered
    public string Source { get; set; } = "User";         // Worker|User
    public string Actor { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string ChangeSummary { get; set; } = string.Empty; // non-sensitive only
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
```

  - [ ] 1.8 CREATE `Finnova.Models/Domain/Entities/AppRole.cs`
    - _Requirements: 18.1, 18.3_

```csharp
namespace Finnova.Models.Domain.Entities;

public sealed class AppRole
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsSystem { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

public sealed class UserAccountRole
{
    public Guid UserAccountId { get; set; }
    public Guid AppRoleId { get; set; }
}
```

  - [ ] 1.9 CREATE `Finnova.Models/Domain/Entities/ServiceCredential.cs`
    - _Requirements: 3.10 (design D3)_

```csharp
namespace Finnova.Models.Domain.Entities;

/// <summary>Machine credential for the seeded SystemUser. Only a salted PBKDF2 hash is stored.</summary>
public sealed class ServiceCredential
{
    public Guid ClientId { get; set; }          // the SystemUser id
    public string SecretHash { get; set; } = string.Empty; // "salt.hash" (PasswordHasher format)
    public bool IsActive { get; set; } = true;
    public DateTime? LastUsedAtUtc { get; set; }
}
```

  - [ ] 1.10 CREATE `Finnova.Models/Domain/Roles/AppRoleCatalog.cs`
    - Declares the four roles with fixed GUIDs (used by `HasData`) and a pure, idempotent seeding function (Property 21).
    - _Requirements: 18.1, 18.2, 18.3_

```csharp
namespace Finnova.Models.Domain.Roles;

public sealed record AppRoleDefinition(Guid Id, string Name, string Description);

public static class AppRoleCatalog
{
    public const string ApplicationProcessor = "ApplicationProcessor";
    public const string AccountsViewer = "AccountsViewer";
    public const string AccountsOperator = "AccountsOperator";
    public const string KycApprover = "KycApprover";

    public static readonly IReadOnlyList<AppRoleDefinition> Roles = new[]
    {
        new AppRoleDefinition(new Guid("00000000-0000-0000-0003-000000000001"), ApplicationProcessor, "Creates and processes applications; submits KYC resolution requests (maker)."),
        new AppRoleDefinition(new Guid("00000000-0000-0000-0003-000000000002"), AccountsViewer, "Read-only access to Loan Accounts."),
        new AppRoleDefinition(new Guid("00000000-0000-0000-0003-000000000003"), AccountsOperator, "Operates the Account Creation queue and dead-letter recovery."),
        new AppRoleDefinition(new Guid("00000000-0000-0000-0003-000000000004"), KycApprover, "Approves or rejects KYC resolution requests (checker)."),
    };
}

public static class AppRoleSeeder
{
    /// <summary>Catalog roles whose name is not already present (case-insensitive).</summary>
    public static IReadOnlyList<AppRoleDefinition> MissingRoles(IEnumerable<string> existingNames)
    {
        var existing = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);
        return AppRoleCatalog.Roles.Where(r => !existing.Contains(r.Name)).ToList();
    }

    /// <summary>Returns existing names unchanged plus any missing catalog roles. Running it again adds nothing.</summary>
    public static IReadOnlyList<string> Apply(IEnumerable<string> existingNames)
    {
        var current = existingNames.ToList();
        current.AddRange(MissingRoles(current).Select(r => r.Name));
        return current;
    }
}
```

  - [ ] 1.11 MODIFY `Finnova.Models/Domain/Entities/NumberingScheme.cs`
    - Add after the `ScopeId` property.
    - _Requirements: 16.1, 16.2_

```csharp
    public Guid? LineOfBusinessId { get; set; }                // optional LOB dimension (application-account-creation R16)
```

- [ ] 2. Backend domain: exceptions
  - [ ] 2.1 CREATE `Finnova.Models/Domain/Exceptions/LoanAccountExceptions.cs`
    - _Requirements: 8.10, 8.14, 9.4, 11.6_

```csharp
namespace Finnova.Models.Domain.Exceptions;

public abstract class LoanAccountException : Exception
{
    protected LoanAccountException(string message) : base(message) { }
    public abstract string ErrorCode { get; }
}

public sealed class LoanAccountNotFoundException : LoanAccountException
{
    public LoanAccountNotFoundException(string message) : base(message) { }
    public static LoanAccountNotFoundException ForAccount(Guid id) => new($"Loan account '{id}' was not found.");
    public override string ErrorCode => "ERR-ACC-404";
}

public sealed class LoanAccountValidationException : LoanAccountException
{
    public LoanAccountValidationException(string message) : base(message) { }
    public override string ErrorCode => "ERR-ACC-400";
}

public sealed class LoanAccountConflictException : LoanAccountException
{
    public LoanAccountConflictException(string message) : base(message) { }
    public override string ErrorCode => "ERR-ACC-409";
}

/// <summary>Checker equals maker (four-eyes). Maps to HTTP 403.</summary>
public sealed class KycResolutionForbiddenException : LoanAccountException
{
    public KycResolutionForbiddenException() : base("The maker of a KYC resolution request cannot approve or reject it.") { }
    public override string ErrorCode => "ERR-ACC-403";
}

/// <summary>Aggregator mode selected but no provider is configured.</summary>
public sealed class KycAggregatorNotConfiguredException : Exception
{
    public KycAggregatorNotConfiguredException() : base("No KYC aggregator provider is configured.") { }
}
```

- [ ] 3. Backend contracts
  - [ ] 3.1 CREATE `Finnova.Models/Contracts/LoanAccounts/LoanAccountContracts.cs`
    - _Requirements: 8.1, 8.9, 9.1_

```csharp
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
```

  - [ ] 3.2 CREATE `Finnova.Models/Contracts/Internal/InternalContracts.cs`
    - Contracts for the SystemAdmin internal endpoints and the Customer Master mapping profile.
    - _Requirements: 4.1, 5.1, 7.1, 7.2, 7.3, 16.8, 17.2, 17.3_

```csharp
using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Contracts.Internal;

public sealed record InternalIssueRequest(string DocumentType, Guid LineOfBusinessId, Guid BranchId, string CorrelationId);
public sealed record InternalIssueResponse(string IssuedNumber, Guid ResolvedSchemeId, string ResolvedSchemeCode, string ResolvedScopeLevel);

public sealed record CustomerNameParts(string? First, string? Second, string? Third, string? Fourth, string? Fifth, string? Sixth);
public sealed record CustomerAddressParts(string? House, string? Block, string? Way, string? Area, string? Landmark,
    string? City, string? State, string? PostalCode, string? Country);
public sealed record MappedVerificationValues(string Name, string Address);

public sealed record InternalKycRequest(Guid CustomerId, string? Pan, string? Aadhaar,
    CustomerNameParts Name, CustomerAddressParts? Address, string CorrelationId, string Mode);

public sealed record InternalKycResponse(
    string PanValidation, string PanNameMatch, string AadhaarValidation, string AadhaarAddressMatch,
    Guid? VerificationSessionId, string? ProviderReference, string? SafeReason, DateTime TimestampUtc);

public sealed record ServiceTokenRequest(Guid ClientId, string ClientSecret);
public sealed record ServiceTokenResponse(string AccessToken, int ExpiresInSeconds);

/// <summary>Provider-neutral request to a KYC aggregator (no provider-specific fields).</summary>
public sealed record ProviderNeutralKycRequest(Guid CustomerId, string? Pan, string? Aadhaar,
    string CustomerName, string Address, string CorrelationId);

/// <summary>Provider-independent aggregator result.</summary>
public sealed record NormalizedKycResult(
    KycCheckResult PanValidation, KycCheckResult PanNameMatch,
    KycCheckResult AadhaarValidation, KycCheckResult AadhaarAddressMatch,
    KycCheckResult Overall, string? ProviderReference, string? SafeReason, DateTime TimestampUtc);
```

  - [ ] 3.3 MODIFY `Finnova.Models/Contracts/DocumentNumberControl/CreateNumberingSchemeRequest.cs`
    - Add a trailing optional parameter after `bool? IsActive`.
    - _Requirements: 16.1, 16.2_

```csharp
    bool? IsActive,
    Guid? LineOfBusinessId = null);
```

  - [ ] 3.4 MODIFY `Finnova.Models/Contracts/DocumentNumberControl/NumberingSchemeResponse.cs`
    - Add a trailing optional parameter after `DateTime UpdatedAt`.
    - _Requirements: 16.1_

```csharp
    DateTime UpdatedAt,
    Guid? LineOfBusinessId = null);
```

  - [ ] 3.5 MODIFY `Finnova.Models/Contracts/DocumentNumberControl/IssuedNumberResponse.cs`
    - Add trailing optional parameters (used by the internal endpoint to echo the resolved scheme).
    - _Requirements: 16.8_

```csharp
    DateTime IssuedAtUtc,
    string? SchemeCode = null,
    string? ScopeLevel = null);
```

- [ ] 4. Backend EF configuration, DbContext, and migration
  - [ ] 4.1 CREATE `Finnova.Repository/Configuration/LoanAccountConfiguration.cs`
    - Maps `LoanAccount` to the new table `loan_accounts`. `AccountNumber` is 100 characters (design D1: DNC templates up to 100). The existing `AccountConfiguration` and `accounts` table are not changed.
    - _Requirements: 3.2, 4.3, 4.9, 6.6, 8.18_

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class LoanAccountConfiguration : IEntityTypeConfiguration<LoanAccount>
{
    public void Configure(EntityTypeBuilder<LoanAccount> builder)
    {
        builder.ToTable("loan_accounts");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.AccountNumber).IsRequired().HasMaxLength(100);   // DNC templates up to 100 (D1)
        builder.HasIndex(a => a.AccountNumber).IsUnique();
        builder.Property(a => a.AccountName).IsRequired().HasMaxLength(200);
        builder.Property(a => a.Type).HasConversion<string>().HasMaxLength(50);
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(50);
        builder.Property(a => a.Balance).HasPrecision(18, 4);
        builder.Property(a => a.Currency).IsRequired().HasMaxLength(3);
        builder.Property(a => a.BranchCode).HasMaxLength(20);

        // ---- Origination references and KYC ----
        builder.Property(a => a.ProductCode).HasMaxLength(50);
        builder.Property(a => a.SchemeCode).HasMaxLength(50);
        builder.Property(a => a.CustomerNameSnapshot).HasMaxLength(300);
        builder.Property(a => a.CreationNotes).HasMaxLength(1000);
        builder.Property(a => a.KycStatus).HasConversion<string>().HasMaxLength(30);
        builder.Property(a => a.KycSource).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.KycPanValidation).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.KycPanNameMatch).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.KycAadhaarValidation).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.KycAadhaarAddressMatch).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.KycProviderReference).HasMaxLength(100);
        builder.Property(a => a.RowVersion).IsRowVersion();

        // At most one Loan Account per Application (A6).
        builder.HasIndex(a => a.ApplicationId).IsUnique().HasFilter("[ApplicationId] IS NOT NULL");
        builder.HasIndex(a => a.CustomerId);
        builder.HasIndex(a => a.LineOfBusinessId);
        builder.HasIndex(a => a.BranchId);
        builder.HasIndex(a => a.ProductCode);
        builder.HasIndex(a => a.SchemeCode);
        builder.HasIndex(a => new { a.KycFlagged, a.KycStatus });
    }
}
```

  - [ ] 4.2 CREATE `Finnova.Repository/Configuration/LoanAccountConfigurations.cs`
    - Configuration for all new tables, including the check constraints and the filtered unique index that guarantees one Pending resolution per Account, and the `HasData` seed of the four roles.
    - _Requirements: 1.4, 9.9, 9.13, 11.8, 18.1_

```csharp
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Roles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finnova.Repository.Configuration;

public sealed class AccountCreationRequestConfiguration : IEntityTypeConfiguration<AccountCreationRequest>
{
    public void Configure(EntityTypeBuilder<AccountCreationRequest> b)
    {
        b.ToTable("account_creation_requests", t =>
        {
            t.HasCheckConstraint("CK_acr_attempts", "[AttemptCount] >= 0 AND [AttemptCount] <= 10");
            t.HasCheckConstraint("CK_acr_retry_time", "[Status] <> 'Retry' OR [NextAttemptAtUtc] IS NOT NULL");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.ApplicationNumber).IsRequired().HasMaxLength(50);
        b.Property(x => x.CorrelationId).IsRequired().HasMaxLength(64);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.ClaimedBy).HasMaxLength(100);
        b.Property(x => x.FailureReason).HasMaxLength(100);
        b.HasIndex(x => x.ApplicationId).IsUnique();
        b.HasIndex(x => new { x.Status, x.NextAttemptAtUtc });
        b.HasIndex(x => new { x.Status, x.LeaseExpiresAtUtc });
    }
}

public sealed class AccountKycJobConfiguration : IEntityTypeConfiguration<AccountKycJob>
{
    public void Configure(EntityTypeBuilder<AccountKycJob> b)
    {
        b.ToTable("account_kyc_jobs", t => t.HasCheckConstraint("CK_akj_attempts", "[AttemptCount] >= 0 AND [AttemptCount] <= 10"));
        b.HasKey(x => x.AccountId);
        b.Property(x => x.CorrelationId).IsRequired().HasMaxLength(64);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.ClaimedBy).HasMaxLength(100);
        b.Property(x => x.FailureReason).HasMaxLength(100);
        b.Property(x => x.RecheckRequestedBy).HasMaxLength(200);
        b.Property(x => x.RecheckReason).HasMaxLength(500);
        b.HasOne<LoanAccount>().WithOne().HasForeignKey<AccountKycJob>(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.Status, x.NextAttemptAtUtc });
        b.HasIndex(x => new { x.Status, x.LeaseExpiresAtUtc });
    }
}

public sealed class AccountKycHistoryEntryConfiguration : IEntityTypeConfiguration<AccountKycHistoryEntry>
{
    public void Configure(EntityTypeBuilder<AccountKycHistoryEntry> b)
    {
        b.ToTable("account_kyc_history");
        b.HasKey(x => x.Id);
        b.Property(x => x.Source).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.KycStatus).HasConversion<string>().HasMaxLength(30).IsRequired();
        b.Property(x => x.PreviousStatus).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.PanValidation).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.PanNameMatch).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.AadhaarValidation).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.AadhaarAddressMatch).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.ProviderReference).HasMaxLength(100);
        b.Property(x => x.Reason).HasMaxLength(500);
        b.Property(x => x.Comment).HasMaxLength(500);
        b.Property(x => x.Actor).IsRequired().HasMaxLength(200);
        b.Property(x => x.CorrelationId).IsRequired().HasMaxLength(64);
        b.HasOne<LoanAccount>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.AccountId, x.OccurredAtUtc });
    }
}

public sealed class AccountKycResolutionRequestConfiguration : IEntityTypeConfiguration<AccountKycResolutionRequest>
{
    public void Configure(EntityTypeBuilder<AccountKycResolutionRequest> b)
    {
        b.ToTable("account_kyc_resolution_requests", t =>
        {
            t.HasCheckConstraint("CK_akrr_reason_len", "LEN(LTRIM(RTRIM([Reason]))) BETWEEN 10 AND 500");
            t.HasCheckConstraint("CK_akrr_four_eyes", "[CheckerUserId] IS NULL OR [CheckerUserId] <> [MakerUserId]");
            t.HasCheckConstraint("CK_akrr_decision", "[Status] = 'Pending' OR ([CheckerUserId] IS NOT NULL AND [DecisionComment] IS NOT NULL AND [DecidedAtUtc] IS NOT NULL)");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.PreviousKycStatus).HasConversion<string>().HasMaxLength(30).IsRequired();
        b.Property(x => x.MakerName).IsRequired().HasMaxLength(200);
        b.Property(x => x.CheckerName).HasMaxLength(200);
        b.Property(x => x.Reason).IsRequired().HasMaxLength(500);
        b.Property(x => x.DecisionComment).HasMaxLength(500);
        b.Property(x => x.CorrelationId).IsRequired().HasMaxLength(64);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne<LoanAccount>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        // At most one Pending request per Account (R9.9, R9.13).
        b.HasIndex(x => x.AccountId).IsUnique().HasFilter("[Status] = 'Pending'").HasDatabaseName("UX_account_kyc_resolution_pending");
        b.HasIndex(x => new { x.AccountId, x.SubmittedAtUtc });
        b.HasIndex(x => new { x.Status, x.SubmittedAtUtc });
    }
}

public sealed class AccountAuditEntryConfiguration : IEntityTypeConfiguration<AccountAuditEntry>
{
    public void Configure(EntityTypeBuilder<AccountAuditEntry> b)
    {
        b.ToTable("account_audit_entries");
        b.HasKey(x => x.Id);
        b.Property(x => x.Action).IsRequired().HasMaxLength(40);
        b.Property(x => x.Source).IsRequired().HasMaxLength(10);
        b.Property(x => x.Actor).IsRequired().HasMaxLength(200);
        b.Property(x => x.CorrelationId).IsRequired().HasMaxLength(64);
        b.Property(x => x.ChangeSummary).IsRequired().HasMaxLength(1000);
        b.HasIndex(x => new { x.AccountId, x.OccurredAtUtc });
    }
}

public sealed class AppRoleConfiguration : IEntityTypeConfiguration<AppRole>
{
    private static readonly DateTime SeedDate = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<AppRole> b)
    {
        b.ToTable("app_roles");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(50);
        b.Property(x => x.Description).IsRequired().HasMaxLength(200);
        b.HasIndex(x => x.Name).IsUnique();
        // HasData with fixed keys is idempotent across migrations and re-runs (R18.3).
        b.HasData(AppRoleCatalog.Roles.Select(r => new AppRole
        {
            Id = r.Id, Name = r.Name, Description = r.Description, IsSystem = true, CreatedAt = SeedDate
        }));
    }
}

public sealed class UserAccountRoleConfiguration : IEntityTypeConfiguration<UserAccountRole>
{
    public void Configure(EntityTypeBuilder<UserAccountRole> b)
    {
        b.ToTable("user_account_roles");
        b.HasKey(x => new { x.UserAccountId, x.AppRoleId });
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserAccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AppRole>().WithMany().HasForeignKey(x => x.AppRoleId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ServiceCredentialConfiguration : IEntityTypeConfiguration<ServiceCredential>
{
    public void Configure(EntityTypeBuilder<ServiceCredential> b)
    {
        b.ToTable("service_credentials");
        b.HasKey(x => x.ClientId);
        b.Property(x => x.SecretHash).IsRequired().HasMaxLength(400);
    }
}
```

  - [ ] 4.3 MODIFY `Finnova.Repository/Configuration/NumberingSchemeConfiguration.cs`
    - Replace the `(DocumentType, Scope, ScopeId)` unique index with the four-column index and map the new column. Replace this existing block:

```csharp
        builder.HasIndex(x => new { x.DocumentType, x.Scope, x.ScopeId }).IsUnique();
```

    with:

```csharp
        // Extended with the optional LOB dimension (application-account-creation R16.9). SQL Server
        // treats NULLs as equal in unique indexes, so existing non-LOB uniqueness is preserved.
        builder.HasIndex(x => new { x.DocumentType, x.Scope, x.ScopeId, x.LineOfBusinessId }).IsUnique();
```

    - _Requirements: 16.1, 16.7, 16.9_

  - [ ] 4.4 MODIFY `Finnova.Repository/Context/FinnovaDbContext.cs`
    - Add after the `JvPostingInboxes` DbSet (the `using Finnova.Models.Domain.Entities;` already exists). The existing `DbSet<Account> Accounts` stays as is.
    - _Requirements: 1.1, 8.1, 18.1_

```csharp
    public DbSet<LoanAccount> LoanAccounts => Set<LoanAccount>();
    public DbSet<AccountCreationRequest> AccountCreationRequests => Set<AccountCreationRequest>();
    public DbSet<AccountKycJob> AccountKycJobs => Set<AccountKycJob>();
    public DbSet<AccountKycHistoryEntry> AccountKycHistoryEntries => Set<AccountKycHistoryEntry>();
    public DbSet<AccountKycResolutionRequest> AccountKycResolutionRequests => Set<AccountKycResolutionRequest>();
    public DbSet<AccountAuditEntry> AccountAuditEntries => Set<AccountAuditEntry>();
    public DbSet<AppRole> AppRoles => Set<AppRole>();
    public DbSet<UserAccountRole> UserAccountRoles => Set<UserAccountRole>();
    public DbSet<ServiceCredential> ServiceCredentials => Set<ServiceCredential>();
```

  - [ ] 4.5 Migration command (run by you after tasks 1 to 5 compile)
    - Review the generated migration: it must `CreateTable` the nine new tables (`loan_accounts` plus the eight in task 4.2), drop and recreate the `numbering_schemes` unique index, and `InsertData` for `app_roles`. It must contain **no** operation on the existing `accounts` or `transactions` tables; if it does, the model has drifted and should be fixed before applying.
    - _Requirements: 4.9_

```powershell
dotnet ef migrations add AddLoanAccountsAndDncLob --project Finnova.Repository --startup-project Finnova.AccountsService
dotnet ef database update --project Finnova.Repository --startup-project Finnova.AccountsService
```

    To grant a role to a user after the migration (no assignment screen is in scope):

```sql
INSERT INTO user_account_roles (UserAccountId, AppRoleId)
SELECT u.Id, r.Id FROM user_accounts u CROSS JOIN app_roles r
WHERE u.UserName = 'usr001' AND r.Name = 'ApplicationProcessor';
```

- [ ] 5. Backend repositories and DI
  - [ ] 5.1 CREATE `Finnova.Models/Domain/Numbering/LobTokenFormatter.cs`
    - Pure helper that turns a LOB name into the `{LOB}` token value.
    - _Requirements: 16.3_

```csharp
namespace Finnova.Models.Domain.Numbering;

/// <summary>Renders the {LOB} token: upper-case letters and digits of the LOB name, at most 10 characters.</summary>
public static class LobTokenFormatter
{
    public const int MaxLength = 10;

    public static string ToToken(string lobName)
    {
        var chars = (lobName ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).Take(MaxLength);
        return new string(chars.ToArray());
    }
}
```

  - [ ] 5.2 CREATE `Finnova.Repository/Interfaces/LoanAccountRepositoryInterfaces.cs`
    - _Requirements: 1.1, 8.1, 8.9, 9.1, 10.1, 11.8_

```csharp
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
```

  - [ ] 5.3 CREATE `Finnova.Repository/Repositories/LoanAccountRepositories.cs`
    - _Requirements: 1.1, 8.1 to 8.9, 9.1, 10.1, 11.8, 16.12_

```csharp
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Numbering;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Finnova.Repository.Repositories;

public sealed class LoanAccountRepository : ILoanAccountRepository
{
    private readonly FinnovaDbContext _db;
    public LoanAccountRepository(FinnovaDbContext db) => _db = db;

    public async Task<(List<LoanAccountRow> Rows, int Total)> SearchAsync(LoanAccountFilters f, CancellationToken ct)
    {
        var q = from a in _db.LoanAccounts.AsNoTracking()
                join ap in _db.Applications.AsNoTracking() on a.ApplicationId equals (Guid?)ap.Id
                join c in _db.Customers.AsNoTracking() on a.CustomerId equals (Guid?)c.Id
                where a.Type == AccountType.Loan
                select new
                {
                    Account = a,
                    ApplicationNumber = ap.ApplicationNumber,
                    CustomerCode = c.CustomerCode,
                    HasPending = _db.AccountKycResolutionRequests.Any(r => r.AccountId == a.Id && r.Status == KycResolutionStatus.Pending)
                };

        var term = f.Search?.Trim().ToLower();
        if (!string.IsNullOrEmpty(term))
            q = q.Where(x => x.ApplicationNumber.ToLower().Contains(term)
                          || x.Account.AccountNumber.ToLower().Contains(term)
                          || x.CustomerCode.ToLower().Contains(term)
                          || (x.Account.CustomerNameSnapshot ?? "").ToLower().Contains(term));
        if (f.KycStatus.HasValue) q = q.Where(x => x.Account.KycStatus == f.KycStatus);
        if (f.KycFlagged.HasValue) q = q.Where(x => x.Account.KycFlagged == f.KycFlagged);
        if (f.AccountStatus.HasValue) q = q.Where(x => x.Account.Status == f.AccountStatus);
        if (f.LineOfBusinessId.HasValue) q = q.Where(x => x.Account.LineOfBusinessId == f.LineOfBusinessId);
        if (f.BranchId.HasValue) q = q.Where(x => x.Account.BranchId == f.BranchId);
        if (!string.IsNullOrWhiteSpace(f.ProductCode)) q = q.Where(x => x.Account.ProductCode == f.ProductCode!.Trim());
        if (!string.IsNullOrWhiteSpace(f.SchemeCode)) q = q.Where(x => x.Account.SchemeCode == f.SchemeCode!.Trim());
        if (f.HasPendingKycResolution.HasValue) q = q.Where(x => x.HasPending == f.HasPendingKycResolution);

        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(x => x.Account.CreatedAt).ThenBy(x => x.Account.AccountNumber)
            .Skip((f.Page - 1) * f.PageSize).Take(f.PageSize).ToListAsync(ct);
        return (rows.Select(x => new LoanAccountRow(x.Account, x.ApplicationNumber, x.CustomerCode, x.HasPending)).ToList(), total);
    }

    public async Task<Dictionary<Guid, CustomerContact>> GetContactsAsync(IReadOnlyCollection<Guid> customerIds, CancellationToken ct)
    {
        if (customerIds.Count == 0) return new();
        var addresses = await _db.CustomerAddresses.AsNoTracking().Where(x => customerIds.Contains(x.CustomerId)).ToListAsync(ct);
        return addresses.GroupBy(x => x.CustomerId).ToDictionary(g => g.Key, g =>
        {
            var a = g.FirstOrDefault(x => x.AddressType == CustomerAddressType.Communication) ?? g.First();
            return new CustomerContact(a.Mobile, a.City);
        });
    }

    public async Task<LoanAccountViewData?> GetViewDataAsync(Guid id, string panCode, string aadhaarCode, CancellationToken ct)
    {
        var account = await _db.LoanAccounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id && a.Type == AccountType.Loan && a.ApplicationId != null, ct);
        if (account is null) return null;

        var app = await _db.Applications.AsNoTracking().FirstAsync(x => x.Id == account.ApplicationId, ct);
        var customer = await _db.Customers.AsNoTracking()
            .Include(c => c.Addresses).Include(c => c.IndividualDetails)
            .FirstOrDefaultAsync(c => c.Id == account.CustomerId, ct);

        var address = customer?.Addresses.FirstOrDefault(x => x.AddressType == CustomerAddressType.Communication)
                      ?? customer?.Addresses.FirstOrDefault();
        var name = customer is null
            ? account.CustomerNameSnapshot ?? string.Empty
            : string.Join(' ', new[] { customer.FirstName, customer.SecondName, customer.ThirdName, customer.FourthName, customer.FifthName, customer.SixthName }
                .Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));

        var docs = await _db.CustomerDocumentReferences.AsNoTracking().Where(d => d.CustomerId == account.CustomerId).ToListAsync(ct);
        var aadhaarDefinitionIds = await _db.Documents.AsNoTracking().Where(d => d.IsAadhaarDocument).Select(d => d.Id).ToListAsync(ct);
        string? pan = docs.FirstOrDefault(d => string.Equals(d.DocumentCode, panCode, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(d.Value))?.Value;
        string? aadhaar = docs.FirstOrDefault(d => (string.Equals(d.DocumentCode, aadhaarCode, StringComparison.OrdinalIgnoreCase)
                                                   || aadhaarDefinitionIds.Contains(d.DocumentDefinitionId)) && !string.IsNullOrWhiteSpace(d.Value))?.Value;
        var identityType = customer?.IndividualDetails?.IndiaIdentityType?.Trim();
        var identityRef = customer?.IndividualDetails?.IndiaIdentityReference;
        if (pan is null && string.Equals(identityType, "PAN", StringComparison.OrdinalIgnoreCase)) pan = identityRef;
        if (aadhaar is null && string.Equals(identityType, "AADHAAR", StringComparison.OrdinalIgnoreCase)) aadhaar = identityRef;

        var history = await _db.AccountKycHistoryEntries.AsNoTracking().Where(h => h.AccountId == id).OrderBy(h => h.OccurredAtUtc).ToListAsync(ct);
        var resolutions = await _db.AccountKycResolutionRequests.AsNoTracking().Where(r => r.AccountId == id).OrderBy(r => r.SubmittedAtUtc).ToListAsync(ct);

        return new LoanAccountViewData(
            account, app.ApplicationNumber,
            new ApplicationFinanceSummary(app.FinanceAmount, app.IrrPercent, app.Tenure, app.RepaymentFrequency.ToString(), app.DisbursementDate),
            new CustomerViewData(customer?.CustomerCode ?? string.Empty, name, address?.Mobile, address?.City),
            pan, aadhaar, history, resolutions);
    }

    public Task<LoanAccount?> GetTrackedAsync(Guid id, CancellationToken ct) =>
        _db.LoanAccounts.FirstOrDefaultAsync(a => a.Id == id && a.Type == AccountType.Loan && a.ApplicationId != null, ct);
}

public sealed class AccountCreationRequestRepository : IAccountCreationRequestRepository
{
    private readonly FinnovaDbContext _db;
    public AccountCreationRequestRepository(FinnovaDbContext db) => _db = db;

    public Task AddAsync(AccountCreationRequest request, CancellationToken ct) => _db.AccountCreationRequests.AddAsync(request, ct).AsTask();
    public Task<AccountCreationRequest?> GetTrackedAsync(Guid id, CancellationToken ct) => _db.AccountCreationRequests.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<Dictionary<AccountRequestStatus, int>> CountByStatusAsync(CancellationToken ct)
    {
        var groups = await _db.AccountCreationRequests.AsNoTracking().GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(ct);
        return groups.ToDictionary(g => g.Status, g => g.Count);
    }

    public async Task<DateTime?> OldestNonCompletedAsync(CancellationToken ct)
    {
        var q = _db.AccountCreationRequests.AsNoTracking().Where(x => x.Status != AccountRequestStatus.Completed);
        return await q.AnyAsync(ct) ? await q.MinAsync(x => x.CreatedAtUtc, ct) : null;
    }

    public async Task<(List<AccountCreationRequest> Items, int Total)> GetDeadLettersAsync(int page, int pageSize, CancellationToken ct)
    {
        var q = _db.AccountCreationRequests.AsNoTracking().Where(x => x.Status == AccountRequestStatus.DeadLetter);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(x => x.UpdatedAtUtc).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }
}

public sealed class AccountKycJobRepository : IAccountKycJobRepository
{
    private readonly FinnovaDbContext _db;
    public AccountKycJobRepository(FinnovaDbContext db) => _db = db;
    public Task<AccountKycJob?> GetTrackedAsync(Guid accountId, CancellationToken ct) => _db.AccountKycJobs.FirstOrDefaultAsync(x => x.AccountId == accountId, ct);
    public Task AddAsync(AccountKycJob job, CancellationToken ct) => _db.AccountKycJobs.AddAsync(job, ct).AsTask();
}

public sealed class AccountKycHistoryRepository : IAccountKycHistoryRepository
{
    private readonly FinnovaDbContext _db;
    public AccountKycHistoryRepository(FinnovaDbContext db) => _db = db;
    public Task AddAsync(AccountKycHistoryEntry entry, CancellationToken ct) => _db.AccountKycHistoryEntries.AddAsync(entry, ct).AsTask();
}

public sealed class AccountKycResolutionRepository : IAccountKycResolutionRepository
{
    private readonly FinnovaDbContext _db;
    public AccountKycResolutionRepository(FinnovaDbContext db) => _db = db;
    public Task AddAsync(AccountKycResolutionRequest request, CancellationToken ct) => _db.AccountKycResolutionRequests.AddAsync(request, ct).AsTask();
    public Task<AccountKycResolutionRequest?> GetTrackedAsync(Guid id, CancellationToken ct) => _db.AccountKycResolutionRequests.FirstOrDefaultAsync(x => x.Id == id, ct);
    public Task<AccountKycResolutionRequest?> GetPendingAsync(Guid accountId, CancellationToken ct) =>
        _db.AccountKycResolutionRequests.AsNoTracking().FirstOrDefaultAsync(x => x.AccountId == accountId && x.Status == KycResolutionStatus.Pending, ct);
}

public sealed class AccountAuditRepository : IAccountAuditRepository
{
    private readonly FinnovaDbContext _db;
    public AccountAuditRepository(FinnovaDbContext db) => _db = db;
    public Task AddAsync(AccountAuditEntry entry, CancellationToken ct) => _db.AccountAuditEntries.AddAsync(entry, ct).AsTask();
}

public sealed class AppRoleRepository : IAppRoleRepository
{
    private readonly FinnovaDbContext _db;
    public AppRoleRepository(FinnovaDbContext db) => _db = db;
    public async Task<IReadOnlyCollection<string>> GetRoleNamesForUserAsync(Guid userAccountId, CancellationToken ct) =>
        await (from ur in _db.UserAccountRoles.AsNoTracking()
               join r in _db.AppRoles.AsNoTracking() on ur.AppRoleId equals r.Id
               where ur.UserAccountId == userAccountId
               select r.Name).ToListAsync(ct);
}

public sealed class NumberingReferenceRepository : INumberingReferenceRepository
{
    private readonly FinnovaDbContext _db;
    public NumberingReferenceRepository(FinnovaDbContext db) => _db = db;

    public async Task<string?> GetLobTokenAsync(Guid id, CancellationToken ct)
    {
        var name = await _db.LinesOfBusiness.AsNoTracking().Where(x => x.Id == id && x.IsActive).Select(x => x.LOB_Name).FirstOrDefaultAsync(ct);
        return name is null ? null : LobTokenFormatter.ToToken(name);
    }

    public Task<string?> GetBranchCodeAsync(Guid id, CancellationToken ct) =>
        _db.Locations.AsNoTracking().Where(x => x.Id == id && x.IsActive && x.Level == 5).Select(x => x.Code).FirstOrDefaultAsync(ct);
}
```

  - [ ] 5.4 MODIFY `Finnova.Repository/Interfaces/INumberingSchemeRepository.cs`
    - Replace the `ExistsByScopeAsync` declaration and add two members before the closing brace. The new `lineOfBusinessId` parameter is last so existing positional callers still compile.
    - _Requirements: 16.2, 16.8, 16.9_

```csharp
    /// <summary>Uniqueness check on (DocumentType, Scope, ScopeId, LineOfBusinessId) (R2.3/2.4, extended by R16.9).</summary>
    Task<bool> ExistsByScopeAsync(
        string documentType, NumberScope scope, Guid? scopeId, Guid? excludeId = null,
        CancellationToken ct = default, Guid? lineOfBusinessId = null);

    /// <summary>All ACTIVE schemes of a document type (input of the pure SchemeResolver).</summary>
    Task<List<NumberingScheme>> GetActiveByDocumentTypeAsync(string documentType, CancellationToken ct = default);

    /// <summary>Atomically issues the next number for a specific scheme (same semantics as IssueNextAsync). Null when the scheme does not exist.</summary>
    Task<(NumberingScheme Scheme, long SequenceValue)?> IssueNextByIdAsync(Guid schemeId, DateTime utcNow, CancellationToken ct = default);
```

  - [ ] 5.5 MODIFY `Finnova.Repository/Repositories/NumberingSchemeRepository.cs`
    - Add `using System.Linq.Expressions;`. Replace `ExistsByScopeAsync` and the whole `IssueNextAsync` method with the following (the admin path now only matches schemes without a LOB, so it never collides with LOB schemes).
    - _Requirements: 16.2, 16.7, 16.8, 16.9_

```csharp
    public async Task<bool> ExistsByScopeAsync(
        string documentType, NumberScope scope, Guid? scopeId, Guid? excludeId = null,
        CancellationToken ct = default, Guid? lineOfBusinessId = null)
    {
        var dt = documentType.Trim();
        return await DbSet.AsNoTracking().AnyAsync(
            x => x.DocumentType == dt && x.Scope == scope && x.ScopeId == scopeId
                 && x.LineOfBusinessId == lineOfBusinessId
                 && (excludeId == null || x.Id != excludeId), ct);
    }

    public Task<List<NumberingScheme>> GetActiveByDocumentTypeAsync(string documentType, CancellationToken ct = default)
    {
        var dt = documentType.Trim();
        return DbSet.AsNoTracking().Where(x => x.DocumentType == dt && x.IsActive).ToListAsync(ct);
    }

    public Task<(NumberingScheme Scheme, long SequenceValue)?> IssueNextAsync(
        string documentType, NumberScope scope, Guid? scopeId, DateTime utcNow, CancellationToken ct = default)
        => IssueAsync(x => x.DocumentType == documentType && x.Scope == scope && x.ScopeId == scopeId
                           && x.LineOfBusinessId == null, utcNow, ct);

    public Task<(NumberingScheme Scheme, long SequenceValue)?> IssueNextByIdAsync(
        Guid schemeId, DateTime utcNow, CancellationToken ct = default)
        => IssueAsync(x => x.Id == schemeId, utcNow, ct);

    private async Task<(NumberingScheme Scheme, long SequenceValue)?> IssueAsync(
        Expression<Func<NumberingScheme, bool>> predicate, DateTime utcNow, CancellationToken ct)
    {
        async Task<(NumberingScheme, long)?> Body()
        {
            // TRACKED load (not AsNoTracking) so the sequence update is persisted.
            var scheme = await DbSet.FirstOrDefaultAsync(predicate, ct);
            if (scheme is null) return null;                                        // R5.6 -> 404 in handler
            if (!scheme.IsActive) throw new NumberingSchemeInactiveException(scheme.Code); // R5.7

            var period = PeriodKey.For(scheme.ResetRule, utcNow);
            long next = scheme.PeriodKey == period && scheme.CurrentValue >= scheme.SeqStart
                ? scheme.CurrentValue + scheme.SeqIncrement                          // within period (R5.4)
                : scheme.SeqStart;                                                  // first issue or reset (R5.3)

            if (next > NumberFormatter.MaxValueForPadding(scheme.SeqPadding))
                throw new NumberSequenceExhaustedException(scheme.Code);             // R5.9

            scheme.CurrentValue = next;
            scheme.PeriodKey = period;
            scheme.UpdatedAt = utcNow;
            await Context.SaveChangesAsync(ct);
            return (scheme, next);
        }

        if (Context.Database.IsRelational())
        {
            var strategy = Context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await Context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                try
                {
                    var result = await Body();
                    await tx.CommitAsync(ct);
                    return result;
                }
                catch
                {
                    await tx.RollbackAsync(ct);
                    throw;
                }
            });
        }

        return await Body();
    }
```

  - [ ] 5.6 MODIFY `Finnova.Repository/DependencyInjection.cs`
    - Add before `return services;`.
    - _Requirements: 1.1, 8.1, 11.8, 18.1_

```csharp
        services.AddScoped<ILoanAccountRepository, LoanAccountRepository>();
        services.AddScoped<IAccountCreationRequestRepository, AccountCreationRequestRepository>();
        services.AddScoped<IAccountKycJobRepository, AccountKycJobRepository>();
        services.AddScoped<IAccountKycHistoryRepository, AccountKycHistoryRepository>();
        services.AddScoped<IAccountKycResolutionRepository, AccountKycResolutionRepository>();
        services.AddScoped<IAccountAuditRepository, AccountAuditRepository>();
        services.AddScoped<IAppRoleRepository, AppRoleRepository>();
        services.AddScoped<INumberingReferenceRepository, NumberingReferenceRepository>();
```

- [ ] 6. Backend pure and domain helpers
  - [ ] 6.1 CREATE `Finnova.Service/LoanAccounts/Rules/KycStatusDeriver.cs`
    - _Requirements: 5.4, 5.5, 5.6, 5.11, 5.12, 6.2, 7.4_

```csharp
using Finnova.Models.Domain.Enums;

namespace Finnova.Service.LoanAccounts.Rules;

/// <summary>Pure KYC derivation (Properties 1 and 2). Mirrored in the Python worker.</summary>
public static class KycStatusDeriver
{
    public static (KycStatus Status, bool Flagged) Derive(
        KycCheckResult panValidation, KycCheckResult panNameMatch,
        KycCheckResult aadhaarValidation, KycCheckResult aadhaarAddressMatch)
        => Derive(new[] { panValidation, panNameMatch, aadhaarValidation, aadhaarAddressMatch });

    public static (KycStatus Status, bool Flagged) Derive(IReadOnlyCollection<KycCheckResult> results)
    {
        KycStatus status =
            results.Count == 4 && results.All(r => r == KycCheckResult.Passed) ? KycStatus.Passed
            : results.Any(r => r == KycCheckResult.Failed) ? KycStatus.Failed
            : results.Any(r => r == KycCheckResult.Unavailable) ? KycStatus.Unavailable
            : KycStatus.PartiallyVerified;
        return (status, status != KycStatus.Passed);
    }

    /// <summary>Bypass mode: status Bypassed, all four checks Bypassed, flag true (R6.2).</summary>
    public static (KycStatus Status, bool Flagged) ForBypass() => (KycStatus.Bypassed, true);
}
```

  - [ ] 6.2 CREATE `Finnova.Service/LoanAccounts/Rules/AccountStatusRules.cs`
    - _Requirements: 8.13, 8.14, 8.15_

```csharp
using Finnova.Models.Domain.Enums;

namespace Finnova.Service.LoanAccounts.Rules;

public static class AccountStatusRules
{
    public static bool IsTransitionAllowed(AccountStatus from, AccountStatus to) =>
        (from, to) is (AccountStatus.Active, AccountStatus.Inactive)
                   or (AccountStatus.Active, AccountStatus.Frozen)
                   or (AccountStatus.Inactive, AccountStatus.Active)
                   or (AccountStatus.Frozen, AccountStatus.Active);

    /// <summary>A Closed account is never updatable (R8.15).</summary>
    public static bool IsUpdatable(AccountStatus status) => status != AccountStatus.Closed;
}
```

  - [ ] 6.3 CREATE `Finnova.Service/LoanAccounts/Rules/KycResolutionRules.cs`
    - _Requirements: 9.1 to 9.10, 9.13_

```csharp
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;

namespace Finnova.Service.LoanAccounts.Rules;

public enum SubmitCheck { Ok, NotFlagged, PendingExists, ReasonInvalid }
public enum DecisionCheck { Ok, NotPending, SameUser, CommentRequired }

/// <summary>Pure maker-checker rules shared by the handlers and the property tests (Properties 13, 18, 19).</summary>
public static class KycResolutionRules
{
    public const int ReasonMin = 10;
    public const int ReasonMax = 500;

    public static SubmitCheck CheckSubmit(bool flagged, bool hasPending, string? reason)
    {
        var r = reason?.Trim() ?? string.Empty;
        if (!flagged) return SubmitCheck.NotFlagged;
        if (hasPending) return SubmitCheck.PendingExists;
        if (r.Length < ReasonMin || r.Length > ReasonMax) return SubmitCheck.ReasonInvalid;
        return SubmitCheck.Ok;
    }

    public static DecisionCheck CheckDecision(KycResolutionStatus status, Guid makerId, Guid checkerId, string? comment)
    {
        if (status != KycResolutionStatus.Pending) return DecisionCheck.NotPending;
        if (makerId == checkerId) return DecisionCheck.SameUser;
        if (string.IsNullOrWhiteSpace(comment)) return DecisionCheck.CommentRequired;
        return DecisionCheck.Ok;
    }

    /// <summary>Approval is the only path that turns an Account's KYC into Passed/Manual (R9.2).</summary>
    public static void ApplyApproval(LoanAccount account, DateTime utcNow)
    {
        account.KycStatus = KycStatus.Passed;
        account.KycFlagged = false;
        account.KycSource = KycSource.Manual;
        account.KycCheckedAtUtc = utcNow;
        account.UpdatedAt = utcNow;
    }
}
```

  - [ ] 6.4 CREATE `Finnova.Service/LoanAccounts/Rules/PiiMasker.cs`
    - _Requirements: 8.11_

```csharp
namespace Finnova.Service.LoanAccounts.Rules;

/// <summary>Masks sensitive identifiers. Same length as the input; reveals at most the last four characters (Property 12).</summary>
public static class PiiMasker
{
    public static string Mask(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Length <= 4) return new string('*', value.Length);
        return new string('*', value.Length - 4) + value[^4..];
    }
}
```

  - [ ] 6.5 CREATE `Finnova.Service/LoanAccounts/Rules/RequestRecoveryRules.cs`
    - _Requirements: 10.3, 10.4_

```csharp
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;

namespace Finnova.Service.LoanAccounts.Rules;

public static class RequestRecoveryRules
{
    public static bool CanReprocess(AccountRequestStatus status) => status == AccountRequestStatus.DeadLetter;

    public static void ResetForReprocess(AccountCreationRequest request, DateTime utcNow)
    {
        request.Status = AccountRequestStatus.Pending;
        request.AttemptCount = 0;
        request.NextAttemptAtUtc = null;
        request.ClaimedBy = null;
        request.LeaseExpiresAtUtc = null;
        request.UpdatedAtUtc = utcNow;
    }
}
```

  - [ ] 6.6 CREATE `Finnova.Service/LoanAccounts/AccountCreationKycOptions.cs`
    - _Requirements: 5.13 (assumption A26)_

```csharp
namespace Finnova.Service.LoanAccounts;

/// <summary>Bound from "AccountCreation:Kyc". Document codes are assumed values (A26) to be confirmed against the Document Master.</summary>
public sealed class AccountCreationKycOptions
{
    public const string SectionName = "AccountCreation:Kyc";
    public string PanDocumentCode { get; set; } = "PAN";
    public string AadhaarDocumentCode { get; set; } = "AADHAAR";
}
```

  - [ ] 6.7 CREATE `Finnova.Service/DocumentNumberControl/Internal/SchemeResolver.cs`
    - Pure fallback resolver (Property 6). Levels: LOB+Branch, LOB, Branch, Global. `Company` scope is ignored.
    - _Requirements: 4.2, 16.8_

```csharp
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;

namespace Finnova.Service.DocumentNumberControl.Internal;

public enum SchemeScopeLevel { LobAndBranch, Lob, Branch, Global }

public sealed record SchemeResolution(NumberingScheme Scheme, SchemeScopeLevel Level);

public static class SchemeResolver
{
    public static SchemeResolution? Resolve(IEnumerable<NumberingScheme> schemes, string documentType, Guid? lobId, Guid? branchId)
    {
        var dt = documentType?.Trim() ?? string.Empty;
        var active = schemes
            .Where(s => s.IsActive && string.Equals(s.DocumentType?.Trim(), dt, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (lobId is not null && branchId is not null)
        {
            var s = active.FirstOrDefault(x => x.Scope == NumberScope.Branch && x.ScopeId == branchId && x.LineOfBusinessId == lobId);
            if (s is not null) return new SchemeResolution(s, SchemeScopeLevel.LobAndBranch);
        }
        if (lobId is not null)
        {
            var s = active.FirstOrDefault(x => x.Scope == NumberScope.Global && x.ScopeId == null && x.LineOfBusinessId == lobId);
            if (s is not null) return new SchemeResolution(s, SchemeScopeLevel.Lob);
        }
        if (branchId is not null)
        {
            var s = active.FirstOrDefault(x => x.Scope == NumberScope.Branch && x.ScopeId == branchId && x.LineOfBusinessId == null);
            if (s is not null) return new SchemeResolution(s, SchemeScopeLevel.Branch);
        }
        var g = active.FirstOrDefault(x => x.Scope == NumberScope.Global && x.ScopeId == null && x.LineOfBusinessId == null);
        return g is null ? null : new SchemeResolution(g, SchemeScopeLevel.Global);
    }
}
```

  - [ ] 6.8 CREATE `Finnova.Service/IdentityVerification/Mapping/MappingProfileRegistry.cs`
    - Customer Master mapping profile (Property 20) and the registry the comparison resolver consults.
    - _Requirements: 17.1 to 17.6_

```csharp
using Finnova.Models.Contracts.Internal;

namespace Finnova.Service.IdentityVerification.Mapping;

/// <summary>Maps Customer Master name and address parts to verification values. Deterministic; never carries PAN or Aadhaar.</summary>
public static class CustomerMasterMappingProfile
{
    public const string Name = "customer-master";

    public static MappedVerificationValues Map(CustomerNameParts name, CustomerAddressParts? address)
    {
        var mappedName = Join(" ", name.First, name.Second, name.Third, name.Fourth, name.Fifth, name.Sixth);
        var mappedAddress = address is null
            ? string.Empty
            : Join(", ", address.House, address.Block, address.Way, address.Area, address.Landmark,
                         address.City, address.State, address.PostalCode, address.Country);
        return new MappedVerificationValues(mappedName, mappedAddress);
    }

    private static string Join(string separator, params string?[] parts) =>
        string.Join(separator, parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));
}

/// <summary>Code-level registry of mapping profile names. Existing configured profiles are unaffected (R17.5).</summary>
public static class MappingProfileRegistry
{
    private static readonly HashSet<string> Names = new(StringComparer.Ordinal) { CustomerMasterMappingProfile.Name };

    public static IReadOnlyCollection<string> RegisteredNames => Names;

    public static bool Contains(string? name) => !string.IsNullOrWhiteSpace(name) && Names.Contains(name.Trim());
}
```

  - [ ] 6.9 CREATE `Finnova.Service/LoanAccounts/Kyc/KycResultMapper.cs`
    - Maps identity-verification results to KYC check results (`NoUsableMatch` becomes `NotChecked`, design D11).
    - _Requirements: 5.3, 5.4_

```csharp
using Finnova.Models.Domain.Enums;

namespace Finnova.Service.LoanAccounts.Kyc;

public static class KycResultMapper
{
    public static KycCheckResult FromValidation(ValidationStatus? status) => status switch
    {
        ValidationStatus.Success => KycCheckResult.Passed,
        ValidationStatus.Failure => KycCheckResult.Failed,
        ValidationStatus.Unavailable => KycCheckResult.Unavailable,
        _ => KycCheckResult.NotChecked
    };

    public static KycCheckResult FromComparison(ComparisonOutcome? outcome) => outcome switch
    {
        ComparisonOutcome.Match => KycCheckResult.Passed,
        ComparisonOutcome.NonMatch => KycCheckResult.Failed,
        _ => KycCheckResult.NotChecked   // NoUsableMatch or not run
    };
}
```

- [ ] 7. Role claims and mapping registry wiring
  - [ ] 7.1 MODIFY `Finnova.Service/Auth/ITokenService.cs`
    - Add an overload (the original member stays, so existing Moq setups keep compiling).
    - _Requirements: 18.4_

```csharp
    /// <summary>Same as <see cref="GenerateToken(UserAccount)"/> but also emits one role claim per assigned catalog role.</summary>
    (string Token, int ExpiresInSeconds) GenerateToken(UserAccount user, IReadOnlyCollection<string> extraRoles);
```

  - [ ] 7.2 MODIFY `Finnova.Service/Auth/TokenService.cs`
    - Replace the method signature line `public (string Token, int ExpiresInSeconds) GenerateToken(UserAccount user)` with the two lines below, and add the `foreach` after the `SystemAdmin` block.
    - _Requirements: 18.4_

```csharp
    public (string Token, int ExpiresInSeconds) GenerateToken(UserAccount user)
        => GenerateToken(user, Array.Empty<string>());

    public (string Token, int ExpiresInSeconds) GenerateToken(UserAccount user, IReadOnlyCollection<string> extraRoles)
```

    Insert directly after the closing brace of `if (user.Role == Finnova.Models.Domain.Enums.UserRole.Admin) { ... }`:

```csharp
        // Roles assigned through the app_roles catalog (application-account-creation R18).
        foreach (var role in extraRoles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }
```

  - [ ] 7.3 MODIFY `Finnova.Service/Auth/Commands/Login/LoginCommandHandler.cs` (replace the whole file)
    - The role repository is an optional constructor argument so existing tests that build the handler with two arguments still compile.
    - _Requirements: 18.4_

```csharp
using MediatR;
using Finnova.Models.Contracts.Auth;
using Finnova.Repository.Interfaces;

namespace Finnova.Service.Auth.Commands.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResponse?>
{
    private readonly IUserManagementRepository _userRepository;
    private readonly ITokenService _tokenService;
    private readonly IAppRoleRepository? _roles;

    public LoginCommandHandler(IUserManagementRepository userRepository, ITokenService tokenService, IAppRoleRepository? roles = null)
    {
        _userRepository = userRepository;
        _tokenService = tokenService;
        _roles = roles;
    }

    public async Task<LoginResponse?> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByUserNameAsync(request.UserName, cancellationToken);

        // Return null on any failure - the controller maps this to 401 without
        // revealing whether the username exists or the password was wrong.
        if (user is null) return null;
        if (!user.IsActive) return null;
        if (!PasswordHasher.Verify(request.Password, user.PasswordHash)) return null;

        var extraRoles = _roles is null
            ? Array.Empty<string>()
            : (await _roles.GetRoleNamesForUserAsync(user.Id, cancellationToken)).ToArray();

        var (token, expiresIn) = extraRoles.Length == 0
            ? _tokenService.GenerateToken(user)
            : _tokenService.GenerateToken(user, extraRoles);

        return new LoginResponse(
            Token: token,
            RefreshToken: null,
            User: user.ToAuthUser(),
            ExpiresIn: expiresIn);
    }
}
```

  - [ ] 7.4 MODIFY `Finnova.SystemAdminService/Program.cs` (comparison resolver)
    - Add `using Finnova.Service.IdentityVerification.Mapping;` at the top. In `ConfiguredComparisonAdapterResolver.Resolve`, replace the `if (!string.IsNullOrWhiteSpace(requestedName) && ...)` block with this version, which also accepts registered profile names and leaves configured names and profiles working.
    - _Requirements: 17.1, 17.5_

```csharp
        var requestedName = adapterName?.Trim();
        var configuredProfile = provider.InputMapping.MappingProfile?.Trim();
        if (!string.IsNullOrWhiteSpace(requestedName) &&
            !string.Equals(requestedName, provider.AdapterName, StringComparison.Ordinal) &&
            !string.Equals(requestedName, configuredProfile, StringComparison.Ordinal) &&
            !MappingProfileRegistry.Contains(requestedName))
        {
            throw new IdentityVerificationConfigurationException();
        }
```

- [ ] 8. Backend DNC extension (LOB and Branch numbering)
  - [ ] 8.1 MODIFY `Finnova.Models/Domain/Numbering/NumberFormatter.cs`
    - Accept `{LOB}` and `{BRANCH}`, render them, validate scope consistency, and compute the maximum rendered length.
    - _Requirements: 16.3, 16.4, 16.5, 16.6, 4.9_

    (a) In `Validate`, replace the case list `case "PREFIX": case "SUFFIX": case "YYYY": case "YY": case "MM": case "DD": break;` with:

```csharp
                case "PREFIX":
                case "SUFFIX":
                case "YYYY":
                case "YY":
                case "MM":
                case "DD":
                case "LOB":
                case "BRANCH":
                    break;
```

    (b) Replace the `Format` signature and its switch arms for `"PREFIX"`/`"SUFFIX"`:

```csharp
    public static string Format(
        string template, long sequenceValue, int defaultPadding,
        string? prefix, string? suffix, DateTime utcNow,
        string? lobCode = null, string? branchCode = null)
    {
        return TokenRegex().Replace(template, m =>
        {
            var inner = m.Groups[1].Value;
            return inner switch
            {
                "PREFIX" => prefix ?? string.Empty,
                "SUFFIX" => suffix ?? string.Empty,
                "LOB" => lobCode ?? string.Empty,
                "BRANCH" => branchCode ?? string.Empty,
```

    (keep the remaining arms `"YYYY"`, `"YY"`, `"MM"`, `"DD"`, `"SEQ"`, the `SEQ:` arm, and the default arm exactly as they are).

    (c) Add these two methods inside the class (for example before `MaxValueForPadding`):

```csharp
    /// <summary>Rejects {LOB} without a LOB on the scope and {BRANCH} without Branch scope (R16.6).</summary>
    public static IReadOnlyList<string> ValidateScopeTokens(string template, bool hasLineOfBusiness, bool scopeIsBranch)
    {
        var errors = new List<string>();
        var tokens = TokenRegex().Matches(template ?? string.Empty).Select(m => m.Groups[1].Value).ToList();
        if (tokens.Contains("LOB") && !hasLineOfBusiness)
            errors.Add("Token '{LOB}' requires a Line of Business on the scheme scope.");
        if (tokens.Contains("BRANCH") && !scopeIsBranch)
            errors.Add("Token '{BRANCH}' requires Branch scope.");
        return errors;
    }

    /// <summary>Length of the longest number the template can render (max sequence, max LOB and Branch token lengths).</summary>
    public static int MaxRenderedLength(string template, int defaultPadding, string? prefix, string? suffix)
        => Format(template, MaxValueForPadding(Math.Min(defaultPadding, MaxSeqWidth)), defaultPadding, prefix, suffix,
                  new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                  new string('X', LobTokenFormatter.MaxLength), new string('X', 20)).Length;
```

  - [ ] 8.2 MODIFY `Finnova.Service/DocumentNumberControl/Commands/IssueNumber/IssueNumberCommand.cs` (replace the whole file)
    - _Requirements: 4.1, 16.8_

```csharp
using Finnova.Models.Contracts.DocumentNumberControl;
using Finnova.Models.Domain.Enums;
using MediatR;

namespace Finnova.Service.DocumentNumberControl.Commands.IssueNumber;

/// <summary>
/// Issues the next number (R5). Not audited (R6.4). With <paramref name="UseFallback"/> the scheme is resolved
/// server-side by the pure SchemeResolver from the LOB and Branch (application-account-creation R16.8); the
/// admin endpoint keeps the exact-scope behavior.
/// </summary>
public record IssueNumberCommand(
    string DocumentType, NumberScope Scope, Guid? ScopeId, string ActingAdmin,
    Guid? LineOfBusinessId = null, Guid? BranchId = null, bool UseFallback = false)
    : IRequest<IssuedNumberResponse>;
```

  - [ ] 8.3 MODIFY `Finnova.Service/DocumentNumberControl/Commands/IssueNumber/IssueNumberCommandHandler.cs` (replace the whole file)
    - The reference repository is optional so `new IssueNumberCommandHandler(repo)` in existing tests still compiles.
    - _Requirements: 4.2, 4.5, 16.3, 16.4, 16.8, 16.12_

```csharp
using Finnova.Models.Contracts.DocumentNumberControl;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Exceptions;
using Finnova.Models.Domain.Numbering;
using Finnova.Repository.Interfaces;
using Finnova.Service.DocumentNumberControl.Internal;
using MediatR;

namespace Finnova.Service.DocumentNumberControl.Commands.IssueNumber;

/// <summary>
/// Handles number issuance (R5). Delegates the atomic sequence advance to the repository, then
/// formats the number from the scheme's template. Not-found scheme -> 404 (R5.6). No audit (R6.4).
/// </summary>
public class IssueNumberCommandHandler : IRequestHandler<IssueNumberCommand, IssuedNumberResponse>
{
    private readonly INumberingSchemeRepository _repository;
    private readonly INumberingReferenceRepository? _references;

    public IssueNumberCommandHandler(INumberingSchemeRepository repository, INumberingReferenceRepository? references = null)
    {
        _repository = repository;
        _references = references;
    }

    public async Task<IssuedNumberResponse> Handle(IssueNumberCommand request, CancellationToken cancellationToken)
    {
        var documentType = request.DocumentType.Trim();
        var issuedAt = DateTime.UtcNow;
        string? lobCode = null, branchCode = null, level = null;
        (NumberingScheme Scheme, long SequenceValue)? result;

        if (request.UseFallback)
        {
            if (_references is null) throw new InvalidOperationException("The reference repository is required for fallback issuance.");
            if (request.LineOfBusinessId is not { } lobId || request.BranchId is not { } branchId)
                throw new NumberingSchemeValidationException("Line of Business and Branch are required.");

            // R16.12: unknown LOB or Branch is rejected before any sequence moves.
            lobCode = await _references.GetLobTokenAsync(lobId, cancellationToken)
                ?? throw new NumberingSchemeValidationException("The Line of Business does not exist or is inactive.");
            branchCode = await _references.GetBranchCodeAsync(branchId, cancellationToken)
                ?? throw new NumberingSchemeValidationException("The Branch does not exist or is inactive.");

            var candidates = await _repository.GetActiveByDocumentTypeAsync(documentType, cancellationToken);
            var resolution = SchemeResolver.Resolve(candidates, documentType, lobId, branchId)
                ?? throw new NumberingSchemeNotFoundException(documentType);   // no active scheme: no sequence advanced
            level = resolution.Level.ToString();
            result = await _repository.IssueNextByIdAsync(resolution.Scheme.Id, issuedAt, cancellationToken)
                ?? throw new NumberingSchemeNotFoundException(documentType);
        }
        else
        {
            // Repository advances the sequence atomically; throws Inactive/Exhausted; null => not found.
            result = await _repository.IssueNextAsync(documentType, request.Scope, request.ScopeId, issuedAt, cancellationToken)
                ?? throw new NumberingSchemeNotFoundException(documentType);   // R5.6
        }

        var (scheme, sequenceValue) = result.Value;
        var number = NumberFormatter.Format(
            scheme.FormatTemplate, sequenceValue, scheme.SeqPadding, scheme.Prefix, scheme.Suffix, issuedAt, lobCode, branchCode); // R5.5

        return new IssuedNumberResponse(scheme.Id, scheme.DocumentType, number, sequenceValue, issuedAt, scheme.Code, level);
    }
}
```

  - [ ] 8.4 MODIFY `Finnova.Service/DocumentNumberControl/Commands/CreateNumberingScheme/CreateNumberingSchemeCommand.cs`
    - Replace the last parameter line `string ActingAdmin) : IRequest<NumberingSchemeResponse>;` with:
    - _Requirements: 16.1_

```csharp
    string ActingAdmin,
    Guid? LineOfBusinessId = null) : IRequest<NumberingSchemeResponse>;
```

  - [ ] 8.5 MODIFY `Finnova.Service/DocumentNumberControl/Commands/CreateNumberingScheme/CreateNumberingSchemeCommandHandler.cs`
    - (a) Insert after the `templateErrors` check (before the uniqueness checks):
    - _Requirements: 16.5, 16.6, 16.9, 4.9_

```csharp
        // LOB/BRANCH token and scope consistency (R16.5, R16.6) and the LoanAccount length ceiling (design D1).
        var tokenErrors = NumberFormatter.ValidateScopeTokens(
            request.FormatTemplate, request.LineOfBusinessId is not null, scope == NumberScope.Branch);
        if (tokenErrors.Count > 0)
            throw new NumberingSchemeValidationException(string.Join(" ", tokenErrors));
        if (string.Equals(documentType, "LoanAccount", StringComparison.OrdinalIgnoreCase)
            && NumberFormatter.MaxRenderedLength(request.FormatTemplate, request.SeqPadding ?? 1, request.Prefix, request.Suffix) > 100)
            throw new NumberingSchemeValidationException("The longest possible LoanAccount number exceeds 100 characters.");
```

    (b) Replace the scope uniqueness call with:

```csharp
        if (await _repository.ExistsByScopeAsync(documentType, scope, request.ScopeId, null, cancellationToken, request.LineOfBusinessId))
            throw new NumberingSchemeScopeConflictException();
```

    (c) In the `new NumberingScheme { ... }` initializer add after `ScopeId = request.ScopeId,`:

```csharp
            LineOfBusinessId = request.LineOfBusinessId,
```

  - [ ] 8.6 MODIFY `Finnova.Service/DocumentNumberControl/Commands/UpdateNumberingScheme/UpdateNumberingSchemeCommandHandler.cs`
    - Insert directly after the existing `templateErrors` check.
    - _Requirements: 16.6, 4.9_

```csharp
        var tokenErrors = NumberFormatter.ValidateScopeTokens(
            newTemplate, entity.LineOfBusinessId is not null, entity.Scope == NumberScope.Branch);
        if (tokenErrors.Count > 0)
            throw new NumberingSchemeValidationException(string.Join(" ", tokenErrors));
        if (string.Equals(entity.DocumentType, "LoanAccount", StringComparison.OrdinalIgnoreCase)
            && NumberFormatter.MaxRenderedLength(newTemplate, request.SeqPadding, request.Prefix, request.Suffix) > 100)
            throw new NumberingSchemeValidationException("The longest possible LoanAccount number exceeds 100 characters.");
```

  - [ ] 8.7 MODIFY `Finnova.Service/Mappers/NumberingSchemeMapper.cs`
    - In `ToResponse(this NumberingScheme x)` add the last argument after `x.UpdatedAt`:
    - _Requirements: 16.1_

```csharp
        x.UpdatedAt,
        x.LineOfBusinessId);
```

  - [ ] 8.8 MODIFY `Finnova.SystemAdminService/Controllers/DocumentNumberControlController.cs`
    - In `Create`, replace `GetActingAdmin()));` (the last argument of `new CreateNumberingSchemeCommand(...)`) with:
    - _Requirements: 16.1_

```csharp
            GetActingAdmin(), r.LineOfBusinessId));
```

  - [ ] 8.9 MODIFY `Finnova.Tests/Infrastructure/InMemoryNumberingSchemeRepository.cs`
    - Required so `Finnova.Tests` still compiles against the extended interface.
    - (a) In `Clone`, add after `ScopeId = x.ScopeId,`: `LineOfBusinessId = x.LineOfBusinessId,`
    - (b) Replace `ExistsByScopeAsync` and the predicate of `IssueNextAsync`, and add the two new members:
    - _Requirements: 16.2, 16.9_

```csharp
    public Task<bool> ExistsByScopeAsync(
        string documentType, NumberScope scope, Guid? scopeId, Guid? excludeId = null,
        CancellationToken ct = default, Guid? lineOfBusinessId = null)
    {
        var dt = documentType.Trim();
        var exists = _store.Any(x =>
            string.Equals(x.DocumentType, dt, StringComparison.OrdinalIgnoreCase) &&
            x.Scope == scope && x.ScopeId == scopeId && x.LineOfBusinessId == lineOfBusinessId &&
            (excludeId == null || x.Id != excludeId));
        return Task.FromResult(exists);
    }

    public Task<List<NumberingScheme>> GetActiveByDocumentTypeAsync(string documentType, CancellationToken ct = default)
        => Task.FromResult(_store
            .Where(x => x.IsActive && string.Equals(x.DocumentType, documentType.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(Clone).ToList());

    public Task<(NumberingScheme Scheme, long SequenceValue)?> IssueNextByIdAsync(Guid schemeId, DateTime utcNow, CancellationToken ct = default)
    {
        var scheme = _store.FirstOrDefault(x => x.Id == schemeId);
        if (scheme is null) return Task.FromResult<(NumberingScheme, long)?>(null);
        if (!scheme.IsActive) throw new NumberingSchemeInactiveException(scheme.Code);
        var period = PeriodKey.For(scheme.ResetRule, utcNow);
        long next = scheme.PeriodKey == period && scheme.CurrentValue >= scheme.SeqStart
            ? scheme.CurrentValue + scheme.SeqIncrement
            : scheme.SeqStart;
        if (next > NumberFormatter.MaxValueForPadding(scheme.SeqPadding))
            throw new NumberSequenceExhaustedException(scheme.Code);
        scheme.CurrentValue = next;
        scheme.PeriodKey = period;
        scheme.UpdatedAt = utcNow;
        return Task.FromResult<(NumberingScheme, long)?>((Clone(scheme), next));
    }
```

    In `IssueNextAsync`, change the `FirstOrDefault` predicate's last line from `x.Scope == scope && x.ScopeId == scopeId);` to:

```csharp
            x.Scope == scope && x.ScopeId == scopeId && x.LineOfBusinessId == null);
```

- [ ] 9. Backend outbox: enqueue Account Creation on Process
  - [ ] 9.1 MODIFY `Finnova.Service/Applications/Commands/ApplicationCommands.cs` (`ProcessApplicationCommandHandler` only)
    - Replace the field line and constructor of `ProcessApplicationCommandHandler` (the two lines starting `private readonly IApplicationUnitOfWork _uow; private readonly IApplicationReferenceValidator _references; private readonly AmortizationCalculator _calculator;` through the end of the constructor) with:
    - _Requirements: 1.1, 1.2, 1.3, 1.5, 1.6_

```csharp
    private readonly IApplicationUnitOfWork _uow; private readonly IApplicationReferenceValidator _references; private readonly AmortizationCalculator _calculator;
    private readonly IAccountCreationRequestRepository _requests;
    public ProcessApplicationCommandHandler(IApplicationRepository apps, IApplicationScheduleRepository schedules, IApplicationStatusAuditRepository audit,
        IApplicationUnitOfWork uow, IApplicationReferenceValidator references, AmortizationCalculator calculator, IAccountCreationRequestRepository requests)
        => (_apps, _schedules, _audit, _uow, _references, _calculator, _requests) = (apps, schedules, audit, uow, references, calculator, requests);
```

    Then replace this single line in the same handler's `Handle` method:

```csharp
        await _uow.SaveChangesAsync(ct); await ((dynamic)transaction).CommitAsync(ct);
```

    with:

```csharp
        // Transactional outbox: the Account Creation Request commits (or rolls back) with the Processed status (R1.1, R1.3).
        // The unique index on ApplicationId rejects a second request for the same Application (R1.4).
        await _requests.AddAsync(new AccountCreationRequest
        {
            ApplicationId = a.Id,
            ApplicationNumber = a.ApplicationNumber,
            CorrelationId = Guid.NewGuid().ToString("N"),
            InitiatedByUserId = Guid.TryParse(r.ActingUser, out var initiator) ? initiator : null,
            Status = AccountRequestStatus.Pending,
            AttemptCount = 0
        }, ct);
        await _uow.SaveChangesAsync(ct); await ((dynamic)transaction).CommitAsync(ct);
```

    Draft and Submitted applications never reach this handler path, so no request is created for them (R1.5). The API does not create the Account (R1.6).

- [ ] 10. Backend loan-account CQRS slice (`Finnova.Service`)
  - [ ] 10.1 CREATE `Finnova.Service/Mappers/LoanAccountMapper.cs`
    - _Requirements: 8.1, 8.9, 8.11_

```csharp
using Finnova.Models.Contracts.LoanAccounts;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Interfaces;
using Finnova.Service.LoanAccounts.Rules;

namespace Finnova.Service.Mappers;

public static class LoanAccountMapper
{
    public static LoanAccountListItemResponse ToListItem(this LoanAccountRow row, CustomerContact? contact)
    {
        var a = row.Account;
        return new LoanAccountListItemResponse(
            a.Id, row.ApplicationNumber, a.AccountNumber, a.Status.ToString(), row.CustomerCode,
            a.CustomerNameSnapshot ?? string.Empty,
            contact?.Mobile is null ? null : PiiMasker.Mask(contact.Mobile), contact?.City,
            (a.KycStatus ?? KycStatus.Pending).ToString(), a.KycFlagged,
            a.KycSource?.ToString(), a.KycSource?.ToString(), a.KycCheckedAtUtc,
            row.HasPendingResolution, a.OpenedAt);
    }

    public static KycCheckResultsResponse ToChecks(this LoanAccount a) => new(
        (a.KycPanValidation ?? KycCheckResult.NotChecked).ToString(),
        (a.KycPanNameMatch ?? KycCheckResult.NotChecked).ToString(),
        (a.KycAadhaarValidation ?? KycCheckResult.NotChecked).ToString(),
        (a.KycAadhaarAddressMatch ?? KycCheckResult.NotChecked).ToString());

    public static KycResolutionRequestResponse ToResponse(this AccountKycResolutionRequest r) => new(
        r.Id, r.AccountId, r.Status.ToString(), r.MakerUserId, r.MakerName, r.Reason, r.PreviousKycStatus.ToString(),
        r.SubmittedAtUtc, r.CheckerUserId, r.CheckerName, r.DecisionComment, r.DecidedAtUtc);

    public static KycHistoryEntryResponse ToResponse(this AccountKycHistoryEntry h) => new(
        h.Source.ToString(), h.KycStatus.ToString(),
        new KycCheckResultsResponse(h.PanValidation.ToString(), h.PanNameMatch.ToString(), h.AadhaarValidation.ToString(), h.AadhaarAddressMatch.ToString()),
        h.Reason, h.PreviousStatus?.ToString(), h.Actor, h.OccurredAtUtc);

    public static LoanAccountResponse ToResponse(this LoanAccountViewData v)
    {
        var a = v.Account;
        var pending = v.Resolutions.FirstOrDefault(r => r.Status == KycResolutionStatus.Pending);
        return new LoanAccountResponse(
            a.Id, a.ApplicationId ?? Guid.Empty, v.ApplicationNumber, a.AccountNumber, a.AccountName, a.Status.ToString(),
            a.CustomerId ?? Guid.Empty, v.Customer.CustomerCode, v.Customer.Name,
            v.Customer.Mobile is null ? null : PiiMasker.Mask(v.Customer.Mobile), v.Customer.City,
            v.Pan is null ? null : PiiMasker.Mask(v.Pan), v.Aadhaar is null ? null : PiiMasker.Mask(v.Aadhaar),
            a.LineOfBusinessId ?? Guid.Empty, a.BranchId ?? Guid.Empty, a.ProductCode ?? string.Empty, a.SchemeCode ?? string.Empty,
            v.Finance.FinanceAmount, v.Finance.IrrPercent, v.Finance.Tenure, v.Finance.RepaymentFrequency, v.Finance.DisbursementDate,
            (a.KycStatus ?? KycStatus.Pending).ToString(), a.KycFlagged, a.KycSource?.ToString(), a.KycCheckedAtUtc,
            a.ToChecks(), a.KycVerificationSessionId,
            v.History.Select(h => h.ToResponse()).ToList(),
            pending?.ToResponse(),
            v.Resolutions.Select(r => r.ToResponse()).ToList(),
            a.CreationNotes, a.OpenedAt, a.CreatedAt, a.UpdatedAt, Convert.ToBase64String(a.RowVersion ?? Array.Empty<byte>()));
    }
}
```

  - [ ] 10.2 CREATE `Finnova.Service/LoanAccounts/LoanAccountViewLoader.cs`
    - Static helper (no DI registration needed, so other hosts that scan this assembly still build).
    - _Requirements: 8.9, 8.10_

```csharp
using Finnova.Models.Contracts.LoanAccounts;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;

namespace Finnova.Service.LoanAccounts;

public static class LoanAccountViewLoader
{
    public static async Task<LoanAccountResponse> LoadAsync(
        ILoanAccountRepository accounts, AccountCreationKycOptions options, Guid id, CancellationToken ct)
    {
        var data = await accounts.GetViewDataAsync(id, options.PanDocumentCode, options.AadhaarDocumentCode, ct)
                   ?? throw LoanAccountNotFoundException.ForAccount(id);
        return data.ToResponse();
    }

    public static AccountAuditEntryBuilder Audit => new();
}

public sealed class AccountAuditEntryBuilder
{
    public Finnova.Models.Domain.Entities.AccountAuditEntry User(
        Guid? accountId, Guid? requestId, string action, string actor, string correlationId, string summary) => new()
    {
        AccountId = accountId, RequestId = requestId, Action = action, Source = "User",
        Actor = actor, CorrelationId = correlationId, ChangeSummary = summary, OccurredAtUtc = DateTime.UtcNow
    };
}
```

  - [ ] 10.3 CREATE `Finnova.Service/LoanAccounts/Queries/LoanAccountQueries.cs`
    - _Requirements: 8.1 to 8.10, 10.1, 10.2_

```csharp
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;
using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.LoanAccounts;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;

namespace Finnova.Service.LoanAccounts.Queries;

public sealed record GetLoanAccountsQuery(LoanAccountSearchRequest Filters) : IRequest<PaginatedResponse<LoanAccountListItemResponse>>;
public sealed record GetLoanAccountByIdQuery(Guid Id) : IRequest<LoanAccountResponse>;
public sealed record GetAccountQueueStatusQuery : IRequest<AccountQueueStatusResponse>;
public sealed record GetAccountDeadLettersQuery(int Page, int PageSize) : IRequest<PaginatedResponse<DeadLetterItemResponse>>;

public sealed class GetLoanAccountsQueryValidator : AbstractValidator<GetLoanAccountsQuery>
{
    public GetLoanAccountsQueryValidator()
    {
        RuleFor(x => x.Filters.Search).MaximumLength(100).WithMessage("Search term must not exceed 100 characters.");
        RuleFor(x => x.Filters.Page).GreaterThanOrEqualTo(1).WithMessage("Page must be 1 or greater.");
        RuleFor(x => x.Filters.PageSize).InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");
    }
}

public sealed class GetAccountDeadLettersQueryValidator : AbstractValidator<GetAccountDeadLettersQuery>
{
    public GetAccountDeadLettersQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class GetLoanAccountsQueryHandler : IRequestHandler<GetLoanAccountsQuery, PaginatedResponse<LoanAccountListItemResponse>>
{
    private readonly ILoanAccountRepository _accounts;
    public GetLoanAccountsQueryHandler(ILoanAccountRepository accounts) => _accounts = accounts;

    public async Task<PaginatedResponse<LoanAccountListItemResponse>> Handle(GetLoanAccountsQuery q, CancellationToken ct)
    {
        var f = q.Filters;
        var (rows, total) = await _accounts.SearchAsync(new LoanAccountFilters(
            f.Search, f.KycStatus, f.KycFlagged, f.AccountStatus, f.LineOfBusinessId, f.BranchId,
            f.ProductCode, f.SchemeCode, f.HasPendingKycResolution, f.Page, f.PageSize), ct);
        var contacts = await _accounts.GetContactsAsync(
            rows.Where(r => r.Account.CustomerId.HasValue).Select(r => r.Account.CustomerId!.Value).Distinct().ToList(), ct);
        var items = rows.Select(r => r.ToListItem(
            r.Account.CustomerId is { } cid && contacts.TryGetValue(cid, out var c) ? c : null)).ToList();
        return new PaginatedResponse<LoanAccountListItemResponse>(
            items, total, f.Page, f.PageSize, (int)Math.Ceiling(total / (double)f.PageSize));
    }
}

public sealed class GetLoanAccountByIdQueryHandler : IRequestHandler<GetLoanAccountByIdQuery, LoanAccountResponse>
{
    private readonly ILoanAccountRepository _accounts;
    private readonly AccountCreationKycOptions _options;
    public GetLoanAccountByIdQueryHandler(ILoanAccountRepository accounts, IOptions<AccountCreationKycOptions> options)
        => (_accounts, _options) = (accounts, options.Value);

    public Task<LoanAccountResponse> Handle(GetLoanAccountByIdQuery q, CancellationToken ct) =>
        LoanAccountViewLoader.LoadAsync(_accounts, _options, q.Id, ct);
}

public sealed class GetAccountQueueStatusQueryHandler : IRequestHandler<GetAccountQueueStatusQuery, AccountQueueStatusResponse>
{
    private readonly IAccountCreationRequestRepository _requests;
    public GetAccountQueueStatusQueryHandler(IAccountCreationRequestRepository requests) => _requests = requests;

    public async Task<AccountQueueStatusResponse> Handle(GetAccountQueueStatusQuery q, CancellationToken ct)
    {
        var counts = await _requests.CountByStatusAsync(ct);
        var all = Enum.GetValues<AccountRequestStatus>().ToDictionary(s => s.ToString(), s => counts.TryGetValue(s, out var n) ? n : 0);
        var oldest = await _requests.OldestNonCompletedAsync(ct);
        return new AccountQueueStatusResponse(all, oldest, oldest is null ? null : (DateTime.UtcNow - oldest.Value).TotalMinutes);
    }
}

public sealed class GetAccountDeadLettersQueryHandler : IRequestHandler<GetAccountDeadLettersQuery, PaginatedResponse<DeadLetterItemResponse>>
{
    private readonly IAccountCreationRequestRepository _requests;
    public GetAccountDeadLettersQueryHandler(IAccountCreationRequestRepository requests) => _requests = requests;

    public async Task<PaginatedResponse<DeadLetterItemResponse>> Handle(GetAccountDeadLettersQuery q, CancellationToken ct)
    {
        var (items, total) = await _requests.GetDeadLettersAsync(q.Page, q.PageSize, ct);
        return new PaginatedResponse<DeadLetterItemResponse>(
            items.Select(x => new DeadLetterItemResponse(x.Id, x.ApplicationNumber, x.FailureReason, x.AttemptCount, x.UpdatedAtUtc)).ToList(),
            total, q.Page, q.PageSize, (int)Math.Ceiling(total / (double)q.PageSize));
    }
}
```

  - [ ] 10.4 CREATE `Finnova.Service/LoanAccounts/Commands/LoanAccountCommands.cs`
    - Update, maker submit, checker decide, re-check, and reprocess. Every handler saves once through `IApplicationUnitOfWork` so the change and its audit entry commit together.
    - _Requirements: 8.12 to 8.18, 9.1 to 9.11, 10.3, 10.4, 11.7_

```csharp
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Finnova.Models.Contracts.LoanAccounts;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.LoanAccounts.Rules;
using Finnova.Service.Mappers;

namespace Finnova.Service.LoanAccounts.Commands;

public sealed record UpdateLoanAccountCommand(Guid Id, string? AccountName, string? Status, string RowVersion, string Actor, string CorrelationId)
    : IRequest<LoanAccountResponse>;
public sealed record SubmitKycResolutionCommand(Guid AccountId, string Reason, Guid MakerUserId, string MakerName, string CorrelationId)
    : IRequest<KycResolutionRequestResponse>;
public sealed record DecideKycResolutionCommand(Guid AccountId, Guid RequestId, bool Approve, string Comment, Guid CheckerUserId, string CheckerName, string CorrelationId)
    : IRequest<LoanAccountResponse>;
public sealed record RequestKycRecheckCommand(Guid AccountId, string? Reason, string Actor, string CorrelationId) : IRequest<bool>;
public sealed record ReprocessAccountRequestCommand(Guid RequestId, string Reason, string Actor, string CorrelationId) : IRequest<bool>;

// ---------------------------------------------------------------- validators
public sealed class UpdateLoanAccountCommandValidator : AbstractValidator<UpdateLoanAccountCommand>
{
    public UpdateLoanAccountCommandValidator()
    {
        RuleFor(x => x.AccountName).Must(n => n!.Trim().Length is >= 1 and <= 150)
            .When(x => x.AccountName is not null).WithMessage("Account name must be 1 to 150 characters.");
        RuleFor(x => x.Status).Must(s => Enum.TryParse<AccountStatus>(s, true, out _))
            .When(x => x.Status is not null).WithMessage("Status is not valid.");
        RuleFor(x => x.RowVersion).NotNull().WithMessage("Row version is required.");
    }
}

public sealed class SubmitKycResolutionCommandValidator : AbstractValidator<SubmitKycResolutionCommand>
{
    public SubmitKycResolutionCommandValidator() =>
        RuleFor(x => x.Reason).Must(r => (r?.Trim().Length ?? 0) is >= KycResolutionRules.ReasonMin and <= KycResolutionRules.ReasonMax)
            .WithMessage("Reason must be 10 to 500 characters.");
}

public sealed class DecideKycResolutionCommandValidator : AbstractValidator<DecideKycResolutionCommand>
{
    public DecideKycResolutionCommandValidator()
    {
        RuleFor(x => x.Comment).Must(c => !string.IsNullOrWhiteSpace(c)).WithMessage("A comment is required.");
        RuleFor(x => x.Comment).MaximumLength(500).WithMessage("Comment must not exceed 500 characters.");
    }
}

public sealed class ReprocessAccountRequestCommandValidator : AbstractValidator<ReprocessAccountRequestCommand>
{
    public ReprocessAccountRequestCommandValidator() =>
        RuleFor(x => x.Reason).Must(r => (r?.Trim().Length ?? 0) is >= 1 and <= 500).WithMessage("Reason must be 1 to 500 characters.");
}

// ---------------------------------------------------------------- handlers
public sealed class UpdateLoanAccountCommandHandler : IRequestHandler<UpdateLoanAccountCommand, LoanAccountResponse>
{
    private readonly ILoanAccountRepository _accounts;
    private readonly IAccountAuditRepository _audit;
    private readonly IApplicationUnitOfWork _uow;
    private readonly AccountCreationKycOptions _options;

    public UpdateLoanAccountCommandHandler(ILoanAccountRepository accounts, IAccountAuditRepository audit,
        IApplicationUnitOfWork uow, IOptions<AccountCreationKycOptions> options)
        => (_accounts, _audit, _uow, _options) = (accounts, audit, uow, options.Value);

    public async Task<LoanAccountResponse> Handle(UpdateLoanAccountCommand r, CancellationToken ct)
    {
        var account = await _accounts.GetTrackedAsync(r.Id, ct) ?? throw LoanAccountNotFoundException.ForAccount(r.Id);
        if (!AccountStatusRules.IsUpdatable(account.Status))
            throw new LoanAccountConflictException("A closed account cannot be updated.");

        byte[] provided;
        try { provided = Convert.FromBase64String(r.RowVersion ?? string.Empty); }
        catch (FormatException) { throw new LoanAccountValidationException("The row version is not valid."); }
        if (!(account.RowVersion ?? Array.Empty<byte>()).AsSpan().SequenceEqual(provided))
            throw new LoanAccountConflictException("The account was changed by another request. Refresh and retry.");
        _uow.Context.Entry(account).Property(x => x.RowVersion).OriginalValue = provided;   // guards the race window (R8.18)

        // Validate first, mutate second, so a refused update leaves the Account unchanged (R8.14).
        AccountStatus? target = null;
        if (r.Status is not null)
        {
            var parsed = Enum.Parse<AccountStatus>(r.Status, true);
            if (parsed != account.Status)
            {
                if (!AccountStatusRules.IsTransitionAllowed(account.Status, parsed))
                    throw new LoanAccountConflictException($"A status change from {account.Status} to {parsed} is not permitted.");
                target = parsed;
            }
        }
        var newName = r.AccountName?.Trim();
        var nameChanged = newName is not null && newName != account.AccountName;

        if (target is null && !nameChanged)
            return await LoanAccountViewLoader.LoadAsync(_accounts, _options, r.Id, ct);   // no-op: UpdatedAt untouched (R8.17)

        var summary = new List<string>();
        if (nameChanged) { summary.Add("AccountName changed"); account.AccountName = newName!; }
        if (target is { } t) { summary.Add($"Status {account.Status}->{t}"); account.Status = t; }
        account.UpdatedAt = DateTime.UtcNow;   // Type, Currency, Balance, numbers, references and dates are never touched (R8.16)

        await _audit.AddAsync(LoanAccountViewLoader.Audit.User(account.Id, null,
            target is null ? "Updated" : "StatusChanged", r.Actor, r.CorrelationId, string.Join("; ", summary)), ct);
        await _uow.SaveChangesAsync(ct);
        return await LoanAccountViewLoader.LoadAsync(_accounts, _options, r.Id, ct);
    }
}

public sealed class SubmitKycResolutionCommandHandler : IRequestHandler<SubmitKycResolutionCommand, KycResolutionRequestResponse>
{
    private readonly ILoanAccountRepository _accounts;
    private readonly IAccountKycResolutionRepository _resolutions;
    private readonly IAccountAuditRepository _audit;
    private readonly IApplicationUnitOfWork _uow;

    public SubmitKycResolutionCommandHandler(ILoanAccountRepository accounts, IAccountKycResolutionRepository resolutions,
        IAccountAuditRepository audit, IApplicationUnitOfWork uow)
        => (_accounts, _resolutions, _audit, _uow) = (accounts, resolutions, audit, uow);

    public async Task<KycResolutionRequestResponse> Handle(SubmitKycResolutionCommand r, CancellationToken ct)
    {
        var account = await _accounts.GetTrackedAsync(r.AccountId, ct) ?? throw LoanAccountNotFoundException.ForAccount(r.AccountId);
        var hasPending = await _resolutions.GetPendingAsync(account.Id, ct) is not null;
        switch (KycResolutionRules.CheckSubmit(account.KycFlagged, hasPending, r.Reason))
        {
            case SubmitCheck.NotFlagged: throw new LoanAccountConflictException("The KYC flag is already cleared for this account.");
            case SubmitCheck.PendingExists: throw new LoanAccountConflictException("A KYC resolution request is already pending for this account.");
            case SubmitCheck.ReasonInvalid: throw new LoanAccountValidationException("Reason must be 10 to 500 characters.");
        }

        var request = new AccountKycResolutionRequest
        {
            AccountId = account.Id,
            Status = KycResolutionStatus.Pending,
            MakerUserId = r.MakerUserId,
            MakerName = r.MakerName,
            Reason = r.Reason.Trim(),
            PreviousKycStatus = account.KycStatus ?? KycStatus.Pending,
            SubmittedAtUtc = DateTime.UtcNow,
            CorrelationId = r.CorrelationId
        };
        await _resolutions.AddAsync(request, ct);
        await _audit.AddAsync(LoanAccountViewLoader.Audit.User(account.Id, request.Id, "KycResolutionSubmitted",
            r.MakerUserId.ToString(), r.CorrelationId, "KYC resolution requested."), ct);
        try { await _uow.SaveChangesAsync(ct); }
        catch (DbUpdateException)   // filtered unique index: two makers raced (R9.9)
        {
            throw new LoanAccountConflictException("A KYC resolution request is already pending for this account.");
        }
        return request.ToResponse();   // the Account's KycStatus and KycFlagged are unchanged (R9.1)
    }
}

public sealed class DecideKycResolutionCommandHandler : IRequestHandler<DecideKycResolutionCommand, LoanAccountResponse>
{
    private readonly ILoanAccountRepository _accounts;
    private readonly IAccountKycResolutionRepository _resolutions;
    private readonly IAccountKycHistoryRepository _history;
    private readonly IAccountAuditRepository _audit;
    private readonly IApplicationUnitOfWork _uow;
    private readonly AccountCreationKycOptions _options;

    public DecideKycResolutionCommandHandler(ILoanAccountRepository accounts, IAccountKycResolutionRepository resolutions,
        IAccountKycHistoryRepository history, IAccountAuditRepository audit, IApplicationUnitOfWork uow,
        IOptions<AccountCreationKycOptions> options)
        => (_accounts, _resolutions, _history, _audit, _uow, _options) = (accounts, resolutions, history, audit, uow, options.Value);

    public async Task<LoanAccountResponse> Handle(DecideKycResolutionCommand r, CancellationToken ct)
    {
        var request = await _resolutions.GetTrackedAsync(r.RequestId, ct);
        if (request is null || request.AccountId != r.AccountId)
            throw new LoanAccountNotFoundException($"KYC resolution request '{r.RequestId}' was not found.");

        switch (KycResolutionRules.CheckDecision(request.Status, request.MakerUserId, r.CheckerUserId, r.Comment))
        {
            case DecisionCheck.NotPending: throw new LoanAccountConflictException("Only a pending KYC resolution request can be decided.");
            case DecisionCheck.SameUser: throw new KycResolutionForbiddenException();   // four-eyes (R9.4)
            case DecisionCheck.CommentRequired: throw new LoanAccountValidationException("A comment is required.");
        }

        var account = await _accounts.GetTrackedAsync(r.AccountId, ct) ?? throw LoanAccountNotFoundException.ForAccount(r.AccountId);
        var now = DateTime.UtcNow;
        request.Status = r.Approve ? KycResolutionStatus.Approved : KycResolutionStatus.Rejected;
        request.CheckerUserId = r.CheckerUserId;
        request.CheckerName = r.CheckerName;
        request.DecisionComment = r.Comment.Trim();
        request.DecidedAtUtc = now;

        if (r.Approve)
        {
            var previous = account.KycStatus;
            KycResolutionRules.ApplyApproval(account, now);   // the only path to Passed/Manual
            await _history.AddAsync(new AccountKycHistoryEntry
            {
                AccountId = account.Id,
                Source = KycSource.Manual,
                KycStatus = KycStatus.Passed,
                PanValidation = account.KycPanValidation ?? KycCheckResult.NotChecked,        // automated results are retained
                PanNameMatch = account.KycPanNameMatch ?? KycCheckResult.NotChecked,
                AadhaarValidation = account.KycAadhaarValidation ?? KycCheckResult.NotChecked,
                AadhaarAddressMatch = account.KycAadhaarAddressMatch ?? KycCheckResult.NotChecked,
                PreviousStatus = previous,
                Reason = request.Reason,
                Comment = request.DecisionComment,
                ResolutionRequestId = request.Id,
                MakerUserId = request.MakerUserId,
                CheckerUserId = r.CheckerUserId,
                Actor = r.CheckerName,
                CorrelationId = r.CorrelationId,
                OccurredAtUtc = now
            }, ct);
        }

        await _audit.AddAsync(LoanAccountViewLoader.Audit.User(account.Id, request.Id,
            r.Approve ? "KycResolutionApproved" : "KycResolutionRejected", r.CheckerUserId.ToString(), r.CorrelationId,
            r.Approve ? "KYC resolution approved." : "KYC resolution rejected."), ct);
        await _uow.SaveChangesAsync(ct);   // DbUpdateConcurrencyException (two checkers) is mapped to 409 by the middleware
        return await LoanAccountViewLoader.LoadAsync(_accounts, _options, account.Id, ct);
    }
}

public sealed class RequestKycRecheckCommandHandler : IRequestHandler<RequestKycRecheckCommand, bool>
{
    private readonly ILoanAccountRepository _accounts;
    private readonly IAccountKycJobRepository _jobs;
    private readonly IAccountAuditRepository _audit;
    private readonly IApplicationUnitOfWork _uow;

    public RequestKycRecheckCommandHandler(ILoanAccountRepository accounts, IAccountKycJobRepository jobs,
        IAccountAuditRepository audit, IApplicationUnitOfWork uow)
        => (_accounts, _jobs, _audit, _uow) = (accounts, jobs, audit, uow);

    public async Task<bool> Handle(RequestKycRecheckCommand r, CancellationToken ct)
    {
        var account = await _accounts.GetTrackedAsync(r.AccountId, ct) ?? throw LoanAccountNotFoundException.ForAccount(r.AccountId);
        if (!account.KycFlagged) throw new LoanAccountConflictException("A KYC re-check is only available for flagged accounts.");

        var now = DateTime.UtcNow;
        var job = await _jobs.GetTrackedAsync(account.Id, ct);
        if (job is null)
        {
            await _jobs.AddAsync(new AccountKycJob { AccountId = account.Id, CorrelationId = r.CorrelationId, RecheckRequestedBy = r.Actor, RecheckReason = r.Reason }, ct);
        }
        else
        {
            job.Status = AccountRequestStatus.Pending;
            job.AttemptCount = 0;
            job.NextAttemptAtUtc = null;
            job.ClaimedBy = null;
            job.LeaseExpiresAtUtc = null;
            job.FailureReason = null;
            job.CorrelationId = r.CorrelationId;
            job.RecheckRequestedBy = r.Actor;
            job.RecheckReason = r.Reason;
            job.UpdatedAtUtc = now;
        }
        await _audit.AddAsync(LoanAccountViewLoader.Audit.User(account.Id, null, "KycRecheck", r.Actor, r.CorrelationId, "KYC re-check requested."), ct);
        await _uow.SaveChangesAsync(ct);
        return true;
    }
}

public sealed class ReprocessAccountRequestCommandHandler : IRequestHandler<ReprocessAccountRequestCommand, bool>
{
    private readonly IAccountCreationRequestRepository _requests;
    private readonly IAccountAuditRepository _audit;
    private readonly IApplicationUnitOfWork _uow;

    public ReprocessAccountRequestCommandHandler(IAccountCreationRequestRepository requests, IAccountAuditRepository audit, IApplicationUnitOfWork uow)
        => (_requests, _audit, _uow) = (requests, audit, uow);

    public async Task<bool> Handle(ReprocessAccountRequestCommand r, CancellationToken ct)
    {
        var request = await _requests.GetTrackedAsync(r.RequestId, ct)
                      ?? throw new LoanAccountNotFoundException($"Account creation request '{r.RequestId}' was not found.");
        if (!RequestRecoveryRules.CanReprocess(request.Status))
            throw new LoanAccountConflictException("Only a dead-lettered request can be reprocessed.");

        RequestRecoveryRules.ResetForReprocess(request, DateTime.UtcNow);
        await _audit.AddAsync(LoanAccountViewLoader.Audit.User(null, request.Id, "Reprocessed", r.Actor, r.CorrelationId,
            $"Reprocess requested: {r.Reason.Trim()}"), ct);
        await _uow.SaveChangesAsync(ct);
        return true;
    }
}
```

- [ ] 11. Backend AccountsService host: controller, policies, error mapping
  - [ ] 11.1 CREATE `Finnova.AccountsService/Controllers/LoanAccountsController.cs`
    - Route is `api/loan-accounts` (assumption A1), not `api/accounts/loan-accounts`. The existing `AccountsController` is untouched.
    - _Requirements: 8.1 to 8.21, 9.12, 10.6, 11.1 to 11.3_

```csharp
using System.Security.Claims;
using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.LoanAccounts;
using Finnova.Models.Domain.Exceptions;
using Finnova.Service.LoanAccounts.Commands;
using Finnova.Service.LoanAccounts.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finnova.AccountsService.Controllers;

[ApiController, Route("api/loan-accounts")]
public sealed class LoanAccountsController : ControllerBase
{
    private readonly IMediator _mediator;
    public LoanAccountsController(IMediator mediator) => _mediator = mediator;

    // Identities come from the validated JWT, never from the request body (four-eyes depends on this).
    private string ActorText => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? "authenticated-user";
    private Guid ActorId => Guid.TryParse(ActorText, out var id)
        ? id : throw new LoanAccountValidationException("The authenticated user id is not a valid identifier.");
    private string ActorName => User.FindFirstValue(ClaimTypes.Name) ?? ActorText;
    private string CorrelationId => HttpContext.TraceIdentifier;

    [HttpGet, Authorize(Policy = "AccountsRead")]
    public async Task<ActionResult<PaginatedResponse<LoanAccountListItemResponse>>> List([FromQuery] LoanAccountSearchRequest r, CancellationToken ct)
        => Ok(await _mediator.Send(new GetLoanAccountsQuery(r), ct));

    [HttpGet("{id:guid}"), Authorize(Policy = "AccountsRead")]
    public async Task<ActionResult<LoanAccountResponse>> GetById(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new GetLoanAccountByIdQuery(id), ct));

    [HttpPut("{id:guid}"), Authorize(Policy = "ApplicationProcessor")]
    public async Task<ActionResult<LoanAccountResponse>> Update(Guid id, UpdateLoanAccountRequest r, CancellationToken ct)
        => Ok(await _mediator.Send(new UpdateLoanAccountCommand(id, r.AccountName, r.Status, r.RowVersion, ActorText, CorrelationId), ct));

    [HttpPost("{id:guid}/kyc/resolution-requests"), Authorize(Policy = "ApplicationProcessor")]
    public async Task<ActionResult<KycResolutionRequestResponse>> SubmitResolution(Guid id, SubmitKycResolutionRequest r, CancellationToken ct)
    {
        var result = await _mediator.Send(new SubmitKycResolutionCommand(id, r.Reason, ActorId, ActorName, CorrelationId), ct);
        return CreatedAtAction(nameof(GetById), new { id }, result);
    }

    [HttpPost("{id:guid}/kyc/resolution-requests/{requestId:guid}/approve"), Authorize(Policy = "KycApprover")]
    public async Task<ActionResult<LoanAccountResponse>> Approve(Guid id, Guid requestId, DecideKycResolutionRequest r, CancellationToken ct)
        => Ok(await _mediator.Send(new DecideKycResolutionCommand(id, requestId, true, r.Comment, ActorId, ActorName, CorrelationId), ct));

    [HttpPost("{id:guid}/kyc/resolution-requests/{requestId:guid}/reject"), Authorize(Policy = "KycApprover")]
    public async Task<ActionResult<LoanAccountResponse>> Reject(Guid id, Guid requestId, DecideKycResolutionRequest r, CancellationToken ct)
        => Ok(await _mediator.Send(new DecideKycResolutionCommand(id, requestId, false, r.Comment, ActorId, ActorName, CorrelationId), ct));

    [HttpPost("{id:guid}/kyc/recheck"), Authorize(Policy = "ApplicationProcessor")]
    public async Task<IActionResult> Recheck(Guid id, RecheckKycRequest r, CancellationToken ct)
    {
        await _mediator.Send(new RequestKycRecheckCommand(id, r.Reason, ActorText, CorrelationId), ct);
        return Accepted();
    }

    [HttpGet("operations/queue-status"), Authorize(Policy = "AccountsOps")]
    public async Task<ActionResult<AccountQueueStatusResponse>> QueueStatus(CancellationToken ct)
        => Ok(await _mediator.Send(new GetAccountQueueStatusQuery(), ct));

    [HttpGet("operations/dead-letters"), Authorize(Policy = "AccountsOps")]
    public async Task<ActionResult<PaginatedResponse<DeadLetterItemResponse>>> DeadLetters([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await _mediator.Send(new GetAccountDeadLettersQuery(page, pageSize), ct));

    [HttpPost("operations/requests/{requestId:guid}/reprocess"), Authorize(Policy = "AccountsOps")]
    public async Task<IActionResult> Reprocess(Guid requestId, ReprocessRequest r, CancellationToken ct)
    {
        await _mediator.Send(new ReprocessAccountRequestCommand(requestId, r.Reason, ActorText, CorrelationId), ct);
        return Ok();
    }
}
```

  - [ ] 11.2 MODIFY `Finnova.AccountsService/Program.cs`
    - (a) Add `using Finnova.Service.LoanAccounts;` at the top.
    - (b) Inside the existing `builder.Services.AddAuthorization(options => { ... })` block add after the `ApplicationProcessor` policy line:
    - _Requirements: 11.2, 18.4, assumption A17_

```csharp
    // Loan Accounts (application-account-creation). SystemAdmin is listed explicitly only for operations.
    options.AddPolicy("AccountsRead", p => p.RequireAssertion(ctx =>
        ctx.User.IsInRole("AccountsViewer") || ctx.User.IsInRole("ApplicationProcessor")));
    options.AddPolicy("KycApprover", p => p.RequireRole("KycApprover"));
    options.AddPolicy("AccountsOps", p => p.RequireAssertion(ctx =>
        ctx.User.IsInRole("AccountsOperator") || ctx.User.IsInRole("SystemAdmin")));
```

    (c) Add after `builder.Services.AddScoped<IAssetCodeGenerator, AssetCodeGenerator>();`:

```csharp
builder.Services.Configure<AccountCreationKycOptions>(builder.Configuration.GetSection(AccountCreationKycOptions.SectionName));
```

  - [ ] 11.3 MODIFY `Finnova.AccountsService/Middlewares/ExceptionHandlingMiddleware.cs`
    - (a) Inside the `catch` block, after the `var fallbackCode = ...` line add:
    - _Requirements: 8.10, 8.14, 9.4, 11.6_

```csharp
            var isLoan = ctx.Request.Path.StartsWithSegments("/api/loan-accounts", StringComparison.OrdinalIgnoreCase);
```

    (b) In the `switch`, add these arms before the `FluentValidation.ValidationException` arm, and change the last two arms:

```csharp
                LoanAccountException l => (l.ErrorCode switch
                {
                    "ERR-ACC-404" => StatusCodes.Status404NotFound,
                    "ERR-ACC-403" => StatusCodes.Status403Forbidden,
                    "ERR-ACC-400" => StatusCodes.Status400BadRequest,
                    _ => StatusCodes.Status409Conflict
                }, l.ErrorCode, l.Message),
                Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException =>
                    (StatusCodes.Status409Conflict, isLoan ? "ERR-ACC-409" : "ERR-GL-409",
                     "The record was changed by another request. Refresh and retry."),

                // ---- shared: validation failures and unhandled fallback ----
                FluentValidation.ValidationException v => (StatusCodes.Status400BadRequest, isLoan ? "ERR-ACC-400" : validationCode, v.Message),
                _ => (StatusCodes.Status500InternalServerError, isLoan ? "ERR-ACC-500" : fallbackCode, "Unexpected error.")
```

- [ ] 12. Backend SystemAdminService host: internal DNC, KYC, and service-auth endpoints
  - [ ] 12.1 CREATE `Finnova.Service/LoanAccounts/Kyc/KycAbstractions.cs`
    - Aggregator seam (R7) and the combined verification facade used by the internal endpoint.
    - _Requirements: 5.1, 5.3, 7.1 to 7.10, 17.2 to 17.4_

```csharp
using Finnova.Models.Contracts.IdentityVerification;
using Finnova.Models.Contracts.Internal;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Service.IdentityVerification.Mapping;
using Finnova.Service.IdentityVerification.Workflow;

namespace Finnova.Service.LoanAccounts.Kyc;

/// <summary>Provider-neutral aggregator contract. Provider-specific fields stay inside adapters.</summary>
public interface IKycAggregatorProvider
{
    Task<NormalizedKycResult> VerifyAsync(ProviderNeutralKycRequest request, CancellationToken ct);
}

/// <summary>Default registration: no provider configured (R7.6). Aggregator mode then fails with ERR-KYC-503.</summary>
public sealed class NullKycAggregatorProvider : IKycAggregatorProvider
{
    public Task<NormalizedKycResult> VerifyAsync(ProviderNeutralKycRequest request, CancellationToken ct)
        => throw new KycAggregatorNotConfiguredException();
}

public interface IKycVerificationFacade
{
    Task<InternalKycResponse> VerifyAsync(InternalKycRequest request, CancellationToken ct);
}

public sealed class KycVerificationFacade : IKycVerificationFacade
{
    private readonly IVerificationWorkflowCoordinator _coordinator;
    private readonly IKycAggregatorProvider _aggregator;

    public KycVerificationFacade(IVerificationWorkflowCoordinator coordinator, IKycAggregatorProvider aggregator)
        => (_coordinator, _aggregator) = (coordinator, aggregator);

    public async Task<InternalKycResponse> VerifyAsync(InternalKycRequest r, CancellationToken ct)
    {
        var mapped = CustomerMasterMappingProfile.Map(r.Name, r.Address);
        var pan = string.IsNullOrWhiteSpace(r.Pan) ? null : r.Pan.Trim();
        var aadhaar = string.IsNullOrWhiteSpace(r.Aadhaar) ? null : r.Aadhaar.Trim();

        if (string.Equals(r.Mode, nameof(KycMode.Aggregator), StringComparison.OrdinalIgnoreCase))
        {
            var n = await _aggregator.VerifyAsync(new ProviderNeutralKycRequest(
                r.CustomerId, pan, aadhaar, mapped.Name, mapped.Address, r.CorrelationId), ct);
            return new InternalKycResponse(n.PanValidation.ToString(), n.PanNameMatch.ToString(),
                n.AadhaarValidation.ToString(), n.AadhaarAddressMatch.ToString(), null, n.ProviderReference, n.SafeReason, n.TimestampUtc);
        }

        // Internal mode. A check is run only when its inputs exist; the rest are reported as NotChecked (R5.7, R17.4).
        var panPart = pan is not null && mapped.Name.Length > 0;
        var aadhaarPart = aadhaar is not null && mapped.Address.Length > 0;
        if (!panPart && !aadhaarPart)
        {
            var nc = KycCheckResult.NotChecked.ToString();
            return new InternalKycResponse(nc, nc, nc, nc, null, null, "No verifiable identity data.", DateTime.UtcNow);
        }

        ComparisonSourceContract Source() => new(null, null, null,
            new Dictionary<string, string> { ["documentName"] = mapped.Name, ["documentAddress"] = mapped.Address },
            CustomerMasterMappingProfile.Name);

        var type = panPart && aadhaarPart ? VerificationWorkflowType.Full
                 : panPart ? VerificationWorkflowType.PanAndName : VerificationWorkflowType.AadhaarAndAddress;
        var res = await _coordinator.ExecuteAsync(new VerificationWorkflowRequest(
            type, WorkflowInputMode.Partial,
            panPart ? pan : null, panPart ? mapped.Name : null, panPart ? Source() : null,
            aadhaarPart ? aadhaar : null, aadhaarPart ? mapped.Address : null, aadhaarPart ? Source() : null), ct);

        KycCheckResult Resolve(bool requested, KycCheckResult? value, VerificationOperationType op) =>
            !requested ? KycCheckResult.NotChecked
            : value is { } v ? v
            : res.PartialFailures.Any(f => f.OperationType == op) ? KycCheckResult.Unavailable : KycCheckResult.NotChecked;

        return new InternalKycResponse(
            Resolve(panPart, res.PanIdentifierResult is null ? null : KycResultMapper.FromValidation(res.PanIdentifierResult.ValidationStatus), VerificationOperationType.PanIdentifierValidation).ToString(),
            Resolve(panPart, res.PanNameResult is null ? null : KycResultMapper.FromComparison(res.PanNameResult.ComparisonOutcome), VerificationOperationType.PanNameVerification).ToString(),
            Resolve(aadhaarPart, res.AadhaarIdentifierResult is null ? null : KycResultMapper.FromValidation(res.AadhaarIdentifierResult.ValidationStatus), VerificationOperationType.AadhaarIdentifierValidation).ToString(),
            Resolve(aadhaarPart, res.AadhaarAddressResult is null ? null : KycResultMapper.FromComparison(res.AadhaarAddressResult.ComparisonOutcome), VerificationOperationType.AadhaarAddressVerification).ToString(),
            res.SessionId, null, res.PartialFailures.Count == 0 ? null : "Some checks could not be completed.", res.TimestampUtc);
    }
}
```

  - [ ] 12.2 CREATE `Finnova.SystemAdminService/ServiceAuth/ServiceAuthSupport.cs`
    - Options, token issuer, idempotent SystemUser seeder, and the `/internal/**` forwarded-header guard (design D3).
    - _Requirements: 2.10, 3.10, 12.4_

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Finnova.Models.Contracts.Internal;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Context;
using Finnova.Service.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Finnova.SystemAdminService.ServiceAuth;

public sealed class ServiceAuthOptions
{
    public const string SectionName = "ServiceAuth";
    public Guid SystemUserId { get; set; } = new("00000000-0000-0000-0004-000000000001");
    /// <summary>Secure configuration only (environment / secret store). Never commit a value.</summary>
    public string? SystemUserSecret { get; set; }
    public int TokenMinutes { get; set; } = 10;
}

public sealed class ServiceTokenIssuer
{
    private readonly JwtSettings _jwt;
    private readonly ServiceAuthOptions _options;
    public ServiceTokenIssuer(IOptions<JwtSettings> jwt, IOptions<ServiceAuthOptions> options)
        => (_jwt, _options) = (jwt.Value, options.Value);

    public ServiceTokenResponse Issue(Guid clientId)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, clientId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, clientId.ToString()),
            new(ClaimTypes.Name, "SystemUser"),
            new(ClaimTypes.Role, "ServiceAccount"),
            new("permission", "svc.dcn.issue"),      // least privilege: exactly the two internal operations
            new("permission", "svc.kyc.verify")
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
        var expires = DateTime.UtcNow.AddMinutes(_options.TokenMinutes);
        var token = new JwtSecurityToken(_jwt.Issuer, _jwt.Audience, claims, expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new ServiceTokenResponse(new JwtSecurityTokenHandler().WriteToken(token), _options.TokenMinutes * 60);
    }
}

/// <summary>Creates the SystemUser and its credential hash at startup. Idempotent; skipped when no secret is configured.</summary>
public sealed class SystemUserSeeder : IHostedService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ServiceAuthOptions _options;
    private readonly ILogger<SystemUserSeeder> _log;

    public SystemUserSeeder(IServiceScopeFactory scopes, IOptions<ServiceAuthOptions> options, ILogger<SystemUserSeeder> log)
        => (_scopes, _options, _log) = (scopes, options.Value, log);

    public async Task StartAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.SystemUserSecret))
        {
            _log.LogWarning("ServiceAuth:SystemUserSecret is not set; the SystemUser service credential was not seeded.");
            return;
        }
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FinnovaDbContext>();
            var id = _options.SystemUserId;
            if (!await db.UserAccounts.AnyAsync(x => x.Id == id, ct))
            {
                db.UserAccounts.Add(new UserAccount
                {
                    Id = id, UserCode = "SYSUSR", UserName = "system.user", Name = "System User",
                    PasswordHash = PasswordHasher.Hash(Guid.NewGuid().ToString("N")),   // unknown password: interactive login is impossible
                    Role = UserRole.User, DateOfJoining = DateTime.UtcNow, Designation = "Automation", Department = "System",
                    UserType = UserType.Corporate, IsActive = false
                });
            }
            var credential = await db.ServiceCredentials.FirstOrDefaultAsync(x => x.ClientId == id, ct);
            if (credential is null)
                db.ServiceCredentials.Add(new ServiceCredential { ClientId = id, SecretHash = PasswordHasher.Hash(_options.SystemUserSecret!), IsActive = true });
            else if (!PasswordHasher.Verify(_options.SystemUserSecret!, credential.SecretHash))
                credential.SecretHash = PasswordHasher.Hash(_options.SystemUserSecret!);   // secret rotation
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "SystemUser seeding failed.");   // never fail host start; the token endpoint then rejects everything
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}

/// <summary>Returns 404 for /internal/** when the request carries forwarded headers (it came through a proxy or the gateway).</summary>
public sealed class InternalEndpointGuardMiddleware
{
    private readonly RequestDelegate _next;
    public InternalEndpointGuardMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx)
    {
        if (ctx.Request.Path.StartsWithSegments("/internal", StringComparison.OrdinalIgnoreCase) &&
            (ctx.Request.Headers.ContainsKey("X-Forwarded-For") || ctx.Request.Headers.ContainsKey("X-Forwarded-Host")))
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        await _next(ctx);
    }
}
```

  - [ ] 12.3 CREATE `Finnova.SystemAdminService/Controllers/InternalControllers.cs`
    - Not exposed through the gateway (task 13). Authorization uses the `permission` claim of the service token.
    - _Requirements: 4.1, 5.1, 16.8, 16.10_

```csharp
using System.Security.Claims;
using Finnova.Models.Contracts.Internal;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Context;
using Finnova.Service.Auth;
using Finnova.Service.DocumentNumberControl.Commands.IssueNumber;
using Finnova.Service.LoanAccounts.Kyc;
using Finnova.SystemAdminService.ServiceAuth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Finnova.SystemAdminService.Controllers;

[ApiController, Route("internal/service-auth")]
public sealed class ServiceAuthController : ControllerBase
{
    private readonly FinnovaDbContext _db;
    private readonly ServiceTokenIssuer _issuer;
    private readonly ILogger<ServiceAuthController> _log;
    public ServiceAuthController(FinnovaDbContext db, ServiceTokenIssuer issuer, ILogger<ServiceAuthController> log)
        => (_db, _issuer, _log) = (db, issuer, log);

    [HttpPost("token"), AllowAnonymous, EnableRateLimiting("service-token")]
    public async Task<ActionResult<ServiceTokenResponse>> Token([FromBody] ServiceTokenRequest r, CancellationToken ct)
    {
        var credential = await _db.ServiceCredentials.FirstOrDefaultAsync(x => x.ClientId == r.ClientId && x.IsActive, ct);
        if (credential is null || !PasswordHasher.Verify(r.ClientSecret, credential.SecretHash))
        {
            _log.LogWarning("Service token request rejected for client {ClientId}.", r.ClientId);   // no secret in the log
            return Unauthorized();
        }
        credential.LastUsedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(_issuer.Issue(credential.ClientId));
    }
}

[ApiController, Route("internal/dcn")]
public sealed class InternalDncController : ControllerBase
{
    private readonly IMediator _mediator;
    public InternalDncController(IMediator mediator) => _mediator = mediator;

    [HttpPost("issue"), Authorize(Policy = "ServiceDncIssue")]
    public async Task<ActionResult<InternalIssueResponse>> Issue([FromBody] InternalIssueRequest r, CancellationToken ct)
    {
        var actor = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "SystemUser";
        var issued = await _mediator.Send(new IssueNumberCommand(
            r.DocumentType, NumberScope.Global, null, actor, r.LineOfBusinessId, r.BranchId, UseFallback: true), ct);
        return Ok(new InternalIssueResponse(issued.Number, issued.SchemeId, issued.SchemeCode ?? string.Empty, issued.ScopeLevel ?? string.Empty));
    }
}

[ApiController, Route("internal/kyc")]
public sealed class InternalKycController : ControllerBase
{
    private readonly IKycVerificationFacade _facade;
    public InternalKycController(IKycVerificationFacade facade) => _facade = facade;

    [HttpPost("verify"), Authorize(Policy = "ServiceKycVerify")]
    public async Task<ActionResult<InternalKycResponse>> Verify([FromBody] InternalKycRequest r, CancellationToken ct)
        => Ok(await _facade.VerifyAsync(r, ct));
}
```

  - [ ] 12.4 MODIFY `Finnova.SystemAdminService/Program.cs` (wiring)
    - (a) Add usings at the top:

```csharp
using Finnova.Service.LoanAccounts.Kyc;
using Finnova.SystemAdminService.ServiceAuth;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
```

    (b) Add two policies inside the existing `AddAuthorization(options => { ... })` block, after the `JveOperationsRecovery` line:

```csharp
    options.AddPolicy("ServiceDncIssue", policy => policy.RequireClaim("permission", "svc.dcn.issue"));
    options.AddPolicy("ServiceKycVerify", policy => policy.RequireClaim("permission", "svc.kyc.verify"));
```

    (c) Add services after `builder.Services.AddScoped<IComparisonAdapterResolver, ConfiguredComparisonAdapterResolver>();`:

```csharp
// Internal service endpoints (application-account-creation): service token, DNC issuance, KYC verify.
builder.Services.Configure<ServiceAuthOptions>(builder.Configuration.GetSection(ServiceAuthOptions.SectionName));
builder.Services.AddScoped<ServiceTokenIssuer>();
builder.Services.AddHostedService<SystemUserSeeder>();
builder.Services.AddScoped<IKycAggregatorProvider, NullKycAggregatorProvider>();   // swap for a real adapter later (R7.10)
builder.Services.AddScoped<IKycVerificationFacade, KycVerificationFacade>();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddFixedWindowLimiter("service-token", l => { l.PermitLimit = 30; l.Window = TimeSpan.FromMinutes(1); l.QueueLimit = 0; });
});
```

    (d) Pipeline order: add the guard first and the rate limiter before authentication. Replace the block from `app.UseMiddleware<...ExceptionHandlingMiddleware>();` through `app.UseAuthorization();` with:

```csharp
app.UseMiddleware<InternalEndpointGuardMiddleware>();   // 404 for /internal/** when forwarded headers are present
app.UseMiddleware<Finnova.SystemAdminService.Middleware.ExceptionHandlingMiddleware>(); // ERR-LKP-005 + validation -> ProblemDetails
app.UseRateLimiter();
app.UseAuthentication();   // must precede UseAuthorization
app.UseAuthorization();
```

  - [ ] 12.5 MODIFY `Finnova.SystemAdminService/Middleware/ExceptionHandlingMiddleware.cs`
    - Add the aggregator arm (and its using) right after the line `NumberingSchemeValidationException => (400, "ERR-DCN-400", ex.Message),`. Validation errors on `/internal/dcn/**` keep the generic family code; the worker only inspects the HTTP status.
    - _Requirements: 7.6_

```csharp
                KycAggregatorNotConfiguredException => (503, "ERR-KYC-503", ex.Message),
```

  - [ ] 12.6 MODIFY `Finnova.SystemAdminService/appsettings.json`
    - Add a section (the secret is supplied by environment variable `ServiceAuth__SystemUserSecret`; do not commit a value):

```json
  "ServiceAuth": {
    "SystemUserId": "00000000-0000-0000-0004-000000000001",
    "TokenMinutes": 10
  },
```

- [ ] 13. Backend API gateway
  - [ ] 13.1 MODIFY `Finnova.ApiGateway/appsettings.json`
    - Add these routes inside `ReverseProxy.Routes` (after `ua-application-processing-alias-root`). No internal route is added.
    - _Requirements: 12.1, 12.2, 12.3_

```json
      "loan-accounts-route": {
        "ClusterId": "accounts-cluster",
        "Match": { "Path": "/api/loan-accounts/{**catch-all}" }
      },
      "loan-accounts-root": {
        "ClusterId": "accounts-cluster",
        "Match": { "Path": "/api/loan-accounts" }
      },
      "ua-loan-accounts-alias-route": {
        "ClusterId": "accounts-cluster",
        "Match": { "Path": "/api/ua/api/loan-accounts/{**catch-all}" },
        "Transforms": [
          { "PathRemovePrefix": "/api/ua/api" },
          { "PathPrefix": "/api" }
        ]
      },
      "ua-loan-accounts-alias-root": {
        "ClusterId": "accounts-cluster",
        "Match": { "Path": "/api/ua/api/loan-accounts" },
        "Transforms": [
          { "PathRemovePrefix": "/api/ua/api" },
          { "PathPrefix": "/api" }
        ]
      },
```

  - [ ] 13.2 MODIFY `Finnova.ApiGateway/Program.cs`
    - Insert before `app.MapReverseProxy();` so no `/internal/**` path (including `/api/systemadmin/internal/...` and `/api/ua/api/internal/...`) is ever forwarded.
    - _Requirements: 12.4_

```csharp
// Internal service endpoints (DNC issuance, KYC verification, service tokens) are never public.
app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path.Value ?? string.Empty;
    if (path.Contains("/internal/", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith("/internal", StringComparison.OrdinalIgnoreCase))
    {
        ctx.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }
    await next();
});
```

- [ ] 14. Backend tests (xUnit, FsCheck.Xunit, `WebApplicationFactory<Program>`)
  - [ ] 14.1 CREATE `Finnova.Tests/LoanAccounts/LoanAccountTestData.cs`
    - Shared builders for the InMemory database tests.
    - _Requirements: 15.1, 15.3_

```csharp
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Numbering;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Finnova.Tests.LoanAccounts;

internal static class LoanAccountTestData
{
    public static FinnovaDbContext NewDb() => new(new DbContextOptionsBuilder<FinnovaDbContext>()
        .UseInMemoryDatabase("loan-" + Guid.NewGuid())
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);

    public static Customer NewCustomer(string code, string firstName)
    {
        var c = new Customer();
        c.SetGeneratedCode(code);
        c.Initialize(
            Guid.NewGuid(), true, false, false, "IND",                                   // companyId, isCustomer, isGuarantor, isCoApplicant, customerTypeCode
            null, null, null, null, null, null, null, null, null, null, null, null,    // groupCode .. subSectorCode (12)
            firstName, null, null, null, null, null,                                    // firstName .. sixthName (6)
            null, null, null, null, null, null, null, null);                            // notes .. employerEconomicActivityCode (8)
        return c;
    }

    public static LoanAccount SeedLoan(FinnovaDbContext db, string applicationNumber, string accountNumber, string customerCode,
        string customerName, bool flagged = true, KycStatus kyc = KycStatus.Failed, DateTime? createdAt = null)
    {
        var customer = NewCustomer(customerCode, customerName);
        db.Customers.Add(customer);
        var app = new Application
        {
            ApplicationNumber = applicationNumber, CustomerId = customer.Id, LineOfBusinessId = Guid.NewGuid(), BranchId = Guid.NewGuid(),
            ProductCode = "HL", SchemeCode = "S1", FinanceAmount = 100000m, IrrPercent = 12m, Tenure = 12,
            RepaymentFrequency = RepaymentFrequency.Monthly, DisbursementDate = new DateOnly(2025, 1, 1), Status = ApplicationStatus.Processed
        };
        db.Applications.Add(app);
        var account = new LoanAccount
        {
            AccountNumber = accountNumber, AccountName = customerName + " " + applicationNumber, Type = AccountType.Loan,
            Status = AccountStatus.Active, ApplicationId = app.Id, CustomerId = customer.Id, LineOfBusinessId = app.LineOfBusinessId,
            BranchId = app.BranchId, ProductCode = "HL", SchemeCode = "S1", CustomerNameSnapshot = customerName,
            KycStatus = kyc, KycFlagged = flagged, KycSource = KycSource.Internal,
            CreatedAt = createdAt ?? DateTime.UtcNow, UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };
        db.LoanAccounts.Add(account);
        db.SaveChanges();
        return account;
    }

    public static string RowVersionOf(FinnovaDbContext db, Guid accountId) =>
        Convert.ToBase64String(db.LoanAccounts.AsNoTracking().First(a => a.Id == accountId).RowVersion ?? Array.Empty<byte>());
}

internal sealed class FakeNumberingReferences : INumberingReferenceRepository
{
    public Task<string?> GetLobTokenAsync(Guid id, CancellationToken ct) => Task.FromResult<string?>("LOB");
    public Task<string?> GetBranchCodeAsync(Guid id, CancellationToken ct) => Task.FromResult<string?>("BR");
}
```

  - [ ] 14.2 CREATE `Finnova.Tests/LoanAccounts/KycStatusDeriverProperties.cs`
    - Covers Properties 1 and 2.
    - _Requirements: 5.4, 5.5, 5.6, 5.11, 5.12, 6.2_

```csharp
using Finnova.Models.Domain.Enums;
using Finnova.Service.LoanAccounts.Rules;
using FsCheck.Xunit;
using Xunit;

namespace Finnova.Tests.LoanAccounts;

public sealed class KycStatusDeriverProperties
{
    [Property(MaxTest = 100)] // Feature: application-account-creation, Property 1: KYC status derivation
    public bool StatusFollowsPrecedence(KycCheckResult a, KycCheckResult b, KycCheckResult c, KycCheckResult d)
    {
        var all = new[] { a, b, c, d };
        var (status, _) = KycStatusDeriver.Derive(a, b, c, d);
        var expected = all.All(x => x == KycCheckResult.Passed) ? KycStatus.Passed
            : all.Any(x => x == KycCheckResult.Failed) ? KycStatus.Failed
            : all.Any(x => x == KycCheckResult.Unavailable) ? KycStatus.Unavailable
            : KycStatus.PartiallyVerified;
        return status == expected && (status == KycStatus.Passed) == all.All(x => x == KycCheckResult.Passed);
    }

    [Property(MaxTest = 100)] // Feature: application-account-creation, Property 2: KYC flag equivalence
    public bool FlagIsFalseExactlyWhenPassed(KycCheckResult a, KycCheckResult b, KycCheckResult c, KycCheckResult d)
    {
        var (status, flagged) = KycStatusDeriver.Derive(a, b, c, d);
        return flagged == (status != KycStatus.Passed);
    }

    [Fact] // Property 2 (bypass part)
    public void BypassIsBypassedAndFlagged()
    {
        var (status, flagged) = KycStatusDeriver.ForBypass();
        Assert.Equal(KycStatus.Bypassed, status);
        Assert.True(flagged);
    }
}
```

  - [ ] 14.3 CREATE `Finnova.Tests/LoanAccounts/LoanAccountRulesProperties.cs`
    - Covers Properties 8, 12, 13, 14, 18, 19.
    - _Requirements: 8.11, 8.13 to 8.15, 9.1 to 9.10, 9.13, 10.3, 10.4_

```csharp
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Service.LoanAccounts.Rules;
using FsCheck.Xunit;
using Xunit;

namespace Finnova.Tests.LoanAccounts;

public sealed class LoanAccountRulesProperties
{
    [Property(MaxTest = 100)] // Feature: application-account-creation, Property 8: Account status transition rule
    public bool TransitionRule(AccountStatus from, AccountStatus to)
    {
        var allowed = (from, to) is (AccountStatus.Active, AccountStatus.Inactive) or (AccountStatus.Active, AccountStatus.Frozen)
                                  or (AccountStatus.Inactive, AccountStatus.Active) or (AccountStatus.Frozen, AccountStatus.Active);
        return AccountStatusRules.IsTransitionAllowed(from, to) == allowed
            && AccountStatusRules.IsUpdatable(from) == (from != AccountStatus.Closed);
    }

    [Property(MaxTest = 100)] // Feature: application-account-creation, Property 12: Sensitive value masking
    public bool MaskingKeepsLengthAndRevealsAtMostLastFour(string? value)
    {
        var v = value ?? string.Empty;
        var m = PiiMasker.Mask(v);
        if (m.Length != v.Length) return false;
        var hiddenPrefix = Math.Max(0, v.Length - 4);
        return m[..hiddenPrefix].All(c => c == '*') && (v.Length <= 4 ? m.All(c => c == '*') : m.EndsWith(v[^4..], StringComparison.Ordinal));
    }

    [Property(MaxTest = 100)] // Feature: application-account-creation, Property 14: Dead-letter reprocess
    public bool ReprocessOnlyFromDeadLetter(AccountRequestStatus status, byte attempts)
    {
        var request = new AccountCreationRequest { Status = status, AttemptCount = attempts % 4, ClaimedBy = "w" };
        var can = RequestRecoveryRules.CanReprocess(status);
        if (can) RequestRecoveryRules.ResetForReprocess(request, DateTime.UtcNow);
        return can == (status == AccountRequestStatus.DeadLetter)
            && (!can || (request.Status == AccountRequestStatus.Pending && request.AttemptCount == 0 && request.ClaimedBy is null));
    }

    [Property(MaxTest = 100)] // Feature: application-account-creation, Property 19: Resolution input validation (submission)
    public bool SubmissionRule(bool flagged, bool hasPending, string? reason)
    {
        var len = reason?.Trim().Length ?? 0;
        var expected = flagged && !hasPending && len is >= 10 and <= 500;
        return (KycResolutionRules.CheckSubmit(flagged, hasPending, reason) == SubmitCheck.Ok) == expected;
    }

    [Property(MaxTest = 100)] // Feature: application-account-creation, Property 19: Resolution input validation (decision)
    public bool DecisionRule(KycResolutionStatus status, bool sameUser, string? comment)
    {
        var maker = Guid.NewGuid();
        var checker = sameUser ? maker : Guid.NewGuid();
        var expected = status == KycResolutionStatus.Pending && !sameUser && !string.IsNullOrWhiteSpace(comment);
        return (KycResolutionRules.CheckDecision(status, maker, checker, comment) == DecisionCheck.Ok) == expected;
    }

    [Property(MaxTest = 100)]
    // Feature: application-account-creation, Property 13: Four-eyes and Passed only on approval
    // Feature: application-account-creation, Property 18: At most one Pending resolution request per Account
    public bool ResolutionSequencesKeepTheInvariants(byte[] kinds, byte[] users)
    {
        var people = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var account = new LoanAccount { KycStatus = KycStatus.Failed, KycFlagged = true, KycSource = KycSource.Internal };
        var requests = new List<AccountKycResolutionRequest>();
        AccountKycResolutionRequest? pending = null;
        var approvedByAnotherUser = false;

        for (var i = 0; i < Math.Min(kinds.Length, users.Length); i++)
        {
            var actor = people[users[i] % 3];
            switch (kinds[i] % 3)
            {
                case 0: // maker submits
                    var reason = (kinds[i] & 4) != 0 ? "a valid reason text" : "short";
                    if (KycResolutionRules.CheckSubmit(account.KycFlagged, pending is not null, reason) == SubmitCheck.Ok)
                    {
                        pending = new AccountKycResolutionRequest { MakerUserId = actor, Reason = reason, Status = KycResolutionStatus.Pending };
                        requests.Add(pending);
                    }
                    break;
                default: // checker approves (1) or rejects (2)
                    if (pending is null) break;
                    if (KycResolutionRules.CheckDecision(pending.Status, pending.MakerUserId, actor, "comment") != DecisionCheck.Ok) break;
                    pending.CheckerUserId = actor;
                    if (kinds[i] % 3 == 1)
                    {
                        pending.Status = KycResolutionStatus.Approved;
                        KycResolutionRules.ApplyApproval(account, DateTime.UtcNow);
                        approvedByAnotherUser = true;
                    }
                    else pending.Status = KycResolutionStatus.Rejected;
                    pending = null;
                    break;
            }
            if (requests.Count(r => r.Status == KycResolutionStatus.Pending) > 1) return false;   // Property 18
        }

        var passedManually = account.KycStatus == KycStatus.Passed && account.KycSource == KycSource.Manual;
        return passedManually == approvedByAnotherUser                                              // Property 13
            && requests.All(r => r.CheckerUserId is null || r.CheckerUserId != r.MakerUserId)
            && account.KycFlagged == !passedManually;
    }
}
```

  - [ ] 14.4 CREATE `Finnova.Tests/LoanAccounts/SchemeResolverProperties.cs`
    - Property 6, an independent oracle versus `SchemeResolver`.
    - _Requirements: 4.2, 16.8_

```csharp
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Service.DocumentNumberControl.Internal;
using FsCheck.Xunit;

namespace Finnova.Tests.LoanAccounts;

public sealed class SchemeResolverProperties
{
    private static readonly Guid Lob = Guid.NewGuid();
    private static readonly Guid Branch = Guid.NewGuid();

    // level 0 LOB+Branch, 1 LOB only, 2 Branch only, 3 Global, 4 unrelated scope (other LOB and branch)
    private static NumberingScheme Make(int level, bool active, string documentType) => new()
    {
        Code = Guid.NewGuid().ToString("N")[..12], Name = "n", DocumentType = documentType, FormatTemplate = "{SEQ}", IsActive = active,
        Scope = level is 0 or 2 or 4 ? NumberScope.Branch : NumberScope.Global,
        ScopeId = level switch { 0 or 2 => Branch, 4 => Guid.NewGuid(), _ => null },
        LineOfBusinessId = level switch { 0 or 1 => Lob, 4 => Guid.NewGuid(), _ => null }
    };

    [Property(MaxTest = 200)] // Feature: application-account-creation, Property 6: Scheme resolution fallback
    public bool ReturnsFirstActiveSchemeInFallbackOrder(bool[] actives, byte[] levels, byte[] docs)
    {
        var n = new[] { actives.Length, levels.Length, docs.Length }.Min();
        var schemes = Enumerable.Range(0, n)
            .Select(i => Make(levels[i] % 5, actives[i], docs[i] % 2 == 0 ? "LoanAccount" : "Other")).ToList();

        var result = SchemeResolver.Resolve(schemes, "LoanAccount", Lob, Branch);

        NumberingScheme? expected = null;
        for (var level = 0; level < 4 && expected is null; level++)
        {
            var wanted = level;
            expected = schemes.FirstOrDefault(s => s.IsActive && s.DocumentType == "LoanAccount" && LevelOf(s) == wanted);
        }
        return result?.Scheme.Id == expected?.Id
            && (result is null || (result.Scheme.IsActive && result.Scheme.DocumentType == "LoanAccount"));
    }

    private static int LevelOf(NumberingScheme s) =>
        s.Scope == NumberScope.Branch && s.ScopeId == Branch && s.LineOfBusinessId == Lob ? 0
        : s.Scope == NumberScope.Global && s.ScopeId == null && s.LineOfBusinessId == Lob ? 1
        : s.Scope == NumberScope.Branch && s.ScopeId == Branch && s.LineOfBusinessId == null ? 2
        : s.Scope == NumberScope.Global && s.ScopeId == null && s.LineOfBusinessId == null ? 3 : 4;
}
```

  - [ ] 14.5 CREATE `Finnova.Tests/LoanAccounts/DncLobExtensionTests.cs`
    - Property 16 (token and scope consistency), Property 17 (independent sequences), plus rendering and length facts.
    - _Requirements: 16.2 to 16.7, 3.6, 4.9_

```csharp
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Numbering;
using Finnova.Service.DocumentNumberControl.Commands.IssueNumber;
using Finnova.Tests.Infrastructure;
using FsCheck.Xunit;
using Xunit;

namespace Finnova.Tests.LoanAccounts;

public sealed class DncLobExtensionTests
{
    [Property(MaxTest = 100)] // Feature: application-account-creation, Property 16: DNC token and scope consistency
    public bool TokensRequireMatchingScope(bool useLob, bool useBranch, bool hasLob, bool isBranch)
    {
        var template = "A" + (useLob ? "{LOB}" : "") + (useBranch ? "{BRANCH}" : "") + "{SEQ:5}";
        var expectedErrors = (useLob && !hasLob ? 1 : 0) + (useBranch && !isBranch ? 1 : 0);
        return NumberFormatter.Validate(template).Count == 0
            && NumberFormatter.ValidateScopeTokens(template, hasLob, isBranch).Count == expectedErrors;
    }

    private static NumberingScheme Make(string code, NumberScope scope, Guid? scopeId, Guid? lob) => new()
    {
        Code = code, Name = code, DocumentType = "LoanAccount", FormatTemplate = "{SEQ:6}", SeqPadding = 6,
        Scope = scope, ScopeId = scopeId, LineOfBusinessId = lob, IsActive = true
    };

    [Property(MaxTest = 100)] // Feature: application-account-creation, Property 17: Independent sequences per scope
    public bool CountersAreIndependentPerSchemeKey(byte[] picks)
    {
        var lob1 = Guid.NewGuid(); var lob2 = Guid.NewGuid(); var b1 = Guid.NewGuid(); var b2 = Guid.NewGuid();
        var repo = new InMemoryNumberingSchemeRepository(new[]
        {
            Make("S-LB", NumberScope.Branch, b1, lob1), Make("S-L", NumberScope.Global, null, lob1),
            Make("S-B", NumberScope.Branch, b1, null), Make("S-G", NumberScope.Global, null, null)
        });
        var handler = new IssueNumberCommandHandler(repo, new FakeNumberingReferences());
        var last = new Dictionary<Guid, long>();
        foreach (var p in picks)
        {
            var lob = (p & 1) == 0 ? lob1 : lob2;
            var branch = (p & 2) == 0 ? b1 : b2;
            var issued = handler.Handle(new IssueNumberCommand("LoanAccount", NumberScope.Global, null, "t", lob, branch, true), default)
                .GetAwaiter().GetResult();
            var expected = last.TryGetValue(issued.SchemeId, out var previous) ? previous + 1 : 1;
            if (issued.SequenceValue != expected) return false;
            last[issued.SchemeId] = issued.SequenceValue;
        }
        return true;
    }

    [Fact]
    public void LobAndBranchTokensAreRendered()
    {
        var utc = new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal("RETAIL-MUM01-0007",
            NumberFormatter.Format("{LOB}-{BRANCH}-{SEQ:4}", 7, 1, null, null, utc, "RETAIL", "MUM01"));
    }

    [Fact]
    public void LobTokenIsUpperCaseAlphanumericAndCapped()
    {
        Assert.Equal("RETAILLEND", LobTokenFormatter.ToToken("Retail Lending (India)"));
    }

    [Fact]
    public void MaxRenderedLengthIncludesPrefixSuffixAndTokenCeilings()
    {
        var length = NumberFormatter.MaxRenderedLength("{PREFIX}{LOB}{BRANCH}{SEQ:5}{SUFFIX}", 5, new string('P', 20), new string('S', 20));
        Assert.Equal(20 + 10 + 20 + 5 + 20, length);
    }

    [Fact]
    public async Task UnknownLineOfBusinessIsRejectedBeforeAnySequenceMoves()
    {
        var scheme = Make("S-G", NumberScope.Global, null, null);
        var repo = new InMemoryNumberingSchemeRepository(new[] { scheme });
        var handler = new IssueNumberCommandHandler(repo, new NoLobReferences());
        await Assert.ThrowsAsync<Finnova.Models.Domain.Exceptions.NumberingSchemeValidationException>(() =>
            handler.Handle(new IssueNumberCommand("LoanAccount", NumberScope.Global, null, "t", Guid.NewGuid(), Guid.NewGuid(), true), default));
        Assert.Equal(0, repo.Snapshot().Single().CurrentValue);
    }

    private sealed class NoLobReferences : Finnova.Repository.Interfaces.INumberingReferenceRepository
    {
        public Task<string?> GetLobTokenAsync(Guid id, CancellationToken ct) => Task.FromResult<string?>(null);
        public Task<string?> GetBranchCodeAsync(Guid id, CancellationToken ct) => Task.FromResult<string?>("BR");
    }
}
```

  - [ ] 14.6 CREATE `Finnova.Tests/LoanAccounts/MappingAndRoleProperties.cs`
    - Property 20 (Customer Master mapping profile) and Property 21 (idempotent role seeding), plus the registry facts.
    - _Requirements: 17.1 to 17.6, 18.1 to 18.3_

```csharp
using Finnova.Models.Contracts.Internal;
using Finnova.Models.Domain.Roles;
using Finnova.Service.IdentityVerification.Mapping;
using FsCheck.Xunit;
using Xunit;

namespace Finnova.Tests.LoanAccounts;

public sealed class MappingAndRoleProperties
{
    private static string? At(string?[] parts, int i) => i < parts.Length ? parts[i] : null;

    [Property(MaxTest = 100)] // Feature: application-account-creation, Property 20: Customer Master mapping profile
    public bool MappingIsDeterministicAndJoinsNonBlankParts(string?[] nameParts, string?[] addressParts)
    {
        var name = new CustomerNameParts(At(nameParts, 0), At(nameParts, 1), At(nameParts, 2), At(nameParts, 3), At(nameParts, 4), At(nameParts, 5));
        var address = new CustomerAddressParts(At(addressParts, 0), At(addressParts, 1), At(addressParts, 2), At(addressParts, 3),
            At(addressParts, 4), At(addressParts, 5), At(addressParts, 6), At(addressParts, 7), At(addressParts, 8));

        var first = CustomerMasterMappingProfile.Map(name, address);
        var second = CustomerMasterMappingProfile.Map(name, address);

        static string Expected(string separator, IEnumerable<string?> parts) =>
            string.Join(separator, parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));
        var expectedName = Expected(" ", new[] { name.First, name.Second, name.Third, name.Fourth, name.Fifth, name.Sixth });
        var expectedAddress = Expected(", ", new[] { address.House, address.Block, address.Way, address.Area, address.Landmark,
            address.City, address.State, address.PostalCode, address.Country });
        return first == second && first.Name == expectedName && first.Address == expectedAddress;
    }

    [Fact]
    public void MissingAddressMapsToAnEmptyValue()
        => Assert.Equal(string.Empty, CustomerMasterMappingProfile.Map(new CustomerNameParts("A", null, null, null, null, null), null).Address);

    [Fact]
    public void RegistryKnowsTheCustomerMasterProfileOnly()
    {
        Assert.True(MappingProfileRegistry.Contains("customer-master"));
        Assert.False(MappingProfileRegistry.Contains("unknown-profile"));
    }

    [Property(MaxTest = 100)] // Feature: application-account-creation, Property 21: Idempotent role seeding
    public bool SeedingIsIdempotentAndPreservesExistingRoles(bool[] present, string[] others, byte repeats)
    {
        var existing = AppRoleCatalog.Roles.Where((_, i) => i < present.Length && present[i]).Select(r => r.Name)
            .Concat(others.Where(o => !string.IsNullOrWhiteSpace(o) && !AppRoleCatalog.Roles.Any(r => r.Name.Equals(o, StringComparison.OrdinalIgnoreCase))).Distinct())
            .ToList();

        IReadOnlyList<string> current = existing;
        for (var i = 0; i < 1 + repeats % 3; i++) current = AppRoleSeeder.Apply(current);

        return AppRoleCatalog.Roles.All(r => current.Count(n => n.Equals(r.Name, StringComparison.OrdinalIgnoreCase)) == 1)
            && existing.All(current.Contains)
            && current.Count == existing.Count + AppRoleSeeder.MissingRoles(existing).Count;
    }
}
```

  - [ ] 14.7 CREATE `Finnova.Tests/LoanAccounts/LoanAccountHandlerTests.cs`
    - Handler-level tests on the InMemory database: update semantics (Property 9), no-op, closed, concurrency, list search/filter/ordering/pagination (Properties 10 and 11), maker-checker, re-check, reprocess, and the process-application outbox.
    - _Requirements: 1.1, 1.4, 8.1 to 8.18, 9.1 to 9.11, 10.3, 10.4_

```csharp
using Finnova.Models.Contracts.LoanAccounts;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Context;
using Finnova.Repository.Repositories;
using Finnova.Service.Applications.Commands;
using Finnova.Service.Applications.Financial;
using Finnova.Service.Applications.Ports;
using Finnova.Service.LoanAccounts;
using Finnova.Service.LoanAccounts.Commands;
using Finnova.Service.LoanAccounts.Queries;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using static Finnova.Tests.LoanAccounts.LoanAccountTestData;

namespace Finnova.Tests.LoanAccounts;

public sealed class LoanAccountHandlerTests
{
    private static readonly IOptions<AccountCreationKycOptions> Opts = Options.Create(new AccountCreationKycOptions());

    private static UpdateLoanAccountCommandHandler UpdateHandler(FinnovaDbContext db) =>
        new(new LoanAccountRepository(db), new AccountAuditRepository(db), new ApplicationUnitOfWork(db), Opts);
    private static SubmitKycResolutionCommandHandler SubmitHandler(FinnovaDbContext db) =>
        new(new LoanAccountRepository(db), new AccountKycResolutionRepository(db), new AccountAuditRepository(db), new ApplicationUnitOfWork(db));
    private static DecideKycResolutionCommandHandler DecideHandler(FinnovaDbContext db) =>
        new(new LoanAccountRepository(db), new AccountKycResolutionRepository(db), new AccountKycHistoryRepository(db),
            new AccountAuditRepository(db), new ApplicationUnitOfWork(db), Opts);
    private static GetLoanAccountsQueryHandler ListHandler(FinnovaDbContext db) => new(new LoanAccountRepository(db));
    private static LoanAccountSearchRequest Search(string? term = null, int page = 1, int size = 20,
        KycStatus? kyc = null, bool? flagged = null, AccountStatus? status = null, bool? pending = null) =>
        new(term, kyc, flagged, status, null, null, null, null, pending, page, size);

    // ------------------------------------------------------------------ update (Property 9)
    [Fact]
    public async Task Update_changes_name_and_refreshes_UpdatedAt_and_writes_audit()
    {
        using var db = NewDb();
        var acc = SeedLoan(db, "APP-1", "LN-1", "C-1", "Asha Rao");
        var before = acc.UpdatedAt;
        var result = await UpdateHandler(db).Handle(new UpdateLoanAccountCommand(acc.Id, "New Name", null, RowVersionOf(db, acc.Id), "u1", "c1"), default);
        Assert.Equal("New Name", result.AccountName);
        Assert.True(db.LoanAccounts.AsNoTracking().First(a => a.Id == acc.Id).UpdatedAt > before);
        Assert.Single(db.AccountAuditEntries);
    }

    [Fact]
    public async Task Update_with_equal_values_is_a_noop()
    {
        using var db = NewDb();
        var acc = SeedLoan(db, "APP-1", "LN-1", "C-1", "Asha Rao");
        var before = acc.UpdatedAt;
        await UpdateHandler(db).Handle(new UpdateLoanAccountCommand(acc.Id, acc.AccountName, "Active", RowVersionOf(db, acc.Id), "u1", "c1"), default);
        Assert.Equal(before, db.LoanAccounts.AsNoTracking().First(a => a.Id == acc.Id).UpdatedAt);
        Assert.Empty(db.AccountAuditEntries);
    }

    [Fact]
    public async Task Update_never_changes_immutable_fields()
    {
        using var db = NewDb();
        var acc = SeedLoan(db, "APP-1", "LN-1", "C-1", "Asha Rao");
        await UpdateHandler(db).Handle(new UpdateLoanAccountCommand(acc.Id, "Other", "Frozen", RowVersionOf(db, acc.Id), "u1", "c1"), default);
        var stored = db.LoanAccounts.AsNoTracking().First(a => a.Id == acc.Id);
        Assert.Equal("LN-1", stored.AccountNumber);
        Assert.Equal(AccountType.Loan, stored.Type);
        Assert.Equal(acc.ApplicationId, stored.ApplicationId);
        Assert.Equal(0m, stored.Balance);
        Assert.Equal("INR", stored.Currency);
    }

    [Fact]
    public async Task Update_rejects_closed_account_and_forbidden_transition_and_stale_row_version()
    {
        using var db = NewDb();
        var closed = SeedLoan(db, "APP-1", "LN-1", "C-1", "A");
        closed.Status = AccountStatus.Closed; db.SaveChanges();
        await Assert.ThrowsAsync<LoanAccountConflictException>(() =>
            UpdateHandler(db).Handle(new UpdateLoanAccountCommand(closed.Id, "X", null, RowVersionOf(db, closed.Id), "u", "c"), default));

        var active = SeedLoan(db, "APP-2", "LN-2", "C-2", "B");
        await Assert.ThrowsAsync<LoanAccountConflictException>(() =>
            UpdateHandler(db).Handle(new UpdateLoanAccountCommand(active.Id, null, "Closed", RowVersionOf(db, active.Id), "u", "c"), default));
        await Assert.ThrowsAsync<LoanAccountConflictException>(() =>
            UpdateHandler(db).Handle(new UpdateLoanAccountCommand(active.Id, "X", null, Convert.ToBase64String(new byte[] { 1, 2, 3 }), "u", "c"), default));
        Assert.Equal(AccountStatus.Active, db.LoanAccounts.AsNoTracking().First(a => a.Id == active.Id).Status);
    }

    [Fact]
    public async Task Unknown_account_is_not_found()
        => await Assert.ThrowsAsync<LoanAccountNotFoundException>(() =>
            new GetLoanAccountByIdQueryHandler(new LoanAccountRepository(NewDb()), Opts).Handle(new GetLoanAccountByIdQuery(Guid.NewGuid()), default));

    // ------------------------------------------------------------------ list (Properties 10 and 11)
    [Fact]
    public async Task List_searches_case_insensitively_and_combines_filters()
    {
        using var db = NewDb();
        SeedLoan(db, "APP-100", "LN-AAA", "CUST-1", "Asha Rao", flagged: true, kyc: KycStatus.Failed);
        SeedLoan(db, "APP-200", "LN-BBB", "CUST-2", "Vikram Shah", flagged: false, kyc: KycStatus.Passed);
        var h = ListHandler(db);

        Assert.Single((await h.Handle(new GetLoanAccountsQuery(Search("app-100")), default)).Data);
        Assert.Single((await h.Handle(new GetLoanAccountsQuery(Search("ln-bbb")), default)).Data);
        Assert.Single((await h.Handle(new GetLoanAccountsQuery(Search("vikram")), default)).Data);
        Assert.Single((await h.Handle(new GetLoanAccountsQuery(Search("cust-1", flagged: true)), default)).Data);
        Assert.Empty((await h.Handle(new GetLoanAccountsQuery(Search("cust-1", flagged: false)), default)).Data);
        Assert.Single((await h.Handle(new GetLoanAccountsQuery(Search(kyc: KycStatus.Passed)), default)).Data);
    }

    [Property(MaxTest = 100)] // Feature: application-account-creation, Property 11: Pagination and ordering
    public bool PaginationOrdersByCreatedDescendingThenAccountNumber(byte count, byte pageSeed, byte sizeSeed)
    {
        var n = count % 12; var page = pageSeed % 5 + 1; var size = sizeSeed % 5 + 1;
        using var db = NewDb();
        var baseTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < n; i++)
            SeedLoan(db, $"APP-{i:D3}", $"LN-{i:D3}", $"C-{i:D3}", $"Name {i}", createdAt: baseTime.AddMinutes(i % 4));   // ties exercise the second key

        var result = ListHandler(db).Handle(new GetLoanAccountsQuery(Search(page: page, size: size)), default).GetAwaiter().GetResult();
        var expected = Enumerable.Range(0, n).Select(i => (Created: baseTime.AddMinutes(i % 4), Number: $"LN-{i:D3}"))
            .OrderByDescending(x => x.Created).ThenBy(x => x.Number, StringComparer.Ordinal)
            .Skip((page - 1) * size).Take(size).Select(x => x.Number).ToList();
        return result.Data.Select(d => d.AccountNumber).SequenceEqual(expected)
            && result.Total == n && result.Page == page && result.PageSize == size && result.Data.Count <= size;
    }

    [Property(MaxTest = 100)] // Feature: application-account-creation, Property 10: List search and filters
    public bool SearchReturnsExactlyTheMatchingAccounts(string? term)
    {
        // ASCII only: SQL Server collation and .NET invariant casing differ for exotic Unicode, which is not what this property tests.
        var t = new string((term ?? string.Empty).Where(ch => ch < 128 && !char.IsControl(ch)).ToArray()).Trim();
        if (t.Length > 100) t = t[..100].Trim();
        using var db = NewDb();
        var seeded = new[]
        {
            SeedLoan(db, "APP-ONE", "LN-ONE", "CUST-A", "Asha Rao"),
            SeedLoan(db, "APP-TWO", "LN-TWO", "CUST-B", "Vikram Shah"),
            SeedLoan(db, "APP-3", "LN-3", "CUST-C", "Priya Nair")
        };
        var result = ListHandler(db).Handle(new GetLoanAccountsQuery(Search(t)), default).GetAwaiter().GetResult();
        var apps = db.Applications.AsNoTracking().ToDictionary(a => a.Id, a => a.ApplicationNumber);
        var customers = db.Customers.AsNoTracking().ToDictionary(c => c.Id, c => c.CustomerCode);
        var expected = seeded.Where(a => t.Length == 0
            || apps[a.ApplicationId!.Value].Contains(t, StringComparison.OrdinalIgnoreCase)
            || a.AccountNumber.Contains(t, StringComparison.OrdinalIgnoreCase)
            || customers[a.CustomerId!.Value].Contains(t, StringComparison.OrdinalIgnoreCase)
            || (a.CustomerNameSnapshot ?? "").Contains(t, StringComparison.OrdinalIgnoreCase))
            .Select(a => a.AccountNumber).OrderBy(x => x).ToList();
        return result.Data.Select(d => d.AccountNumber).OrderBy(x => x).SequenceEqual(expected);
    }

    // ------------------------------------------------------------------ maker-checker
    [Fact]
    public async Task Maker_checker_flow_clears_the_flag_only_on_approval_by_another_user()
    {
        using var db = NewDb();
        var acc = SeedLoan(db, "APP-1", "LN-1", "C-1", "Asha Rao");
        var maker = Guid.NewGuid(); var checker = Guid.NewGuid();

        var created = await SubmitHandler(db).Handle(new SubmitKycResolutionCommand(acc.Id, "Verified originals at the branch", maker, "Maker One", "c"), default);
        Assert.Equal("Pending", created.Status);
        var stored = db.LoanAccounts.AsNoTracking().First(a => a.Id == acc.Id);
        Assert.True(stored.KycFlagged);                       // unchanged by the maker step (R9.1)
        Assert.Equal(KycStatus.Failed, stored.KycStatus);

        await Assert.ThrowsAsync<LoanAccountConflictException>(() =>        // second pending request refused (R9.9)
            SubmitHandler(db).Handle(new SubmitKycResolutionCommand(acc.Id, "Another valid reason here", maker, "Maker One", "c"), default));
        await Assert.ThrowsAsync<KycResolutionForbiddenException>(() =>     // four-eyes (R9.4)
            DecideHandler(db).Handle(new DecideKycResolutionCommand(acc.Id, created.Id, true, "ok", maker, "Maker One", "c"), default));

        var approved = await DecideHandler(db).Handle(new DecideKycResolutionCommand(acc.Id, created.Id, true, "Documents match", checker, "Checker One", "c"), default);
        Assert.Equal("Passed", approved.KycStatus);
        Assert.False(approved.KycFlagged);
        Assert.Equal("Manual", approved.KycSource);
        Assert.Null(approved.PendingKycResolution);
        Assert.Single(approved.KycHistory);
        Assert.Equal("Approved", Assert.Single(approved.KycResolutionRequests).Status);

        await Assert.ThrowsAsync<LoanAccountConflictException>(() =>
            DecideHandler(db).Handle(new DecideKycResolutionCommand(acc.Id, created.Id, false, "again", checker, "Checker One", "c"), default));
    }

    [Fact]
    public async Task Rejection_keeps_the_flag_and_allows_a_new_request()
    {
        using var db = NewDb();
        var acc = SeedLoan(db, "APP-1", "LN-1", "C-1", "Asha Rao");
        var created = await SubmitHandler(db).Handle(new SubmitKycResolutionCommand(acc.Id, "Verified originals at the branch", Guid.NewGuid(), "M", "c"), default);
        var view = await DecideHandler(db).Handle(new DecideKycResolutionCommand(acc.Id, created.Id, false, "Not convincing", Guid.NewGuid(), "C", "c"), default);

        Assert.True(view.KycFlagged);
        Assert.Equal("Failed", view.KycStatus);
        Assert.Equal("Rejected", Assert.Single(view.KycResolutionRequests).Status);
        await SubmitHandler(db).Handle(new SubmitKycResolutionCommand(acc.Id, "Second attempt with documents", Guid.NewGuid(), "M", "c"), default);
    }

    [Fact]
    public async Task Submission_is_refused_for_unflagged_accounts_and_short_reasons()
    {
        using var db = NewDb();
        var clean = SeedLoan(db, "APP-1", "LN-1", "C-1", "A", flagged: false, kyc: KycStatus.Passed);
        await Assert.ThrowsAsync<LoanAccountConflictException>(() =>
            SubmitHandler(db).Handle(new SubmitKycResolutionCommand(clean.Id, "A perfectly valid reason", Guid.NewGuid(), "M", "c"), default));
        var flagged = SeedLoan(db, "APP-2", "LN-2", "C-2", "B");
        await Assert.ThrowsAsync<LoanAccountValidationException>(() =>
            SubmitHandler(db).Handle(new SubmitKycResolutionCommand(flagged.Id, "short", Guid.NewGuid(), "M", "c"), default));
    }

    [Fact]
    public async Task Recheck_rearms_the_kyc_job_for_flagged_accounts_only()
    {
        using var db = NewDb();
        var flagged = SeedLoan(db, "APP-1", "LN-1", "C-1", "A");
        var handler = new RequestKycRecheckCommandHandler(new LoanAccountRepository(db), new AccountKycJobRepository(db), new AccountAuditRepository(db), new ApplicationUnitOfWork(db));
        await handler.Handle(new RequestKycRecheckCommand(flagged.Id, "documents updated", "u1", "c"), default);
        var job = Assert.Single(db.AccountKycJobs);
        Assert.Equal(AccountRequestStatus.Pending, job.Status);

        var clean = SeedLoan(db, "APP-2", "LN-2", "C-2", "B", flagged: false, kyc: KycStatus.Passed);
        await Assert.ThrowsAsync<LoanAccountConflictException>(() => handler.Handle(new RequestKycRecheckCommand(clean.Id, null, "u1", "c"), default));
    }

    [Fact]
    public async Task Reprocess_resets_only_dead_lettered_requests()
    {
        using var db = NewDb();
        var dead = new AccountCreationRequest { ApplicationId = Guid.NewGuid(), ApplicationNumber = "A1", CorrelationId = "c", Status = AccountRequestStatus.DeadLetter, AttemptCount = 3 };
        var pending = new AccountCreationRequest { ApplicationId = Guid.NewGuid(), ApplicationNumber = "A2", CorrelationId = "c", Status = AccountRequestStatus.Pending };
        db.AccountCreationRequests.AddRange(dead, pending); db.SaveChanges();
        var handler = new ReprocessAccountRequestCommandHandler(new AccountCreationRequestRepository(db), new AccountAuditRepository(db), new ApplicationUnitOfWork(db));

        await handler.Handle(new ReprocessAccountRequestCommand(dead.Id, "scheme created", "ops", "c"), default);
        Assert.Equal(AccountRequestStatus.Pending, db.AccountCreationRequests.AsNoTracking().First(x => x.Id == dead.Id).Status);
        Assert.Equal(0, db.AccountCreationRequests.AsNoTracking().First(x => x.Id == dead.Id).AttemptCount);
        await Assert.ThrowsAsync<LoanAccountConflictException>(() =>
            handler.Handle(new ReprocessAccountRequestCommand(pending.Id, "x", "ops", "c"), default));
    }

    // ------------------------------------------------------------------ outbox (R1)
    [Fact]
    public async Task Processing_an_application_inserts_exactly_one_account_creation_request()
    {
        using var db = NewDb();
        var app = new Application
        {
            ApplicationNumber = "APP-9", CustomerId = Guid.NewGuid(), LineOfBusinessId = Guid.NewGuid(), BranchId = Guid.NewGuid(),
            ProductCode = "HL", SchemeCode = "S1", FinanceAmount = 100000m, IrrPercent = 12m, Tenure = 12,
            RepaymentFrequency = RepaymentFrequency.Monthly, DisbursementDate = new DateOnly(2025, 1, 31), Status = ApplicationStatus.Submitted
        };
        db.Applications.Add(app); db.SaveChanges();
        var handler = new ProcessApplicationCommandHandler(
            new ApplicationRepository(db), new ApplicationScheduleRepository(db), new ApplicationStatusAuditRepository(db),
            new ApplicationUnitOfWork(db), new Mock<IApplicationReferenceValidator>().Object, new AmortizationCalculator(),
            new AccountCreationRequestRepository(db));
        var user = Guid.NewGuid();

        await handler.Handle(new ProcessApplicationCommand(app.Id, user.ToString()), default);
        var request = Assert.Single(db.AccountCreationRequests);
        Assert.Equal(app.Id, request.ApplicationId);
        Assert.Equal("APP-9", request.ApplicationNumber);
        Assert.Equal(AccountRequestStatus.Pending, request.Status);
        Assert.Equal(0, request.AttemptCount);
        Assert.Equal(user, request.InitiatedByUserId);

        await Assert.ThrowsAsync<ApplicationAlreadyProcessedException>(() => handler.Handle(new ProcessApplicationCommand(app.Id, user.ToString()), default));
        Assert.Single(db.AccountCreationRequests);
    }

    [Fact]
    public void Account_creation_request_has_a_unique_application_index_and_pending_resolution_index_is_filtered()
    {
        using var db = NewDb();
        var request = db.Model.FindEntityType(typeof(AccountCreationRequest))!;
        Assert.Contains(request.GetIndexes(), i => i.IsUnique && i.Properties.Count == 1 && i.Properties[0].Name == nameof(AccountCreationRequest.ApplicationId));
        var resolution = db.Model.FindEntityType(typeof(AccountKycResolutionRequest))!;
        Assert.Contains(resolution.GetIndexes(), i => i.IsUnique && i.GetFilter() == "[Status] = 'Pending'");
    }
}
```

  - [ ] 14.8 CREATE `Finnova.Tests/LoanAccounts/LoanAccountsApiFactory.cs` and `Finnova.Tests/LoanAccounts/LoanAccountsApiTests.cs`
    - `WebApplicationFactory` coverage: 401 before 403, 403 for wrong roles, list, 404 code, and the maker-checker flow over HTTP. Requires `FINNOVA_TEST_JWT_KEY` (set by the existing `Finnova.Tests.runsettings`).
    - _Requirements: 8.10, 9.4, 9.12, 11.1 to 11.3, 11.6, 15.3_

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Finnova.Repository.Context;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace Finnova.Tests.LoanAccounts;

public sealed class LoanAccountsApiFactory : WebApplicationFactory<Finnova.AccountsService.Controllers.LoanAccountsController>
{
    private readonly string _dbName = "loan-accounts-api-" + Guid.NewGuid();
    private static string Key => Environment.GetEnvironmentVariable("FINNOVA_TEST_JWT_KEY")
        ?? throw new InvalidOperationException("FINNOVA_TEST_JWT_KEY must be set for integration tests.");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "Finnova", ["Jwt:Audience"] = "FinnovaTests", ["Jwt:SigningKey"] = Key,
            ["ApplicationProcessing:AuthorizationPolicy"] = "ApplicationProcessor",
            ["ApplicationProcessing:LookupModule"] = "ApplicationProcessing",
            ["ApplicationProcessing:ProductLookupType"] = "PRODUCT",
            ["ApplicationProcessing:SchemeLookupType"] = "SCHEME"
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<FinnovaDbContext>>();
            services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<FinnovaDbContext>>();
            services.AddDbContext<FinnovaDbContext>(o => o.UseInMemoryDatabase(_dbName));
        });
    }

    public void Seed(Action<FinnovaDbContext> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FinnovaDbContext>();
        action(db);
        db.SaveChanges();
    }

    public HttpClient ClientFor(Guid? userId, params string[] roles)
    {
        var client = CreateClient();
        if (userId is null) return client;
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.Value.ToString()), new(ClaimTypes.Name, "User " + userId.Value.ToString("N")[..6]) };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        var token = new JwtSecurityToken("Finnova", "FinnovaTests", claims, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(10),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }
}
```

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Finnova.Tests.LoanAccounts;

public sealed class LoanAccountsApiTests : IClassFixture<LoanAccountsApiFactory>
{
    private readonly LoanAccountsApiFactory _factory;
    public LoanAccountsApiTests(LoanAccountsApiFactory factory) => _factory = factory;

    private Guid SeedFlaggedAccount()
    {
        Guid id = default;
        _factory.Seed(db => id = LoanAccountTestData.SeedLoan(db, "APP-" + Guid.NewGuid().ToString("N")[..6], "LN-" + Guid.NewGuid().ToString("N")[..6], "C-" + Guid.NewGuid().ToString("N")[..4], "Asha Rao").Id);
        return id;
    }

    [Fact] public async Task Missing_token_returns_401()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("/api/loan-accounts")).StatusCode);

    [Fact] public async Task Invalid_token_returns_401_before_403()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/loan-accounts")).StatusCode);
    }

    [Fact] public async Task Authenticated_user_without_a_role_returns_403()
        => Assert.Equal(HttpStatusCode.Forbidden, (await _factory.ClientFor(Guid.NewGuid(), "OtherRole").GetAsync("/api/loan-accounts")).StatusCode);

    [Fact] public async Task Viewer_can_list_and_view_but_cannot_update()
    {
        var id = SeedFlaggedAccount();
        var viewer = _factory.ClientFor(Guid.NewGuid(), "AccountsViewer");
        var list = await viewer.GetAsync("/api/loan-accounts?pageSize=100");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync($"/api/loan-accounts/{id}")).StatusCode);
        var put = await viewer.PutAsJsonAsync($"/api/loan-accounts/{id}", new { accountName = "x", rowVersion = "" });
        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
    }

    [Fact] public async Task Unknown_account_returns_404_with_stable_code()
    {
        var response = await _factory.ClientFor(Guid.NewGuid(), "AccountsViewer").GetAsync($"/api/loan-accounts/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("ERR-ACC-404", await response.Content.ReadAsStringAsync());
    }

    [Fact] public async Task Invalid_page_size_returns_400_with_stable_code()
    {
        var response = await _factory.ClientFor(Guid.NewGuid(), "AccountsViewer").GetAsync("/api/loan-accounts?pageSize=101");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("ERR-ACC-400", await response.Content.ReadAsStringAsync());
    }

    [Fact] public async Task Maker_checker_over_http_enforces_roles_and_four_eyes()
    {
        var id = SeedFlaggedAccount();
        var makerAndApprover = Guid.NewGuid();
        var maker = _factory.ClientFor(makerAndApprover, "ApplicationProcessor", "KycApprover");

        var submit = await maker.PostAsJsonAsync($"/api/loan-accounts/{id}/kyc/resolution-requests", new { reason = "Verified originals at the branch" });
        Assert.Equal(HttpStatusCode.Created, submit.StatusCode);
        var requestId = (await submit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var approveUrl = $"/api/loan-accounts/{id}/kyc/resolution-requests/{requestId}/approve";

        var self = await maker.PostAsJsonAsync(approveUrl, new { comment = "self approval" });
        Assert.Equal(HttpStatusCode.Forbidden, self.StatusCode);
        Assert.Contains("ERR-ACC-403", await self.Content.ReadAsStringAsync());

        var processorOnly = _factory.ClientFor(Guid.NewGuid(), "ApplicationProcessor");
        Assert.Equal(HttpStatusCode.Forbidden, (await processorOnly.PostAsJsonAsync(approveUrl, new { comment = "no approver role" })).StatusCode);

        var duplicate = await maker.PostAsJsonAsync($"/api/loan-accounts/{id}/kyc/resolution-requests", new { reason = "Another valid reason" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var approver = _factory.ClientFor(Guid.NewGuid(), "KycApprover");
        var approved = await approver.PostAsJsonAsync(approveUrl, new { comment = "Documents match" });
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var body = await approved.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Passed", body.GetProperty("kycStatus").GetString());
        Assert.False(body.GetProperty("kycFlagged").GetBoolean());
    }

    [Fact] public async Task Approval_without_a_comment_returns_400()
    {
        var id = SeedFlaggedAccount();
        var maker = _factory.ClientFor(Guid.NewGuid(), "ApplicationProcessor");
        var submit = await maker.PostAsJsonAsync($"/api/loan-accounts/{id}/kyc/resolution-requests", new { reason = "Verified originals at the branch" });
        var requestId = (await submit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var response = await _factory.ClientFor(Guid.NewGuid(), "KycApprover")
            .PostAsJsonAsync($"/api/loan-accounts/{id}/kyc/resolution-requests/{requestId}/approve", new { comment = "  " });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact] public async Task Operations_require_the_operator_role()
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await _factory.ClientFor(Guid.NewGuid(), "AccountsViewer").GetAsync("/api/loan-accounts/operations/queue-status")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _factory.ClientFor(Guid.NewGuid(), "AccountsOperator").GetAsync("/api/loan-accounts/operations/queue-status")).StatusCode);
    }
}
```

  - [ ] 14.9 CREATE `Finnova.Tests/LoanAccounts/InternalEndpointsTests.cs`
    - SystemAdmin host: service token, internal DNC issuance with LOB and Branch tokens and fallback, authorization, and the forwarded-header guard.
    - _Requirements: 4.1, 4.2, 4.4, 16.8, 16.10, 16.12_

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Context;
using Finnova.Service.Auth;
using Finnova.Tests.Integration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Finnova.Tests.LoanAccounts;

public sealed class InternalEndpointsTests : IClassFixture<SystemAdminAppFactory>
{
    private static readonly Guid ClientId = new("00000000-0000-0000-0004-000000000001");
    private const string Secret = "unit-test-service-secret";
    private static readonly Guid LobId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private readonly SystemAdminAppFactory _factory;

    public InternalEndpointsTests(SystemAdminAppFactory factory)
    {
        _factory = factory;
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FinnovaDbContext>();
        if (db.ServiceCredentials.Any(x => x.ClientId == ClientId)) return;
        db.ServiceCredentials.Add(new ServiceCredential { ClientId = ClientId, SecretHash = PasswordHasher.Hash(Secret), IsActive = true });
        db.LinesOfBusiness.Add(new LineOfBusiness { Id = LobId, LOB_Name = "Retail Lending", LOB_Description = "d", IsActive = true });
        db.Locations.Add(new Location { Id = BranchId, Code = "MUM01", Name = "Mumbai", Level = 5, IsActive = true });
        db.NumberingSchemes.Add(new NumberingScheme
        {
            Code = "LOAN-ACCT-TEST", Name = "Loan Account", DocumentType = "LoanAccount", FormatTemplate = "{LOB}-{BRANCH}-{SEQ:4}",
            SeqPadding = 4, Scope = NumberScope.Branch, ScopeId = BranchId, LineOfBusinessId = LobId, IsActive = true
        });
        db.SaveChanges();
    }

    private async Task<HttpClient> ServiceClientAsync()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/internal/service-auth/token", new { clientId = ClientId, clientSecret = Secret });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static object IssueBody(string documentType = "LoanAccount", Guid? lob = null, Guid? branch = null) =>
        new { documentType, lineOfBusinessId = lob ?? LobId, branchId = branch ?? BranchId, correlationId = "c1" };

    [Fact] public async Task Issue_without_a_token_returns_401()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().PostAsJsonAsync("/internal/dcn/issue", IssueBody())).StatusCode);

    [Fact] public async Task Issue_rejects_a_user_token_without_the_service_permission()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", DevJwt.SystemAdmin());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/internal/dcn/issue", IssueBody())).StatusCode);
    }

    [Fact] public async Task Token_endpoint_rejects_a_wrong_secret()
        => Assert.Equal(HttpStatusCode.Unauthorized,
            (await _factory.CreateClient().PostAsJsonAsync("/internal/service-auth/token", new { clientId = ClientId, clientSecret = "wrong" })).StatusCode);

    [Fact] public async Task Service_token_issues_a_formatted_number_from_the_resolved_scheme()
    {
        var client = await ServiceClientAsync();
        var first = await client.PostAsJsonAsync("/internal/dcn/issue", IssueBody());
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var body = await first.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("RETAILLEND-MUM01-0001", body.GetProperty("issuedNumber").GetString());
        Assert.Equal("LOAN-ACCT-TEST", body.GetProperty("resolvedSchemeCode").GetString());
        Assert.Equal("LobAndBranch", body.GetProperty("resolvedScopeLevel").GetString());

        var second = await (await client.PostAsJsonAsync("/internal/dcn/issue", IssueBody())).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("RETAILLEND-MUM01-0002", second.GetProperty("issuedNumber").GetString());
    }

    [Fact] public async Task Unknown_document_type_returns_404_and_unknown_branch_returns_400()
    {
        var client = await ServiceClientAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/internal/dcn/issue", IssueBody("NoSuchType"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/internal/dcn/issue", IssueBody(branch: Guid.NewGuid()))).StatusCode);
    }

    [Fact] public async Task Internal_endpoints_are_hidden_when_forwarded_headers_are_present()
    {
        var client = await ServiceClientAsync();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.0.0.1");
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/internal/dcn/issue", IssueBody())).StatusCode);
    }

    [Fact] public async Task Kyc_verify_rejects_unauthenticated_callers()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().PostAsJsonAsync("/internal/kyc/verify", new { })).StatusCode);
}
```

  - [ ] 14.10 CREATE `Finnova.Tests/LoanAccounts/RoleSeedingAndGatewayTests.cs`
    - Token role claims, idempotent `HasData` seeding, KYC result mapping, and gateway configuration.
    - _Requirements: 12.1, 12.2, 12.3, 12.4, 15.6, 18.1 to 18.4_

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Roles;
using Finnova.Service.Auth;
using Finnova.Service.LoanAccounts.Kyc;
using Microsoft.Extensions.Options;
using Xunit;

namespace Finnova.Tests.LoanAccounts;

public sealed class RoleSeedingAndGatewayTests
{
    [Fact]
    public void Token_service_emits_assigned_roles_as_claims()
    {
        var service = new TokenService(Options.Create(new JwtSettings { Secret = "unit-test-secret-key-that-is-long-enough-32", Issuer = "Finnova", Audience = "FinnovaClients" }));
        var (token, _) = service.GenerateToken(new UserAccount { Name = "U", Role = UserRole.User }, new[] { "KycApprover", "AccountsViewer" });
        var values = new JwtSecurityTokenHandler().ReadJwtToken(token).Claims.Select(c => c.Value).ToList();
        Assert.Contains("KycApprover", values);
        Assert.Contains("AccountsViewer", values);
        Assert.DoesNotContain("SystemAdmin", values);
    }

    [Fact]
    public void Original_token_overload_still_works_without_extra_roles()
    {
        var service = new TokenService(Options.Create(new JwtSettings { Secret = "unit-test-secret-key-that-is-long-enough-32" }));
        var (token, _) = service.GenerateToken(new UserAccount { Name = "U", Role = UserRole.User });
        Assert.False(string.IsNullOrEmpty(token));
    }

    [Fact]
    public void Seeding_creates_the_four_roles_exactly_once_even_when_applied_twice()
    {
        using var db = LoanAccountTestData.NewDb();
        db.Database.EnsureCreated();
        db.Database.EnsureCreated();
        Assert.Equal(4, db.AppRoles.Count());
        foreach (var role in AppRoleCatalog.Roles) Assert.Single(db.AppRoles.Where(r => r.Name == role.Name));
    }

    [Theory]
    [InlineData(ValidationStatus.Success, KycCheckResult.Passed)]
    [InlineData(ValidationStatus.Failure, KycCheckResult.Failed)]
    [InlineData(ValidationStatus.Unavailable, KycCheckResult.Unavailable)]
    public void Validation_status_maps_to_check_result(ValidationStatus input, KycCheckResult expected)
        => Assert.Equal(expected, KycResultMapper.FromValidation(input));

    [Theory]
    [InlineData(ComparisonOutcome.Match, KycCheckResult.Passed)]
    [InlineData(ComparisonOutcome.NonMatch, KycCheckResult.Failed)]
    [InlineData(ComparisonOutcome.NoUsableMatch, KycCheckResult.NotChecked)]
    public void Comparison_outcome_maps_to_check_result(ComparisonOutcome input, KycCheckResult expected)
        => Assert.Equal(expected, KycResultMapper.FromComparison(input));

    private static JsonElement Gateway()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var path = Path.Combine(dir.FullName, "Finnova.ApiGateway", "appsettings.json");
            if (File.Exists(path)) return JsonDocument.Parse(File.ReadAllText(path)).RootElement.GetProperty("ReverseProxy").GetProperty("Routes");
            dir = dir.Parent;
        }
        throw new FileNotFoundException("Finnova.ApiGateway/appsettings.json was not found above the test output folder.");
    }

    [Fact]
    public void Gateway_forwards_loan_account_routes_to_the_accounts_cluster_with_the_alias_rewrite()
    {
        var routes = Gateway();
        foreach (var name in new[] { "loan-accounts-route", "loan-accounts-root", "ua-loan-accounts-alias-route", "ua-loan-accounts-alias-root" })
            Assert.Equal("accounts-cluster", routes.GetProperty(name).GetProperty("ClusterId").GetString());
        Assert.Equal("/api/ua/api/loan-accounts/{**catch-all}", routes.GetProperty("ua-loan-accounts-alias-route").GetProperty("Match").GetProperty("Path").GetString());
        Assert.Contains("PathRemovePrefix", routes.GetProperty("ua-loan-accounts-alias-route").GetProperty("Transforms").ToString());
    }

    [Fact]
    public void Gateway_has_no_route_to_internal_endpoints_and_never_sends_accounts_to_systemadmin()
    {
        foreach (var route in Gateway().EnumerateObject())
        {
            var path = route.Value.GetProperty("Match").GetProperty("Path").GetString() ?? string.Empty;
            Assert.DoesNotContain("/internal", path, StringComparison.OrdinalIgnoreCase);
            if (path.Contains("loan-accounts", StringComparison.OrdinalIgnoreCase))
                Assert.NotEqual("systemadmin-cluster", route.Value.GetProperty("ClusterId").GetString());
        }
    }
}
```

- [ ] 15. Checkpoint - backend
  - Run `dotnet build Finnova.Backend.slnx`, then `dotnet test Finnova.Tests`. Confirm the generated migration (task 4.5) before running the database. Ensure all tests pass, ask the user if questions arise.


## Notes

- The graph below schedules each incomplete leaf task once, while preserving the implementation order above. Tasks in the same wave write separate files and may be handled in parallel.

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3", "1.4", "1.5", "1.6", "1.7", "1.8", "1.9", "1.10", "1.11"] },
    { "id": 1, "tasks": ["2.1"] },
    { "id": 2, "tasks": ["3.1", "3.2", "3.3", "3.4", "3.5"] },
    { "id": 3, "tasks": ["4.1", "4.2", "4.3", "4.4"] },
    { "id": 4, "tasks": ["5.1", "5.2", "6.1", "6.2", "6.3", "6.4", "6.5", "6.6", "6.7", "6.8", "6.9"] },
    { "id": 5, "tasks": ["5.3", "5.4", "5.5", "5.6", "7.1", "7.2", "7.3", "7.4"] },
    { "id": 6, "tasks": ["8.1", "8.2", "8.3", "8.4", "8.5", "8.6", "8.7", "8.8", "8.9"] },
    { "id": 7, "tasks": ["9.1", "10.1", "10.2", "10.3", "10.4"] },
    { "id": 8, "tasks": ["11.1", "11.2", "11.3"] },
    { "id": 9, "tasks": ["12.1", "12.2", "12.3", "12.4", "12.5", "12.6"] },
    { "id": 10, "tasks": ["13.1", "13.2"] },
    { "id": 11, "tasks": ["14.1", "14.2", "14.3", "14.4", "14.5", "14.6"] },
    { "id": 12, "tasks": ["14.7", "14.8", "14.9", "14.10"] },
    { "id": 13, "tasks": ["4.5"] }
  ]
}
```
