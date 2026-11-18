# Implementation Plan: Receipt Authorization and Inter-Branch Accounting

**Feature:** `receipt-authorization-inter-branch-accounting`  
**Jira:** FINNOVA-63  
**Source documents:** `requirements.md`, `design.md`  
**Backend repository:** `d:\Projects\Finnova\Finnova-API`  
**Frontend repository:** `E:\Finnova\Finnova-UI\Finnova-UI` (the path was not present in the planning workspace; complete the UI gate before applying frontend tasks)

## Execution contract

This file is a copy-and-paste implementation plan. Apply tasks in order. Each implementation task is marked `CREATE <path>` or `MODIFY <path>`. A `CREATE` block is the complete content of a new file. A `MODIFY` block gives the exact existing anchor from the current repository where it is known, followed by the insertion or replacement. Do not create a second Receipt aggregate, journal model, JVE queue, Cashflow Master, GL Master, account-branch master, or public worker endpoint.

The implementation agent must stop at a **GATE** task when an authoritative FINNOVA-61, JVE, organization, role, print, or UI contract is missing. Record the confirmed type/path in the task notes, then continue without inventing a substitute. All amounts are INR with two decimal places, technical timestamps are UTC, user-facing values are English-only, and no RTL or localization framework is introduced.

## Overview

This plan extends the FINNOVA-61 Receipt aggregate with separate authorization and asynchronous posting states. A permitted Checker reviews the complete receipt, and one atomic decision transaction either rejects the receipt without posting or records authorization plus exactly one immutable request in the existing JVE inbox. The JVE remains the only owner of Cashflow Master resolution, GL selection, canonical journal creation, retries, dead-letter processing, and final `Posted` completion. The UI uses the established React, TypeScript, MUI, shared-axios, React-hook, and mock/real-toggle patterns.

The implementation order is domain and contracts first, persistence and adapters second, pure rules and CQRS third, controller/gateway wiring fourth, and frontend plus automated tests last. External contracts are explicit gates because the current planning workspace does not contain the FINNOVA-61 source or the separately named Finnova-UI repository.

## Mandatory external gates

- FINNOVA-61 Receipt, ReceiptLine, status, creator identity, row-version, audit, and read-model types must be confirmed before Tasks 1.3, 3, and 6.
- The exact `JvPostingInbox` namespace, columns, insertion API, schema version, event key, and same-database transaction boundary must be confirmed before Tasks 2.3, 3, 5, and 6. No canonical journal table is added here.
- Workflow-rule ownership, precedence, no-rule behavior, Loan Account to Account Branch relationship, Level-5 branch validation, inter-branch configuration owner, GL validation adapter, role names, access-scope shape, and print endpoint must be confirmed before their adapters are wired.
- The frontend repository and FINNOVA-61 UI service/page anchors must be available before Tasks 10 and 11. If the supplied frontend path differs, bind the paths below to the equivalent FINNOVA-61 pattern and record the mapping.

## Task Dependency Graph

Tasks in one wave can run in parallel. A wave starts only after all prior waves are complete. Checkpoint tasks are not included in the graph.

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.4", "2.1"] },
    { "id": 1, "tasks": ["1.3", "2.2", "2.3"] },
    { "id": 2, "tasks": ["3.1", "3.2", "4.1"] },
    { "id": 3, "tasks": ["3.3", "4.2", "5.1"] },
    { "id": 4, "tasks": ["5.2", "5.3", "5.4"] },
    { "id": 5, "tasks": ["6.1", "6.2", "6.3", "6.4"] },
    { "id": 6, "tasks": ["7.1", "7.2", "7.3"] },
    { "id": 7, "tasks": ["7.4", "8.1", "8.2", "8.3"] },
    { "id": 8, "tasks": ["9.1", "9.2", "9.3", "9.4"] },
    { "id": 9, "tasks": ["10.1", "10.2", "10.3", "10.4"] },
    { "id": 10, "tasks": ["11.1", "11.2", "11.3", "11.4", "11.5", "11.6"] },
    { "id": 11, "tasks": ["12.1", "12.2", "12.3", "12.4", "12.5"] },
    { "id": 12, "tasks": ["13.1", "13.2", "13.3", "13.4"] }
  ]
}
```

## Tasks

- [ ] 1. Backend domain lifecycle and immutable records
  - [ ] 1.1 CREATE `Finnova.Models/Domain/Enums/ReceiptAuthorizationEnums.cs`
    - Create this complete file:
      ```csharp
      namespace Finnova.Models.Domain.Enums;

      public enum AuthorizationDecision
      {
          PendingAuthorization = 0,
          Authorized = 1,
          Rejected = 2
      }

      public enum PostingStatus
      {
          NotSubmitted = 0,
          PostingPending = 1,
          RetryPending = 2,
          Failed = 3,
          Posted = 4
      }

      public enum ReceiptBranchRelationship
      {
          SameBranch = 0,
          InterBranch = 1
      }

      public enum ReceiptAuthorizationAction
      {
          Authorize = 0,
          Reject = 1
      }

      public enum ReceiptAuthorizationAuditKind
      {
          AuthorizationDecision = 0,
          PostingLifecycle = 1,
          IntegrityAlert = 2
      }
      ```
    - Preserve any FINNOVA-61 `ReceiptStatus` enum. Do not rename or delete legacy values in this task. **Requirements:** 3.3–3.4, 4.1–4.6, 5.1–5.6, 9.1–9.15.

  - [ ] 1.2 CREATE `Finnova.Models/Domain/Entities/ReceiptAuthorizationEntities.cs`
    - Create this complete file. It contains only feature-owned request/audit records; it does not duplicate `Receipt`, `ReceiptLine`, `JvPostingInbox`, journal, Cashflow, GL, or branch entities.
      ```csharp
      using Finnova.Models.Domain.Enums;

      namespace Finnova.Models.Domain.Entities;

      public sealed class ReceiptAuthorizationRequest
      {
          public Guid Id { get; set; } = Guid.NewGuid();
          public Guid ClientRequestId { get; set; }
          public Guid ReceiptId { get; set; }
          public ReceiptAuthorizationAction Action { get; set; }
          public string ExpectedVersion { get; set; } = string.Empty;
          public string RequestHash { get; set; } = string.Empty;
          public string OutcomeStatus { get; set; } = string.Empty;
          public AuthorizationDecision AuthorizationDecision { get; set; }
          public PostingStatus PostingStatus { get; set; }
          public Guid? PostingRequestId { get; set; }
          public Guid? AuditEntryId { get; set; }
          public string ResponseSnapshotJson { get; set; } = string.Empty;
          public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
          public DateTime? CompletedAtUtc { get; set; }
          public byte[] RowVersion { get; set; } = Array.Empty<byte>();
      }

      public sealed class ReceiptAuthorizationAuditEntry
      {
          public Guid Id { get; set; } = Guid.NewGuid();
          public Guid ReceiptId { get; set; }
          public ReceiptAuthorizationAuditKind EntryKind { get; set; }
          public AuthorizationDecision? PriorAuthorizationDecision { get; set; }
          public AuthorizationDecision? NewAuthorizationDecision { get; set; }
          public PostingStatus? PriorPostingStatus { get; set; }
          public PostingStatus? NewPostingStatus { get; set; }
          public string MakerUserId { get; set; } = string.Empty;
          public string? CheckerUserId { get; set; }
          public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
          public string ExpectedVersion { get; set; } = string.Empty;
          public Guid? ClientRequestId { get; set; }
          public string CorrelationId { get; set; } = string.Empty;
          public Guid? PostingRequestId { get; set; }
          public Guid? CanonicalJournalId { get; set; }
          public string? SafeOutcomeCode { get; set; }
          public string? Reason { get; set; }
          public string ChangeSummary { get; set; } = string.Empty;
      }
      ```
    - The audit repository must expose add/read only. Do not add update/delete methods. **Requirements:** 4.5, 5.4, 9.12–9.13, 13.1–13.10, 14.5–14.13.

  - [ ] 1.3 MODIFY `Finnova.Models/Domain/Entities/Receipt.cs`
    - **GATE:** Confirm the FINNOVA-61 file and exact class are present. The current FINNOVA-61 task contract uses `Finnova.Models/Domain/Entities/Receipt.cs` and the anchor below. If the implemented file has a different path, bind to that existing Receipt type instead of creating a parallel class.
    - Find this exact anchor from the FINNOVA-61 Receipt entity:
      ```csharp
      public ReceiptStatus Status { get; set; } = ReceiptStatus.PendingAuthorization;
      ```
    - Insert immediately after it:
      ```csharp
      public AuthorizationDecision AuthorizationDecision { get; set; } = AuthorizationDecision.PendingAuthorization;
      public PostingStatus PostingStatus { get; set; } = PostingStatus.NotSubmitted;
      public Guid? AuthorizationRuleId { get; set; }
      public string? AuthorizationRuleVersion { get; set; }
      public bool AuthorizationRequired { get; set; } = true;
      public string? AuthorizationActorId { get; set; }
      public string? AuthorizationActorDisplay { get; set; }
      public DateTime? AuthorizationAtUtc { get; set; }
      public string? AuthorizationReason { get; set; }
      public string? RejectionReason { get; set; }
      public Guid? PostingRequestId { get; set; }
      public string? PostingSchemaVersion { get; set; }
      public string? PostingPayloadHash { get; set; }
      public Guid? CanonicalJournalId { get; set; }
      public string? PostingFailureCode { get; set; }
      public string? PostingFailureMessageSafe { get; set; }
      public string? PostingRecoveryReference { get; set; }
      public DateTime? PostingUpdatedAtUtc { get; set; }
      public DateTime? PrintingUnlockedAtUtc { get; set; }
      ```
    - Add `using Finnova.Models.Domain.Enums;` if the file does not already import the enum namespace. Preserve legacy `Status`, `AuthorizedBy`, and `AuthorizedAtUtc` as compatibility projections until the confirmed migration mapping is applied. **Requirements:** 3.3–3.12, 4.1–4.20, 5.1–5.12, 9.1–9.15, 10.1–10.13.

  - [ ] 1.4 CREATE `Finnova.Models/Domain/Exceptions/ReceiptAuthorizationExceptions.cs`
    - Create this complete file:
      ```csharp
      namespace Finnova.Models.Domain.Exceptions;

      public static class ReceiptAuthorizationRuleKeys
      {
          public const string MakerCannotAuthorizeOwnReceipt = "MAKER_CANNOT_AUTHORIZE_OWN_RECEIPT";
          public const string InvalidAuthorizationState = "INVALID_AUTHORIZATION_STATE";
          public const string WorkflowConfigurationMissing = "WORKFLOW_CONFIGURATION_MISSING";
          public const string WorkflowConfigurationConflict = "WORKFLOW_CONFIGURATION_CONFLICT";
          public const string AccountBranchUnresolved = "ACCOUNT_BRANCH_UNRESOLVED";
          public const string InterBranchConfigurationMissing = "INTER_BRANCH_CONFIGURATION_MISSING";
          public const string PostingIdentityMismatch = "POSTING_IDENTITY_MISMATCH";
          public const string PrintingNotAvailable = "PRINTING_NOT_AVAILABLE";
          public const string IdempotencyConflict = "IDEMPOTENCY_CONFLICT";
          public const string ScopeDenied = "SCOPE_DENIED";
      }

      public abstract class ReceiptAuthorizationException : Exception
      {
          protected ReceiptAuthorizationException(string message, string? rule = null) : base(message) => Rule = rule;
          public abstract string ErrorCode { get; }
          public abstract int StatusCode { get; }
          public string? Rule { get; }
      }

      public sealed class ReceiptAuthorizationValidationException : ReceiptAuthorizationException
      {
          public ReceiptAuthorizationValidationException(string message) : base(message) { }
          public override string ErrorCode => "ERR-RCA-400";
          public override int StatusCode => 400;
      }

      public sealed class ReceiptAuthorizationNotFoundException : ReceiptAuthorizationException
      {
          public ReceiptAuthorizationNotFoundException(string message) : base(message) { }
          public override string ErrorCode => "ERR-RCA-404";
          public override int StatusCode => 404;
      }

      public sealed class ReceiptAuthorizationForbiddenException : ReceiptAuthorizationException
      {
          public ReceiptAuthorizationForbiddenException(string message) : base(message, ReceiptAuthorizationRuleKeys.ScopeDenied) { }
          public override string ErrorCode => "ERR-RCA-403";
          public override int StatusCode => 403;
      }

      public sealed class ReceiptAuthorizationConflictException : ReceiptAuthorizationException
      {
          public ReceiptAuthorizationConflictException(string message, string rule) : base(message, rule) { }
          public override string ErrorCode => "ERR-RCA-409";
          public override int StatusCode => 409;
      }

      public sealed class ReceiptAuthorizationBusinessRuleException : ReceiptAuthorizationException
      {
          public ReceiptAuthorizationBusinessRuleException(string message, string rule) : base(message, rule) { }
          public override string ErrorCode => "ERR-RCA-422";
          public override int StatusCode => 422;
      }

      public sealed class ReceiptAuthorizationDependencyException : ReceiptAuthorizationException
      {
          public ReceiptAuthorizationDependencyException(string message) : base(message) { }
          public override string ErrorCode => "ERR-RCA-503";
          public override int StatusCode => 503;
      }

      public static class ReceiptAuthorizationMessages
      {
          public const string MakerCannotAuthorizeOwnReceipt = "Maker cannot authorize their own receipt";
          public const string Unexpected = "Unexpected error.";
      }
      ```
      **Requirements:** 6.1–6.11, 12.1–12.13, 15.1–15.11.

- [ ] 2. Backend contracts and authoritative adapter ports
  - [ ] 2.1 CREATE `Finnova.Models/Contracts/ReceiptAuthorization/ReceiptAuthorizationContracts.cs`
    - Create complete DTO contracts with no Checker identity in request bodies:
      ```csharp
      using Finnova.Models.Domain.Enums;

      namespace Finnova.Models.Contracts.ReceiptAuthorization;

      public sealed record ReceiptAuthorizationReviewFilter(
          string? ReceiptType, string? ReceiptMode, Guid? CollectionBranchId, string? CustomerCode,
          string? DocumentNumber, DateOnly? DocDateFrom, DateOnly? DocDateTo, string? MakerUserId,
          AuthorizationDecision? AuthorizationDecision, PostingStatus? PostingStatus, int Page = 1, int PageSize = 20);

      public sealed record AuthorizeReceiptRequest(Guid ClientRequestId, string ExpectedVersion, string? DecisionReason);
      public sealed record RejectReceiptRequest(Guid ClientRequestId, string ExpectedVersion, string Reason);

      public sealed record ReceiptAuthorizationLineResponse(
          Guid LineId, int LineNo, Guid LoanAccountId, string AccountNumber, string ComponentCode,
          decimal Amount, Guid AccountBranchId, string AccountBranchCode, ReceiptBranchRelationship BranchRelationship);

      public sealed record ReceiptAuthorizationAuditEntryResponse(
          Guid Id, ReceiptAuthorizationAuditKind EntryKind, AuthorizationDecision? PriorAuthorizationDecision,
          AuthorizationDecision? NewAuthorizationDecision, PostingStatus? PriorPostingStatus, PostingStatus? NewPostingStatus,
          string MakerUserId, string? CheckerUserId, DateTime OccurredAtUtc, string CorrelationId,
          Guid? PostingRequestId, Guid? CanonicalJournalId, string? SafeOutcomeCode, string? Reason, string ChangeSummary);

      public sealed record ReceiptPrintEligibilityResponse(
          Guid ReceiptId, bool CanPrint, string ReasonCode, string EnglishReason,
          Guid? PostingRequestId, Guid? CanonicalJournalId, bool IsPreview);

      public sealed record ReceiptAuthorizationListItemResponse(
          Guid ReceiptId, string DocumentNumber, DateOnly DocDate, string MakerUserId, string CustomerCode,
          string CollectionBranchCode, decimal Amount, string ReceiptType, string ReceiptMode,
          AuthorizationDecision AuthorizationDecision, PostingStatus PostingStatus, string ExpectedVersion);

      public sealed record ReceiptAuthorizationReviewResponse(
          Guid ReceiptId, string DocumentNumber, DateOnly DocDate, DateOnly ValueDate, string ReceiptType,
          string ReceiptMode, string CurrencyCode, decimal Amount, string CustomerCode, string CustomerName,
          string MakerUserId, string CollectionBranchCode, AuthorizationDecision AuthorizationDecision,
          PostingStatus PostingStatus, bool AuthorizationRequired, Guid? WorkflowRuleId, string? WorkflowRuleVersion,
          string ExpectedVersion, IReadOnlyList<ReceiptAuthorizationLineResponse> Lines,
          IReadOnlyList<ReceiptAuthorizationAuditEntryResponse> AuditEntries, ReceiptPrintEligibilityResponse PrintEligibility);

      public sealed record ReceiptAuthorizationDecisionResponse(
          Guid ReceiptId, AuthorizationDecision AuthorizationDecision, PostingStatus PostingStatus,
          string DecisionActorId, DateTime DecisionAtUtc, string ExpectedVersion, Guid ClientRequestId,
          Guid? PostingRequestId, Guid? CanonicalJournalId, string? SafeMessage, bool PrintingAllowed);

      public sealed record ReceiptPostingStatusResponse(
          Guid ReceiptId, Guid? PostingRequestId, AuthorizationDecision AuthorizationDecision,
          PostingStatus PostingStatus, Guid? CanonicalJournalId, string? SafeOutcomeCode,
          string? RecoveryReference, DateTime? UpdatedAtUtc, bool PrintingAllowed);

      public sealed record ReceiptPostingStatusCallback(
          Guid ReceiptId, Guid PostingRequestId, string ReceiptVersionOrHash, string PayloadHash,
          string NewPostingStatus, Guid? CanonicalJournalId, string? SafeOutcomeCode,
          string? SafeMessage, string? RecoveryReference, DateTime OccurredAtUtc, string CorrelationId);

      public sealed record ReceiptAuthorizationPageResponse(IReadOnlyList<ReceiptAuthorizationListItemResponse> Items, int TotalCount, int Page, int PageSize);
      public sealed record ReceiptTraceResponse(Guid ReceiptId, IReadOnlyList<ReceiptAuthorizationAuditEntryResponse> AuditEntries, Guid? PostingRequestId, Guid? CanonicalJournalId, PostingStatus PostingStatus, bool PrintingAllowed);
      public sealed record ReceiptAuthorizationPostStatusRequest(ReceiptPostingStatusCallback Callback);
      ```
      **Requirements:** 1.1–1.11, 4.6–4.7, 5.9–5.10, 9.10–9.15, 11.1–11.17.

  - [ ] 2.2 CREATE `Finnova.Service/ReceiptAuthorization/Ports/ReceiptAuthorizationPorts.cs`
    - Create complete contracts for external owners. These are interfaces only; adapters must call existing FINNOVA-61/JVE/master repositories and must not introduce local master tables.
      ```csharp
      using Finnova.Models.Contracts.ReceiptAuthorization;
      using Finnova.Models.Domain.Enums;

      namespace Finnova.Service.ReceiptAuthorization.Ports;

      public sealed record ReceiptWorkflowLineContext(Guid LineId, Guid CompanyId, Guid LineOfBusinessId, Guid CollectionBranchId, Guid AccountBranchId, string ReceiptType, string ReceiptFrom, string ValueDate);
      public sealed record WorkflowEvaluationResult(bool AuthorizationRequired, Guid? RuleId, string? RuleVersion, int? Precedence, string? CashflowEventKey, string EvaluatedScope, string EnglishReason, bool ConfigurationConflict);
      public sealed record AccountBranchContext(Guid LineId, Guid LoanAccountId, Guid CompanyId, Guid CollectionBranchId, Guid AccountBranchId, string AccountBranchCode, bool IsActiveLevelFive);
      public sealed record InterBranchConfigurationResult(bool Found, bool Ambiguous, string? ConfigurationReference, string? FailureReason);
      public sealed record JvePostingLine(Guid LineId, Guid LoanAccountId, Guid CollectionBranchId, Guid AccountBranchId, ReceiptBranchRelationship BranchRelationship, decimal Amount, string CurrencyCode, string ValueDate, string ComponentCode);
      public sealed record PreparedJvePostingRequest(Guid RequestId, string SourceSystem, string EventType, string RuleKey, string SchemaVersion, Guid CompanyId, Guid CollectionBranchId, string ValueDate, string CurrencyCode, string PayloadJson, string PayloadHash);
      public sealed record JvePostingRequestIdentity(Guid RequestId, string SchemaVersion, string PayloadHash);
      public sealed record JveStatusObservation(Guid RequestId, string ReceiptVersionOrHash, string PayloadHash, PostingStatus Status, Guid? CanonicalJournalId, string? SafeOutcomeCode, string? RecoveryReference, DateTime OccurredAtUtc);

      public interface IReceiptWorkflowRulePort
      {
          Task<WorkflowEvaluationResult> ResolveAsync(Guid receiptId, IReadOnlyList<ReceiptWorkflowLineContext> lines, CancellationToken cancellationToken);
      }

      public interface IReceiptAccountBranchPort
      {
          Task<IReadOnlyList<AccountBranchContext>> ResolveForAccountsAsync(IReadOnlyList<Guid> loanAccountIds, CancellationToken cancellationToken);
      }

      public interface IInterBranchConfigurationPort
      {
          Task<InterBranchConfigurationResult> ResolveAsync(Guid companyId, Guid collectionBranchId, Guid accountBranchId, string currencyCode, string cashflowEventKey, CancellationToken cancellationToken);
      }

      public interface IApprovedJveSubmissionPort
      {
          Task<PreparedJvePostingRequest> PrepareAsync(Guid receiptId, IReadOnlyList<JvePostingLine> lines, Guid companyId, Guid collectionBranchId, string valueDate, string schemaVersion, CancellationToken cancellationToken);
          Task<JvePostingRequestIdentity> InsertReadyRequestAsync(PreparedJvePostingRequest request, object transaction, CancellationToken cancellationToken);
      }

      public interface IReceiptPostingReconciliationPort
      {
          Task<JveStatusObservation?> ReadCurrentAsync(Guid postingRequestId, CancellationToken cancellationToken);
      }

      public interface IReceiptScopePort
      {
          Task<bool> CanReadAsync(string actorId, Guid companyId, Guid lineOfBusinessId, Guid collectionBranchId, CancellationToken cancellationToken);
          Task<bool> CanDecideAsync(string actorId, Guid companyId, Guid lineOfBusinessId, Guid collectionBranchId, CancellationToken cancellationToken);
          Task<bool> CanPrintAsync(string actorId, Guid companyId, Guid collectionBranchId, CancellationToken cancellationToken);
      }
      ```
      **GATE:** Replace `object transaction` with the confirmed `DbTransaction` or approved repository transaction type after the JVE inbox contract is confirmed. **Requirements:** 2.1–2.8, 7.1–7.11, 8.9–8.14, 9.10–9.13, 16.1–16.8.

  - [ ] 2.3 CREATE `Finnova.Service/ReceiptAuthorization/Ports/ReceiptAuthorizationAdapters.cs`
    - Add fail-closed adapter implementations only after confirming authoritative owners. Until then, register these adapters as blocking implementations that throw `ReceiptAuthorizationDependencyException`; do not return permissive defaults.
      ```csharp
      using Finnova.Models.Domain.Exceptions;

      namespace Finnova.Service.ReceiptAuthorization.Ports;

      public sealed class UnconfiguredReceiptWorkflowRulePort : IReceiptWorkflowRulePort
      {
          public Task<WorkflowEvaluationResult> ResolveAsync(Guid receiptId, IReadOnlyList<ReceiptWorkflowLineContext> lines, CancellationToken cancellationToken)
              => throw new ReceiptAuthorizationDependencyException("Receipt workflow rule adapter is not configured.");
      }

      public sealed class UnconfiguredReceiptAccountBranchPort : IReceiptAccountBranchPort
      {
          public Task<IReadOnlyList<AccountBranchContext>> ResolveForAccountsAsync(IReadOnlyList<Guid> loanAccountIds, CancellationToken cancellationToken)
              => throw new ReceiptAuthorizationDependencyException("Receipt account-branch adapter is not configured.");
      }

      public sealed class UnconfiguredInterBranchConfigurationPort : IInterBranchConfigurationPort
      {
          public Task<InterBranchConfigurationResult> ResolveAsync(Guid companyId, Guid collectionBranchId, Guid accountBranchId, string currencyCode, string cashflowEventKey, CancellationToken cancellationToken)
              => throw new ReceiptAuthorizationDependencyException("Inter-branch configuration adapter is not configured.");
      }

      public sealed class UnconfiguredReceiptPostingReconciliationPort : IReceiptPostingReconciliationPort
      {
          public Task<JveStatusObservation?> ReadCurrentAsync(Guid postingRequestId, CancellationToken cancellationToken)
              => throw new ReceiptAuthorizationDependencyException("Receipt posting reconciliation adapter is not configured.");
      }
      ```
      **Requirements:** 2.6–2.7, 3.5–3.12, 4.17–4.20, 7.7–7.11, 15.5–15.10.

- [ ] 3. EF persistence, lifecycle migration, and database command
  - [ ] 3.1 CREATE `Finnova.Repository/Configuration/ReceiptAuthorizationConfiguration.cs`
    - Create complete EF configurations. Confirm the Receipt table name and key before applying; the configuration must target the FINNOVA-61 entity rather than create a second receipt table.
      ```csharp
      using Finnova.Models.Domain.Entities;
      using Microsoft.EntityFrameworkCore;
      using Microsoft.EntityFrameworkCore.Metadata.Builders;

      namespace Finnova.Repository.Configuration;

      public sealed class ReceiptAuthorizationRequestConfiguration : IEntityTypeConfiguration<ReceiptAuthorizationRequest>
      {
          public void Configure(EntityTypeBuilder<ReceiptAuthorizationRequest> builder)
          {
              builder.ToTable("receipt_authorization_requests");
              builder.HasKey(x => x.Id);
              builder.HasIndex(x => x.ClientRequestId).IsUnique();
              builder.HasIndex(x => new { x.ReceiptId, x.Action, x.ExpectedVersion });
              builder.Property(x => x.RequestHash).HasMaxLength(128).IsRequired();
              builder.Property(x => x.ResponseSnapshotJson).HasMaxLength(16000).IsRequired();
              builder.Property(x => x.RowVersion).IsRowVersion();
          }
      }

      public sealed class ReceiptAuthorizationAuditEntryConfiguration : IEntityTypeConfiguration<ReceiptAuthorizationAuditEntry>
      {
          public void Configure(EntityTypeBuilder<ReceiptAuthorizationAuditEntry> builder)
          {
              builder.ToTable("receipt_authorization_audit_entries");
              builder.HasKey(x => x.Id);
              builder.HasIndex(x => new { x.ReceiptId, x.OccurredAtUtc, x.Id });
              builder.HasIndex(x => x.PostingRequestId);
              builder.Property(x => x.ChangeSummary).HasMaxLength(1000).IsRequired();
              builder.Property(x => x.Reason).HasMaxLength(2000);
              builder.Property(x => x.CorrelationId).HasMaxLength(200).IsRequired();
              builder.Property(x => x.MakerUserId).HasMaxLength(200).IsRequired();
              builder.Property(x => x.CheckerUserId).HasMaxLength(200);
              builder.Property(x => x.ExpectedVersion).HasMaxLength(200).IsRequired();
              builder.Property(x => x.SafeOutcomeCode).HasMaxLength(200);
          }
      }
      ```
      **Requirements:** 13.1–13.10, 14.5–14.13, 16.3–16.7.

  - [ ] 3.2 MODIFY `Finnova.Repository/Context/FinnovaDbContext.cs`
    - Find this exact existing anchor:
      ```csharp
      public DbSet<JvPostingInbox> JvPostingInboxes => Set<JvPostingInbox>();
      ```
    - Insert immediately after it:
      ```csharp
      public DbSet<ReceiptAuthorizationRequest> ReceiptAuthorizationRequests => Set<ReceiptAuthorizationRequest>();
      public DbSet<ReceiptAuthorizationAuditEntry> ReceiptAuthorizationAuditEntries => Set<ReceiptAuthorizationAuditEntry>();
      ```
    - Add `using Finnova.Models.Domain.Entities;` if absent. Do not add a DbSet for a journal, Cashflow Master, GL Master, or a duplicate JVE inbox. **Requirements:** 4.5, 5.4, 13.1–13.10, 14.1–14.13.

  - [ ] 3.3 CREATE `Finnova.Repository/Migrations/<timestamp>_AddReceiptAuthorizationLifecycle.cs` and `Finnova.Repository/Migrations/<timestamp>_AddReceiptAuthorizationLifecycle.Designer.cs`
    - **GATE:** First inspect the latest EF migration and the confirmed FINNOVA-61 Receipt/JVE mappings. Generate the migration with the command below rather than fabricating designer metadata. The migration must add lifecycle fields to the existing receipt table, add the two feature-owned tables, add unique `ClientRequestId`, audit ordering indexes, filtered `PostingRequestId` uniqueness, UTC datetime columns, and the approved compatibility backfill (`PendingAuthorization` unchanged, `Authorized` unchanged, legacy `Revoked` mapped to the confirmed rejected representation).
    - The migration must not alter canonical journal, Cashflow Master, GL Master, or `JvPostingInbox` ownership. Audit delete behavior must preserve history.
    - Copyable command, not to be executed during spec creation:
      ```powershell
      dotnet ef migrations add AddReceiptAuthorizationLifecycle --project Finnova.Repository --startup-project Finnova.AccountsService --output-dir Migrations
      ```
      **Requirements:** 3.3–3.12, 13.9, 14.1–14.13, 16.3–16.8.

- [ ] 4. Repository transaction and unit-of-work support
  - [ ] 4.1 CREATE `Finnova.Repository/Interfaces/IReceiptAuthorizationRepositories.cs`
    - Create complete repository contracts. Methods must not call `SaveChangesAsync` internally.
      ```csharp
      using Finnova.Models.Contracts.ReceiptAuthorization;
      using Finnova.Models.Domain.Entities;
      using Finnova.Models.Domain.Enums;

      namespace Finnova.Repository.Interfaces;

      public interface IReceiptAuthorizationRepository
      {
          Task<(IReadOnlyList<ReceiptAuthorizationListItemResponse> Items, int TotalCount)> GetReviewPageAsync(ReceiptAuthorizationReviewFilter filter, string actorId, CancellationToken cancellationToken);
          Task<Receipt?> GetForDecisionAsync(Guid receiptId, CancellationToken cancellationToken);
          Task<ReceiptAuthorizationReviewResponse?> GetReviewAsync(Guid receiptId, string actorId, CancellationToken cancellationToken);
          Task<int> CompareAndSetDecisionAsync(Guid receiptId, string expectedVersion, AuthorizationDecision expectedDecision, PostingStatus expectedPostingStatus, AuthorizationDecision newDecision, PostingStatus newPostingStatus, CancellationToken cancellationToken);
      }

      public interface IReceiptAuthorizationRequestRepository
      {
          Task<ReceiptAuthorizationRequest?> GetByClientRequestIdAsync(Guid clientRequestId, CancellationToken cancellationToken);
          void Add(ReceiptAuthorizationRequest request);
      }

      public interface IReceiptAuthorizationAuditRepository
      {
          void Add(ReceiptAuthorizationAuditEntry entry);
          Task<IReadOnlyList<ReceiptAuthorizationAuditEntryResponse>> GetByReceiptIdAsync(Guid receiptId, CancellationToken cancellationToken);
          Task<ReceiptTraceResponse?> GetTraceAsync(Guid receiptId, string actorId, CancellationToken cancellationToken);
      }

      public interface IReceiptPostingReconciliationRepository
      {
          Task<Receipt?> GetForReconciliationAsync(Guid receiptId, CancellationToken cancellationToken);
          Task<int> ApplyStatusCompareAndSetAsync(Guid receiptId, Guid postingRequestId, string receiptVersionOrHash, string payloadHash, PostingStatus expected, PostingStatus next, Guid? canonicalJournalId, string? safeOutcomeCode, string? recoveryReference, CancellationToken cancellationToken);
      }
      ```
      **GATE:** Bind `ReceiptTraceResponse` to the confirmed response contract if it already exists; otherwise add it to Task 2.1 before compiling. **Requirements:** 1.1–1.9, 4.5–4.12, 9.10–9.13, 13.8–13.10, 14.1–14.13.

  - [ ] 4.2 MODIFY `Finnova.Repository/Interfaces/IApplicationUnitOfWork.cs`
    - Find the exact existing file content anchor:
      ```csharp
      public interface IApplicationUnitOfWork
      {
          FinnovaDbContext Context { get; }
          Task<int> SaveChangesAsync(CancellationToken ct = default);
          Task<IAsyncDisposable> BeginTransactionAsync(CancellationToken ct = default);
      }
      ```
    - Replace it with:
      ```csharp
      using Microsoft.EntityFrameworkCore.Storage;
      using Finnova.Repository.Context;

      namespace Finnova.Repository.Interfaces;

      public interface IApplicationUnitOfWork
      {
          FinnovaDbContext Context { get; }
          Task<int> SaveChangesAsync(CancellationToken ct = default);
          Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default);
      }
      ```
    - Update the existing implementation so `BeginTransactionAsync` returns the real EF transaction and commit/rollback is controlled by the handler. Do not permit repository methods to commit independently. **Requirements:** 4.5, 4.17–4.20, 14.1–14.13.

- [ ] 5. Repositories, approved JVE boundary, DI, and policies
  - [ ] 5.1 CREATE `Finnova.Repository/Repositories/ReceiptAuthorizationRepositories.cs`
    - Implement the interfaces from Task 4 using the existing `FinnovaDbContext`, parameterized LINQ, scope predicates, stable ordering (`DocDate DESC`, `CreatedAtUtc DESC`, `Id DESC`), and row-version/CAS predicates. Use the existing FINNOVA-61 receipt navigation and JVE inbox mapping after the gate. Never write canonical journal tables.
    - **GATE:** The file must not be copied until the exact FINNOVA-61 Receipt/Line properties and existing `JvPostingInbox` property names are confirmed. The implementation must fail closed for unresolved Account Branch and scope rather than filtering in memory after returning inaccessible records. **Requirements:** 1.1–1.9, 7.7–7.10, 12.6–12.7, 14.1–14.13.

  - [ ] 5.2 CREATE `Finnova.Service/ReceiptAuthorization/Adapters/ApprovedJveInboxAdapter.cs`
    - Implement `IApprovedJveSubmissionPort` against the confirmed existing `JvPostingInbox` entity and approved transaction boundary. The adapter must set the immutable raw payload/hash, event key, schema version, company/branch/value-date/currency context, and actor metadata. It must insert exactly one ready inbox row inside the caller transaction. It must not call HTTP and must not create journal rows.
    - **GATE:** Bind only after the JVE schema and method contract are confirmed. Until then, retain `UnconfiguredApprovedJveSubmissionPort` from Task 2.3. **Requirements:** 3.5–3.8, 4.5–4.10, 7.1–7.11, 8.9–8.14.

  - [ ] 5.3 MODIFY `Finnova.Service/DependencyInjection.cs`
    - Find the exact existing registration anchor:
      ```csharp
      services.AddScoped<IJournalPort, ConfiguredJournalPort>();
      services.AddScoped<IDemandProcessor, DemandProcessor>();
      ```
    - Insert immediately before it:
      ```csharp
      services.AddScoped<IReceiptWorkflowRulePort, UnconfiguredReceiptWorkflowRulePort>();
      services.AddScoped<IReceiptAccountBranchPort, UnconfiguredReceiptAccountBranchPort>();
      services.AddScoped<IInterBranchConfigurationPort, UnconfiguredInterBranchConfigurationPort>();
      services.AddScoped<IApprovedJveSubmissionPort, UnconfiguredApprovedJveSubmissionPort>();
      services.AddScoped<IReceiptPostingReconciliationPort, UnconfiguredReceiptPostingReconciliationPort>();
      services.AddScoped<IReceiptAuthorizationRepository, ReceiptAuthorizationRepository>();
      services.AddScoped<IReceiptAuthorizationRequestRepository, ReceiptAuthorizationRequestRepository>();
      services.AddScoped<IReceiptAuthorizationAuditRepository, ReceiptAuthorizationAuditRepository>();
      services.AddScoped<IReceiptPostingReconciliationRepository, ReceiptPostingReconciliationRepository>();
      ```
    - Replace the unconfigured registrations with confirmed adapters only after the external gates pass. **Requirements:** 2.6–2.7, 4.5, 5.4, 9.10–9.13, 16.1–16.8.

  - [ ] 5.4 MODIFY `Finnova.AccountsService/Program.cs`
    - Find the exact existing authorization block ending with:
      ```csharp
      options.AddPolicy("AccountsOps", p => p.RequireAssertion(ctx =>
          ctx.User.IsInRole("AccountsOperator") || ctx.User.IsInRole("SystemAdmin")));
      ```
    - Insert immediately after it, after confirming the approved role names:
      ```csharp
      options.AddPolicy("ReceiptAuthorizationRead", p => p.RequireAuthenticatedUser().RequireAssertion(ctx =>
          ctx.User.IsInRole("AuthorizedApprover") ||
          ctx.User.IsInRole("BranchManager") ||
          ctx.User.IsInRole("SystemAdmin")));
      options.AddPolicy("ReceiptAuthorizationDecision", p => p.RequireAuthenticatedUser().RequireAssertion(ctx =>
          ctx.User.IsInRole("AuthorizedApprover") ||
          ctx.User.IsInRole("BranchManager")));
      options.AddPolicy("ReceiptPrint", p => p.RequireAuthenticatedUser().RequireAssertion(ctx =>
          ctx.User.IsInRole("ReceiptPrinter") || ctx.User.IsInRole("SystemAdmin")));
      options.AddPolicy("ReceiptJveCallback", p => p.RequireAuthenticatedUser().RequireClaim("internal_jve", "true"));
      ```
    - Do not grant UI clients the callback policy. **Requirements:** 6.1–6.11, 12.1–12.13.

- [ ] 6. Pure/domain helpers
  - [ ] 6.1 CREATE `Finnova.Service/ReceiptAuthorization/Domain/ReceiptAuthorizationRules.cs`
    - Create complete pure helpers for aggregate workflow, stable identity, status transition, and printing gate:
      ```csharp
      using Finnova.Models.Domain.Enums;
      using Finnova.Models.Domain.Exceptions;
      using Finnova.Service.ReceiptAuthorization.Ports;

      namespace Finnova.Service.ReceiptAuthorization.Domain;

      public static class ReceiptAuthorizationRules
      {
          public static WorkflowEvaluationResult AggregateThirdParty(bool thirdParty, IReadOnlyList<WorkflowEvaluationResult> lineResults)
          {
              if (lineResults.Count == 0) throw new ReceiptAuthorizationBusinessRuleException("Receipt has no account lines.", ReceiptAuthorizationRuleKeys.WorkflowConfigurationMissing);
              if (lineResults.Any(x => x.ConfigurationConflict)) throw new ReceiptAuthorizationBusinessRuleException("Workflow configuration is conflicting.", ReceiptAuthorizationRuleKeys.WorkflowConfigurationConflict);
              var required = lineResults.Any(x => x.AuthorizationRequired);
              var selected = lineResults.Where(x => x.AuthorizationRequired).OrderByDescending(x => x.Precedence ?? int.MinValue).FirstOrDefault() ?? lineResults[0];
              var reason = thirdParty && required ? "Complete Third-Party receipt requires authorization." : selected.EnglishReason;
              return selected with { AuthorizationRequired = required, EnglishReason = reason };
          }

          public static bool IsSelf(string makerId, string checkerId) =>
              !string.IsNullOrWhiteSpace(makerId) && string.Equals(makerId, checkerId, StringComparison.Ordinal);

          public static void EnsureCanDecide(AuthorizationDecision decision, PostingStatus postingStatus, string expectedVersion, string actualVersion)
          {
              if (decision != AuthorizationDecision.PendingAuthorization || postingStatus != PostingStatus.NotSubmitted || !string.Equals(expectedVersion, actualVersion, StringComparison.Ordinal))
                  throw new ReceiptAuthorizationConflictException("The receipt was changed or already decided. Refresh and retry.", ReceiptAuthorizationRuleKeys.InvalidAuthorizationState);
          }

          public static bool CanPrint(AuthorizationDecision decision, PostingStatus postingStatus, Guid? journalId, bool sourceLinkMatches, bool integrityAlert, bool callerAllowed)
              => callerAllowed && decision == AuthorizationDecision.Authorized && postingStatus == PostingStatus.Posted && journalId.HasValue && sourceLinkMatches && !integrityAlert;
      }
      ```
      **Requirements:** 2.1–2.9, 6.1–6.11, 9.1–9.15, 10.1–10.13, 14.13.

  - [ ] 6.2 CREATE `Finnova.Service/ReceiptAuthorization/Domain/ReceiptBranchRules.cs`
    - Create complete branch-context and inter-branch validation helpers. Account Branch is input only from the authoritative adapter.
      ```csharp
      using Finnova.Models.Domain.Enums;
      using Finnova.Models.Domain.Exceptions;
      using Finnova.Service.ReceiptAuthorization.Ports;

      namespace Finnova.Service.ReceiptAuthorization.Domain;

      public sealed record ReceiptLineBranchContext(Guid LineId, Guid LoanAccountId, Guid CompanyId, Guid CollectionBranchId, Guid AccountBranchId, ReceiptBranchRelationship Relationship, decimal Amount, string CurrencyCode, string ValueDate, string ComponentCode);

      public static class ReceiptBranchRules
      {
          public static IReadOnlyList<ReceiptLineBranchContext> Build(Guid companyId, Guid collectionBranchId, IReadOnlyList<(Guid LineId, Guid LoanAccountId, decimal Amount, string ComponentCode)> lines, IReadOnlyList<AccountBranchContext> branches, string valueDate)
          {
              var byAccount = branches.ToDictionary(x => x.LoanAccountId);
              return lines.Select(line =>
              {
                  if (!byAccount.TryGetValue(line.LoanAccountId, out var branch) || branch.CompanyId != companyId || !branch.IsActiveLevelFive)
                      throw new ReceiptAuthorizationBusinessRuleException("Account Branch is unresolved or invalid.", ReceiptAuthorizationRuleKeys.AccountBranchUnresolved);
                  var relationship = collectionBranchId == branch.AccountBranchId ? ReceiptBranchRelationship.SameBranch : ReceiptBranchRelationship.InterBranch;
                  return new ReceiptLineBranchContext(line.LineId, line.LoanAccountId, companyId, collectionBranchId, branch.AccountBranchId, relationship, decimal.Round(line.Amount, 2, MidpointRounding.AwayFromZero), "INR", valueDate, line.ComponentCode);
              }).ToArray();
          }

          public static void EnsureInterBranchConfiguration(InterBranchConfigurationResult result)
          {
              if (!result.Found || result.Ambiguous) throw new ReceiptAuthorizationBusinessRuleException("Inter-branch configuration is missing or ambiguous.", ReceiptAuthorizationRuleKeys.InterBranchConfigurationMissing);
          }
      }
      ```
      **Requirements:** 7.1–7.11, 8.5–8.8, 18.3–18.4.

  - [ ] 6.3 CREATE `Finnova.Service/ReceiptAuthorization/Domain/ReceiptAuthorizationPayloadHasher.cs`
    - Create complete deterministic hashing code. Canonical JSON must include immutable receipt facts, all lines, branch contexts, actor/time, workflow identity, and schema version; it must exclude transport-only metadata.
      ```csharp
      using System.Security.Cryptography;
      using System.Text;
      using System.Text.Json;

      namespace Finnova.Service.ReceiptAuthorization.Domain;

      public static class ReceiptAuthorizationPayloadHasher
      {
          public static string Hash<T>(T payload)
          {
              var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false });
              var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
              return Convert.ToHexString(bytes).ToLowerInvariant();
          }
      }
      ```
      **Requirements:** 7.2, 7.11, 9.12–9.13, 13.6–13.7, 14.12.

  - [ ] 6.4 CREATE `Finnova.Service/ReceiptAuthorization/Domain/InterBranchContractValidator.cs`
    - Create complete validator for INR, two-decimal amounts, branch identity, line traceability, and aggregate total. It must not select GL codes or debit/credit rules.
      ```csharp
      using Finnova.Models.Domain.Exceptions;

      namespace Finnova.Service.ReceiptAuthorization.Domain;

      public static class InterBranchContractValidator
      {
          public static void Validate(IReadOnlyList<ReceiptLineBranchContext> contexts, decimal receiptAmount)
          {
              if (contexts.Any(x => x.CurrencyCode != "INR" || decimal.Round(x.Amount, 2) != x.Amount || x.Amount <= 0m))
                  throw new ReceiptAuthorizationBusinessRuleException("Inter-branch amounts must be positive INR values with two decimals.", ReceiptAuthorizationRuleKeys.InterBranchConfigurationMissing);
              if (contexts.Select(x => x.LineId).Distinct().Count() != contexts.Count)
                  throw new ReceiptAuthorizationBusinessRuleException("Receipt line identity is not unique.", ReceiptAuthorizationRuleKeys.InterBranchConfigurationMissing);
              if (decimal.Round(contexts.Sum(x => x.Amount), 2) != decimal.Round(receiptAmount, 2))
                  throw new ReceiptAuthorizationBusinessRuleException("Receipt line amounts do not equal the receipt amount.", ReceiptAuthorizationRuleKeys.InterBranchConfigurationMissing);
          }
      }
      ```
      **Requirements:** 7.5–7.6, 8.5–8.8, 17.14–17.15.

- [ ] 7. CQRS review, decision, and read-side slices
  - [ ] 7.1 CREATE `Finnova.Service/ReceiptAuthorization/Queries/ReceiptAuthorizationQueries.cs`
    - Add complete MediatR request records for review list, detail, audit, posting status, trace, and print eligibility. Validators must enforce page 1/default 20, max page size 100, supported filters, and no data mutation.
      ```csharp
      using Finnova.Models.Contracts.ReceiptAuthorization;
      using MediatR;

      namespace Finnova.Service.ReceiptAuthorization.Queries;

      public sealed record GetReceiptAuthorizationReviewPageQuery(ReceiptAuthorizationReviewFilter Filter, string ActorId) : IRequest<ReceiptAuthorizationPageResponse>;
      public sealed record GetReceiptAuthorizationReviewQuery(Guid ReceiptId, string ActorId) : IRequest<ReceiptAuthorizationReviewResponse>;
      public sealed record GetReceiptAuthorizationAuditQuery(Guid ReceiptId, string ActorId) : IRequest<IReadOnlyList<ReceiptAuthorizationAuditEntryResponse>>;
      public sealed record GetReceiptPostingStatusQuery(Guid ReceiptId, string ActorId) : IRequest<ReceiptPostingStatusResponse>;
      public sealed record GetReceiptTraceQuery(Guid ReceiptId, string ActorId) : IRequest<ReceiptTraceResponse>;
      public sealed record GetReceiptPrintEligibilityQuery(Guid ReceiptId, string ActorId) : IRequest<ReceiptPrintEligibilityResponse>;
      ```
    - Replace `object` with the confirmed generic response types from the contracts before compiling. **Requirements:** 1.1–1.11, 9.2, 10.4–10.7, 11.1–11.6, 13.8–13.11.

  - [ ] 7.2 CREATE `Finnova.Service/ReceiptAuthorization/Commands/ReceiptAuthorizationCommands.cs`
    - Add complete command records. The actor is supplied by the controller from validated claims, never by JSON.
      ```csharp
      using Finnova.Models.Contracts.ReceiptAuthorization;
      using MediatR;

      namespace Finnova.Service.ReceiptAuthorization.Commands;

      public sealed record AuthorizeReceiptCommand(Guid ReceiptId, AuthorizeReceiptRequest Request, string ActorId, string CorrelationId) : IRequest<ReceiptAuthorizationDecisionResponse>;
      public sealed record RejectReceiptCommand(Guid ReceiptId, RejectReceiptRequest Request, string ActorId, string CorrelationId) : IRequest<ReceiptAuthorizationDecisionResponse>;
      public sealed record ReconcileReceiptPostingStatusCommand(Guid ReceiptId, ReceiptAuthorizationPostStatusRequest Request, string InternalActor, string CorrelationId) : IRequest<ReceiptPostingStatusResponse>;
      ```
    - Add `ReceiptAuthorizationPostStatusRequest` to Task 2.1 if the confirmed callback transport needs it. Replace `object` with the confirmed response types. **Requirements:** 4.1–4.20, 5.1–5.12, 9.10–9.13, 12.8–12.11.

  - [ ] 7.3 CREATE `Finnova.Service/ReceiptAuthorization/Validators/ReceiptAuthorizationValidators.cs`
    - Create complete FluentValidation validators for page bounds, GUIDs, expected version, ClientRequestId, English rejection reason, max reason length, INR-only values, and callback hashes. Validators return field-specific errors and do not inspect authorization state.
      ```csharp
      using FluentValidation;
      using Finnova.Models.Contracts.ReceiptAuthorization;

      namespace Finnova.Service.ReceiptAuthorization.Validators;

      public sealed class AuthorizeReceiptRequestValidator : AbstractValidator<AuthorizeReceiptRequest>
      {
          public AuthorizeReceiptRequestValidator()
          {
              RuleFor(x => x.ClientRequestId).NotEmpty();
              RuleFor(x => x.ExpectedVersion).NotEmpty().MaximumLength(200);
              RuleFor(x => x.DecisionReason).MaximumLength(2000).When(x => x.DecisionReason is not null);
          }
      }

      public sealed class RejectReceiptRequestValidator : AbstractValidator<RejectReceiptRequest>
      {
          public RejectReceiptRequestValidator()
          {
              RuleFor(x => x.ClientRequestId).NotEmpty();
              RuleFor(x => x.ExpectedVersion).NotEmpty().MaximumLength(200);
              RuleFor(x => x.Reason).NotEmpty().MaximumLength(2000);
          }
      }

      public sealed class ReceiptAuthorizationReviewFilterValidator : AbstractValidator<ReceiptAuthorizationReviewFilter>
      {
          public ReceiptAuthorizationReviewFilterValidator()
          {
              RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
              RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
              RuleFor(x => x.DocDateTo).GreaterThanOrEqualTo(x => x.DocDateFrom!.Value).When(x => x.DocDateFrom.HasValue && x.DocDateTo.HasValue);
          }
      }
      ```
      **Requirements:** 1.5–1.7, 5.7, 11.2–11.5, 15.1–15.2.

  - [ ] 7.4 CREATE `Finnova.Service/ReceiptAuthorization/Handlers/ReceiptAuthorizationHandlers.cs`
    - Implement handlers for review/detail/audit/status/trace/print and authorize/reject. The authorize handler must resolve scope, compare stable maker/checker IDs, evaluate workflow, resolve Account Branches, validate inter-branch contexts, prepare the immutable JVE payload, then in one transaction: check idempotency, CAS `PendingAuthorization/NotSubmitted`, insert the existing JVE inbox row, append exactly one audit entry, store the response snapshot, and commit. Rejection follows the same CAS/idempotency transaction without a JVE row. A pre-commit JVE rejection rolls back every change. Post-commit JVE failure is reconciled only through Task 8.
    - **GATE:** Bind handler constructor types to the confirmed repository and `IApplicationUnitOfWork` implementation. Do not copy a guessed handler that writes to unknown Receipt properties or tables. **Requirements:** 1.1–1.11, 2.1–2.9, 3.1–3.12, 4.1–4.20, 5.1–5.12, 6.1–6.11, 7.1–7.11, 9.1–9.15, 10.1–10.7, 13.1–13.13, 14.1–14.13.

- [ ] 8. JVE status reconciliation and printing
  - [ ] 8.1 CREATE `Finnova.Service/ReceiptAuthorization/Handlers/ReconcileReceiptPostingStatusHandler.cs`
    - Implement the internal reconciliation handler against the confirmed durable `JvPostingInbox` status or protected callback. Match request ID, receipt version/hash, payload hash, and allowed current posting status. Valid `RetryPending`, `DeadLetter/Failed`, and `Posted` transitions preserve `AuthorizationDecision = Authorized`; `Posted` requires a canonical journal ID and matching source link. Duplicate valid observations are idempotent. Mismatches preserve state and append one integrity event.
    - **GATE:** Use the confirmed status vocabulary and source-link check. Do not expose this handler through the browser gateway. **Requirements:** 8.9–8.15, 9.4–9.14, 13.6–13.13.

  - [ ] 8.2 CREATE `Finnova.Service/ReceiptAuthorization/Mappers/ReceiptAuthorizationMapper.cs`
    - Map only safe response fields. Include Account Branch for every line, separate authorization/posting statuses, UTC timestamps, safe error/recovery references, and print eligibility. Never serialize bearer tokens, full instrument numbers, unnecessary contacts, raw payload, or unredacted account secrets.
      ```csharp
      using Finnova.Models.Contracts.ReceiptAuthorization;
      using Finnova.Models.Domain.Enums;

      namespace Finnova.Service.ReceiptAuthorization.Mappers;

      public static class ReceiptAuthorizationMapper
      {
          public static ReceiptPrintEligibilityResponse Print(Guid receiptId, bool allowed, string code, string reason, Guid? requestId, Guid? journalId, bool preview = false)
              => new(receiptId, allowed, code, reason, requestId, journalId, preview);

          public static ReceiptAuthorizationDecisionResponse Decision(Guid receiptId, AuthorizationDecision decision, PostingStatus posting, string actor, DateTime atUtc, string version, Guid clientRequestId, Guid? requestId, Guid? journalId, string? message)
              => new(receiptId, decision, posting, actor, atUtc, version, clientRequestId, requestId, journalId, message, false);
      }
      ```
      **Requirements:** 1.2, 4.6–4.7, 5.9–5.10, 9.14–9.15, 10.5, 11.6, 12.12, 13.11.

  - [ ] 8.3 MODIFY `Finnova.Service/ReceiptAuthorization/Domain/ReceiptAuthorizationRules.cs`
    - Find the exact method from Task 6.1:
      ```csharp
      public static bool CanPrint(AuthorizationDecision decision, PostingStatus postingStatus, Guid? journalId, bool sourceLinkMatches, bool integrityAlert, bool callerAllowed)
          => callerAllowed && decision == AuthorizationDecision.Authorized && postingStatus == PostingStatus.Posted && journalId.HasValue && sourceLinkMatches && !integrityAlert;
      ```
    - Replace it with a reason-producing result:
      ```csharp
      public sealed record PrintGateResult(bool Allowed, string ReasonCode, string EnglishReason);

      public static PrintGateResult EvaluatePrint(AuthorizationDecision decision, PostingStatus postingStatus, Guid? journalId, bool sourceLinkMatches, bool integrityAlert, bool callerAllowed)
      {
          if (!callerAllowed) return new(false, "PRINT_SCOPE_DENIED", "You are not authorized to print this receipt.");
          if (integrityAlert) return new(false, "POSTING_INTEGRITY_ALERT", "Printing is locked because posting integrity could not be verified.");
          if (decision != AuthorizationDecision.Authorized) return new(false, "AUTHORIZATION_REQUIRED", "Regular printing is unavailable until the receipt is authorized.");
          if (postingStatus != PostingStatus.Posted) return new(false, "POSTING_NOT_COMPLETE", "Regular printing is unavailable until posting is complete.");
          if (!journalId.HasValue || !sourceLinkMatches) return new(false, "PRINTING_NOT_AVAILABLE", "Regular printing is unavailable because the journal link is incomplete.");
          return new(true, "PRINT_AVAILABLE", "Receipt is available for regular printing.");
      }
      ```
      **Requirements:** 10.1–10.13, 13.12–13.13.

- [ ] 9. Controller, middleware, gateway, and host wiring
  - [ ] 9.1 CREATE `Finnova.AccountsService/Controllers/ReceiptAuthorizationController.cs`
    - Create complete controller route shape using MediatR and claims. It must expose review list/detail, authorize, reject, audit, posting status, trace, print eligibility, final print, and an internal callback only if the confirmed integration requires it. Request bodies must contain no Checker identity. Apply policies from Task 5.4. The internal callback must use `ReceiptJveCallback` and must not be reachable through the UI alias.
      ```csharp
      using Finnova.Models.Contracts.ReceiptAuthorization;
      using Finnova.Service.ReceiptAuthorization.Commands;
      using Finnova.Service.ReceiptAuthorization.Queries;
      using MediatR;
      using Microsoft.AspNetCore.Authorization;
      using Microsoft.AspNetCore.Mvc;
      using System.Security.Claims;

      namespace Finnova.AccountsService.Controllers;

      [ApiController]
      [Route("api/receipts")]
      public sealed class ReceiptAuthorizationController(IMediator mediator) : ControllerBase
      {
          private string ActorId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();
          private string CorrelationId => HttpContext.TraceIdentifier;

          [HttpGet("authorization/reviews"), Authorize(Policy = "ReceiptAuthorizationRead")]
          public async Task<IActionResult> Reviews([FromQuery] ReceiptAuthorizationReviewFilter filter, CancellationToken ct)
              => Ok(await mediator.Send(new GetReceiptAuthorizationReviewPageQuery(filter, ActorId), ct));

          [HttpGet("{receiptId:guid}/authorization-review"), Authorize(Policy = "ReceiptAuthorizationRead")]
          public async Task<IActionResult> Review(Guid receiptId, CancellationToken ct)
              => Ok(await mediator.Send(new GetReceiptAuthorizationReviewQuery(receiptId, ActorId), ct));

          [HttpPost("{receiptId:guid}/authorize"), Authorize(Policy = "ReceiptAuthorizationDecision")]
          public async Task<IActionResult> AuthorizeReceipt(Guid receiptId, [FromBody] AuthorizeReceiptRequest request, CancellationToken ct)
              => Ok(await mediator.Send(new AuthorizeReceiptCommand(receiptId, request, ActorId, CorrelationId), ct));

          [HttpPost("{receiptId:guid}/reject"), Authorize(Policy = "ReceiptAuthorizationDecision")]
          public async Task<IActionResult> Reject(Guid receiptId, [FromBody] RejectReceiptRequest request, CancellationToken ct)
              => Ok(await mediator.Send(new RejectReceiptCommand(receiptId, request, ActorId, CorrelationId), ct));

          [HttpGet("{receiptId:guid}/authorization-audit"), Authorize(Policy = "ReceiptAuthorizationRead")]
          public async Task<IActionResult> Audit(Guid receiptId, CancellationToken ct)
              => Ok(await mediator.Send(new GetReceiptAuthorizationAuditQuery(receiptId, ActorId), ct));

          [HttpGet("{receiptId:guid}/posting-status"), Authorize(Policy = "ReceiptAuthorizationRead")]
          public async Task<IActionResult> PostingStatus(Guid receiptId, CancellationToken ct)
              => Ok(await mediator.Send(new GetReceiptPostingStatusQuery(receiptId, ActorId), ct));

          [HttpGet("{receiptId:guid}/trace"), Authorize(Policy = "ReceiptAuthorizationRead")]
          public async Task<IActionResult> Trace(Guid receiptId, CancellationToken ct)
              => Ok(await mediator.Send(new GetReceiptTraceQuery(receiptId, ActorId), ct));

          [HttpGet("{receiptId:guid}/print-eligibility"), Authorize(Policy = "ReceiptPrint")]
          public async Task<IActionResult> PrintEligibility(Guid receiptId, CancellationToken ct)
              => Ok(await mediator.Send(new GetReceiptPrintEligibilityQuery(receiptId, ActorId), ct));

          [HttpPost("internal/{receiptId:guid}/posting-status"), Authorize(Policy = "ReceiptJveCallback")]
          public async Task<IActionResult> Reconcile(Guid receiptId, [FromBody] ReceiptPostingStatusCallback callback, CancellationToken ct)
          {
              if (receiptId != callback.ReceiptId) return Conflict();
              return Ok(await mediator.Send(new ReconcileReceiptPostingStatusCommand(receiptId, callback, ActorId, CorrelationId), ct));
          }
      }
      ```
      **GATE:** Add the final-print action only after the existing FINNOVA-61 print endpoint and document contract are confirmed; never generate a new printable document here. **Requirements:** 1.1–1.11, 4.1–4.20, 5.1–5.12, 9.2, 10.4–10.7, 11.1–11.17, 12.1–12.13.

  - [ ] 9.2 MODIFY `Finnova.AccountsService/Middlewares/ExceptionHandlingMiddleware.cs`
    - Find the exact existing switch anchor:
      ```csharp
      var (status, code, detail) = ex switch
      {
      ```
    - Insert this arm immediately before the existing `FluentValidation.ValidationException` arm:
      ```csharp
      ReceiptAuthorizationException r => (r.StatusCode, r.ErrorCode, r.Message),
      ```
    - Find the exact extension anchor:
      ```csharp
      problem.Extensions["correlationId"] = ctx.TraceIdentifier;
      ```
    - Insert immediately after it:
      ```csharp
      if (ex is ReceiptAuthorizationException receiptAuthorization && receiptAuthorization.Rule is not null)
          problem.Extensions["rule"] = receiptAuthorization.Rule;
      ```
    - Preserve existing GL, Loan Account, Demand, and generic mappings. **Requirements:** 6.1–6.6, 12.1–12.7, 15.1–15.11.

  - [ ] 9.3 MODIFY `Finnova.ApiGateway/appsettings.json`
    - Find the exact existing `"Routes": {` anchor near the start of `ReverseProxy`.
    - Insert these two route objects immediately before the first existing route, preserving valid JSON:
      ```json
      "ua-receipt-authorization-alias-route": {
        "ClusterId": "accounts-cluster",
        "Match": { "Path": "/api/ua/api/receipts/{**catch-all}" },
        "Transforms": [
          { "PathRemovePrefix": "/api/ua/api" },
          { "PathPrefix": "/api" }
        ]
      },
      "ua-receipt-authorization-alias-root": {
        "ClusterId": "accounts-cluster",
        "Match": { "Path": "/api/ua/api/receipts" },
        "Transforms": [
          { "PathRemovePrefix": "/api/ua/api" },
          { "PathPrefix": "/api" }
        ]
      },
      ```
    - If FINNOVA-61 already owns an equivalent receipt alias, modify that existing route instead of adding duplicates. Do not add an alias for `/internal`. **Requirements:** 11.1, 12.13, 18.8.

  - [ ] 9.4 MODIFY `Finnova.AccountsService/Program.cs`
    - Find the exact existing middleware anchor:
      ```csharp
      app.UseMiddleware<Finnova.AccountsService.Middlewares.ExceptionHandlingMiddleware>(); // GL exceptions -> ProblemDetails
      app.UseAuthentication();
      ```
    - Replace only the comment, leaving order unchanged:
      ```csharp
      app.UseMiddleware<Finnova.AccountsService.Middlewares.ExceptionHandlingMiddleware>(); // GL, Receipt Authorization, and validation ProblemDetails
      app.UseAuthentication();
      ```
    - Confirm controllers are discovered by the existing `app.MapControllers()`. Do not add a second authentication scheme or a public JVE worker endpoint. **Requirements:** 12.1–12.13, 15.1–15.11.

- [ ] 10. Frontend models and service layer
  - [ ] 10.1 CREATE `src/services/models/receiptAuthorization.models.ts`
    - Create complete TypeScript models matching the backend JSON contracts, using English string unions, INR number values, ISO dates, and UTC timestamp strings:
      ```ts
      export type AuthorizationDecision = 'PendingAuthorization' | 'Authorized' | 'Rejected';
      export type PostingStatus = 'NotSubmitted' | 'PostingPending' | 'RetryPending' | 'Failed' | 'Posted';
      export type ReceiptBranchRelationship = 'SameBranch' | 'InterBranch';

      export interface ReceiptAuthorizationReviewFilter { page?: number; pageSize?: number; receiptType?: string; receiptMode?: string; collectionBranchId?: string; customerCode?: string; documentNumber?: string; docDateFrom?: string; docDateTo?: string; makerUserId?: string; authorizationDecision?: AuthorizationDecision; postingStatus?: PostingStatus; }
      export interface ReceiptAuthorizationListItem { receiptId: string; documentNumber: string; docDate: string; makerUserId: string; customerCode: string; collectionBranchCode: string; amount: number; receiptType: string; receiptMode: string; authorizationDecision: AuthorizationDecision; postingStatus: PostingStatus; expectedVersion: string; }
      export interface ReceiptAuthorizationLine { lineId: string; lineNo: number; loanAccountId: string; accountNumber: string; componentCode: string; amount: number; accountBranchId: string; accountBranchCode: string; branchRelationship: ReceiptBranchRelationship; }
      export interface ReceiptAuthorizationAuditEntry { id: string; entryKind: string; priorAuthorizationDecision?: AuthorizationDecision; newAuthorizationDecision?: AuthorizationDecision; priorPostingStatus?: PostingStatus; newPostingStatus?: PostingStatus; makerUserId: string; checkerUserId?: string; occurredAtUtc: string; correlationId: string; postingRequestId?: string; canonicalJournalId?: string; safeOutcomeCode?: string; reason?: string; changeSummary: string; }
      export interface ReceiptPrintEligibility { receiptId: string; canPrint: boolean; reasonCode: string; englishReason: string; postingRequestId?: string; canonicalJournalId?: string; isPreview: boolean; }
      export interface ReceiptAuthorizationReview { receiptId: string; documentNumber: string; docDate: string; valueDate: string; receiptType: string; receiptMode: string; currencyCode: 'INR'; amount: number; customerCode: string; customerName: string; makerUserId: string; collectionBranchCode: string; authorizationDecision: AuthorizationDecision; postingStatus: PostingStatus; authorizationRequired: boolean; workflowRuleId?: string; workflowRuleVersion?: string; expectedVersion: string; lines: ReceiptAuthorizationLine[]; auditEntries: ReceiptAuthorizationAuditEntry[]; printEligibility: ReceiptPrintEligibility; }
      export interface AuthorizeReceiptRequest { clientRequestId: string; expectedVersion: string; decisionReason?: string; }
      export interface RejectReceiptRequest { clientRequestId: string; expectedVersion: string; reason: string; }
      export interface ReceiptAuthorizationDecision { receiptId: string; authorizationDecision: AuthorizationDecision; postingStatus: PostingStatus; decisionActorId: string; decisionAtUtc: string; expectedVersion: string; clientRequestId: string; postingRequestId?: string; canonicalJournalId?: string; safeMessage?: string; printingAllowed: boolean; }
      ```
      **Requirements:** 1.10–1.11, 2.9, 4.14–4.16, 5.9–5.10, 9.15, 10.12–10.13, 11.7–11.17.

  - [ ] 10.2 CREATE `src/services/interfaces/receiptAuthorization.interface.ts`
    - Create complete service interface:
      ```ts
      import type { AxiosResponse } from 'axios';
      import type { AuthorizeReceiptRequest, RejectReceiptRequest, ReceiptAuthorizationDecision, ReceiptAuthorizationReview, ReceiptAuthorizationReviewFilter, ReceiptAuthorizationListItem, ReceiptAuthorizationAuditEntry, ReceiptPrintEligibility, PostingStatus } from '../models/receiptAuthorization.models';

      export interface ReceiptAuthorizationService {
        getReviewPage(filter: ReceiptAuthorizationReviewFilter): Promise<{ items: ReceiptAuthorizationListItem[]; totalCount: number }>;
        getReview(receiptId: string): Promise<ReceiptAuthorizationReview>;
        authorize(receiptId: string, request: AuthorizeReceiptRequest): Promise<ReceiptAuthorizationDecision>;
        reject(receiptId: string, request: RejectReceiptRequest): Promise<ReceiptAuthorizationDecision>;
        getAudit(receiptId: string): Promise<ReceiptAuthorizationAuditEntry[]>;
        getPostingStatus(receiptId: string): Promise<{ receiptId: string; postingStatus: PostingStatus; printingAllowed: boolean }>;
        getPrintEligibility(receiptId: string): Promise<ReceiptPrintEligibility>;
      }
      ```
      Remove the unused `AxiosResponse` import if the repository lint configuration rejects unused imports. **Requirements:** 11.1–11.17.

  - [ ] 10.3 CREATE `src/services/real/receiptAuthorization.real.ts`
    - Create complete real implementation using only the shared axios instance from `src/services/api.ts`. Use the exact FINNOVA-61 gateway base/path convention after the UI gate; do not create an axios client.
      ```ts
      import api from '../api';
      import type { ReceiptAuthorizationService } from '../interfaces/receiptAuthorization.interface';
      import type { AuthorizeReceiptRequest, RejectReceiptRequest, ReceiptAuthorizationDecision, ReceiptAuthorizationReview, ReceiptAuthorizationReviewFilter, ReceiptAuthorizationListItem, ReceiptAuthorizationAuditEntry, ReceiptPrintEligibility, PostingStatus } from '../models/receiptAuthorization.models';

      const base = '/receipts';

      export const realReceiptAuthorizationService: ReceiptAuthorizationService = {
        async getReviewPage(filter) { const { data } = await api.get<{ items: ReceiptAuthorizationListItem[]; totalCount: number }>(`${base}/authorization/reviews`, { params: filter }); return data; },
        async getReview(receiptId) { const { data } = await api.get<ReceiptAuthorizationReview>(`${base}/${receiptId}/authorization-review`); return data; },
        async authorize(receiptId, request) { const { data } = await api.post<ReceiptAuthorizationDecision>(`${base}/${receiptId}/authorize`, request); return data; },
        async reject(receiptId, request) { const { data } = await api.post<ReceiptAuthorizationDecision>(`${base}/${receiptId}/reject`, request); return data; },
        async getAudit(receiptId) { const { data } = await api.get<ReceiptAuthorizationAuditEntry[]>(`${base}/${receiptId}/authorization-audit`); return data; },
        async getPostingStatus(receiptId) { const { data } = await api.get<{ receiptId: string; postingStatus: PostingStatus; printingAllowed: boolean }>(`${base}/${receiptId}/posting-status`); return data; },
        async getPrintEligibility(receiptId) { const { data } = await api.get<ReceiptPrintEligibility>(`${base}/${receiptId}/print-eligibility`); return data; },
      };
      ```
      **GATE:** Confirm whether the shared axios base URL already includes `/api/ua/api`; if it does, keep `base = '/receipts'`. If not, use the existing FINNOVA-61 composed path. **Requirements:** 11.1, 11.11, 11.14–11.17, 12.13.

  - [ ] 10.4 CREATE `src/services/mock/receiptAuthorization.mock.ts`, `src/services/receiptAuthorization.service.ts`, `src/services/interfaces/index.ts`, and `src/services/index.ts`
    - Mock data must be English-only, INR with two decimals, UTC timestamps, deterministic, and must model pending, rejected, posting-pending, failed, and posted states. The selector must use `VITE_USE_MOCK_API === 'true'` and otherwise select the real service. Export the service and models through existing barrels without removing unrelated exports.
      ```ts
      // src/services/mock/receiptAuthorization.mock.ts
      import type { ReceiptAuthorizationService } from '../interfaces/receiptAuthorization.interface';
      import type { ReceiptAuthorizationReview, ReceiptAuthorizationReviewFilter, ReceiptAuthorizationDecision, ReceiptAuthorizationListItem, ReceiptAuthorizationAuditEntry, ReceiptPrintEligibility } from '../models/receiptAuthorization.models';

      const receiptId = '00000000-0000-0000-0000-000000000063';
      let review: ReceiptAuthorizationReview = {
        receiptId, documentNumber: 'RRC-2025-0000063', docDate: '2025-01-15', valueDate: '2025-01-15',
        receiptType: 'ThirdParty', receiptMode: 'Cash', currencyCode: 'INR', amount: 1250.00,
        customerCode: 'CUS-0063', customerName: 'Example Customer', makerUserId: 'maker-0063', collectionBranchCode: 'Barka',
        authorizationDecision: 'PendingAuthorization', postingStatus: 'NotSubmitted', authorizationRequired: true,
        workflowRuleId: '00000000-0000-0000-0000-000000000601', workflowRuleVersion: '1', expectedVersion: '1',
        lines: [{ lineId: '00000000-0000-0000-0000-000000000064', lineNo: 1, loanAccountId: '00000000-0000-0000-0000-000000000065', accountNumber: 'LN-0065', componentCode: 'INSTALLMENT_INFLOW', amount: 1250.00, accountBranchId: '00000000-0000-0000-0000-000000000066', accountBranchCode: 'Muscat', branchRelationship: 'InterBranch' }],
        auditEntries: [], printEligibility: { receiptId, canPrint: false, reasonCode: 'AUTHORIZATION_REQUIRED', englishReason: 'Regular printing is unavailable until the receipt is authorized.', isPreview: false },
      };

      const pageItem = (): ReceiptAuthorizationListItem => ({ receiptId, documentNumber: review.documentNumber, docDate: review.docDate, makerUserId: review.makerUserId, customerCode: review.customerCode, collectionBranchCode: review.collectionBranchCode, amount: review.amount, receiptType: review.receiptType, receiptMode: review.receiptMode, authorizationDecision: review.authorizationDecision, postingStatus: review.postingStatus, expectedVersion: review.expectedVersion });
      const nowUtc = () => new Date().toISOString();

      export const mockReceiptAuthorizationService: ReceiptAuthorizationService = {
        async getReviewPage(_filter: ReceiptAuthorizationReviewFilter) { return { items: review.authorizationDecision === 'PendingAuthorization' ? [pageItem()] : [], totalCount: review.authorizationDecision === 'PendingAuthorization' ? 1 : 0 }; },
        async getReview() { return review; },
        async authorize(_id, request) { const response: ReceiptAuthorizationDecision = { receiptId, authorizationDecision: 'Authorized', postingStatus: 'PostingPending', decisionActorId: 'checker-0063', decisionAtUtc: nowUtc(), expectedVersion: '2', clientRequestId: request.clientRequestId, safeMessage: 'Receipt authorized; posting is pending.', printingAllowed: false }; review = { ...review, authorizationDecision: 'Authorized', postingStatus: 'PostingPending', expectedVersion: '2' }; return response; },
        async reject(_id, request) { const response: ReceiptAuthorizationDecision = { receiptId, authorizationDecision: 'Rejected', postingStatus: 'NotSubmitted', decisionActorId: 'checker-0063', decisionAtUtc: nowUtc(), expectedVersion: '2', clientRequestId: request.clientRequestId, safeMessage: request.reason, printingAllowed: false }; review = { ...review, authorizationDecision: 'Rejected', postingStatus: 'NotSubmitted', expectedVersion: '2' }; return response; },
        async getAudit() { return review.auditEntries as ReceiptAuthorizationAuditEntry[]; },
        async getPostingStatus() { return { receiptId, postingStatus: review.postingStatus, printingAllowed: review.printEligibility.canPrint }; },
        async getPrintEligibility() { return review.printEligibility as ReceiptPrintEligibility; },
      };
      ```

      ```ts
      // src/services/receiptAuthorization.service.ts
      import type { ReceiptAuthorizationService } from './interfaces/receiptAuthorization.interface';
      import { realReceiptAuthorizationService } from './real/receiptAuthorization.real';
      import { mockReceiptAuthorizationService } from './mock/receiptAuthorization.mock';

      export const receiptAuthorizationService: ReceiptAuthorizationService = import.meta.env.VITE_USE_MOCK_API === 'true'
        ? mockReceiptAuthorizationService
        : realReceiptAuthorizationService;
      ```

      **MODIFY** `src/services/interfaces/index.ts` by inserting:
      ```ts
      export type { ReceiptAuthorizationService } from './receiptAuthorization.interface';
      ```
      **MODIFY** `src/services/index.ts` by inserting:
      ```ts
      export { receiptAuthorizationService } from './receiptAuthorization.service';
      export * from './models/receiptAuthorization.models';
      ```
      **GATE:** Preserve the existing barrel contents and exact mock conventions from FINNOVA-61. **Requirements:** 2.9, 4.14–4.16, 5.9–5.10, 9.15, 10.12–10.13, 11.13–11.14.

- [ ] 11. Frontend screen, components, route, and navigation
  - [ ] 11.1 CREATE `src/pages/ReceiptAuthorization/ReceiptAuthorizationStatusChips.tsx`, `ReceiptAuthorizationLinesGrid.tsx`, and `ReceiptAuthorizationAuditTimeline.tsx`
    - Use MUI only. Render separate Authorization and Posting chips, line Account Branch and Same/Inter-Branch values, and descending UTC audit entries. Do not add RTL, translation providers, or non-English fixture data.
    - **GATE:** Match existing MUI Data Grid import and theme conventions from `LocationMaster.tsx`/`LookupMaster.tsx` after the UI repository is available. **Requirements:** 1.10–1.11, 2.9, 9.15, 11.7, 11.15–11.17, 13.8.

  - [ ] 11.2 CREATE `src/pages/ReceiptAuthorization/ReceiptAuthorizationDetail.tsx`, `ReceiptDecisionDialog.tsx`, `ReceiptRejectionDialog.tsx`, and `ReceiptPrintControl.tsx`
    - Build read-only detail, authorize confirmation, required bounded English rejection reason, and server-side print eligibility control. Disable duplicate submissions while pending; preserve review context on errors; refresh expected version on stale responses; enable Regular Print only for a valid Posted response with journal reference. Format amounts with `Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', minimumFractionDigits: 2, maximumFractionDigits: 2 })` and label technical timestamps `UTC`.
      ```tsx
      export const formatInr = (value: number) => new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value);
      export const formatUtc = (value: string) => `${new Date(value).toISOString()} UTC`;
      ```
      **Requirements:** 1.10–1.11, 2.9, 3.9–3.10, 4.14–4.16, 5.9–5.10, 9.15, 10.1–10.13, 11.7–11.17.

  - [ ] 11.3 CREATE `src/pages/ReceiptAuthorization/ReceiptAuthorizationReviewGrid.tsx` and `src/pages/ReceiptAuthorization/ReceiptAuthorizationErrorAlert.tsx`
    - Implement server-side pagination and supported filters. Display pending receipt, maker, customer, collection branch, amount, type, mode, and posting status. Display the exact self-authorization message unchanged. Keep inaccessible-record errors generic and preserve the selected review context.
      **Requirements:** 1.1–1.10, 6.6–6.7, 11.7, 11.10–11.12.

  - [ ] 11.4 CREATE `src/pages/ReceiptAuthorization/ReceiptAuthorization.tsx`
    - Compose the grid/detail/dialog/audit/status/printing components with React hooks only. On successful authorization show separate `Authorized` and `Posting Pending` states and disable both decisions. On rejection show status and reason. A Third-Party line-level requirement displays that the complete receipt requires authorization. Do not add Redux, RxJS, or a second state store.
      **Requirements:** 2.9, 4.14–4.16, 5.9–5.12, 9.15, 10.12–10.13, 11.7–11.12.

  - [ ] 11.5 MODIFY `src/App.tsx`
    - **GATE:** Confirm the existing protected-route anchor from the UI repository. Insert the route alongside the existing Operations/master-data routes:
      ```tsx
      <Route path="/receipts/authorization" element={<ReceiptAuthorization />} />
      ```
    - Add the import using the confirmed page path. Preserve the existing authorization wrapper. **Requirements:** 11.7, 12.11.

  - [ ] 11.6 MODIFY `src/components/Layout.tsx`
    - **GATE:** Confirm the existing Operations navigation array anchor. Insert one English-only entry:
      ```ts
      { label: 'Receipt Authorization', path: '/receipts/authorization' },
      ```
    - Preserve existing navigation entries and icon conventions. **Requirements:** 11.7, 11.17.

- [ ] 12. Backend automated tests
  - [ ]* 12.1 CREATE `Finnova.Tests/ReceiptAuthorization/ReceiptAuthorizationPropertyTests.cs`
    - Use FsCheck.Xunit with `MaxTest = 100` or higher. Add one property per design property and the exact required comment form:
      ```csharp
      // Feature: receipt-authorization-inter-branch-accounting, Property 1: Third-Party authorization is aggregate and deterministic
      // Feature: receipt-authorization-inter-branch-accounting, Property 2: Self-authorization uses stable identity only
      // Feature: receipt-authorization-inter-branch-accounting, Property 3: Branch-context construction preserves receipt amounts and line traceability
      // Feature: receipt-authorization-inter-branch-accounting, Property 4: Inter-branch pairs are equal and complete-request totals balance
      // Feature: receipt-authorization-inter-branch-accounting, Property 5: Valid lifecycle transitions are one-way at the decision boundary
      // Feature: receipt-authorization-inter-branch-accounting, Property 6: Idempotent decision commands have one outcome
      // Feature: receipt-authorization-inter-branch-accounting, Property 7: Status reconciliation accepts only the matching immutable request
      // Feature: receipt-authorization-inter-branch-accounting, Property 8: Printing is unlocked only by a complete Posted state
      ```
      Use pure helpers/fakes only. Do not randomize against a live database, gateway, or JVE. **Requirements:** 2.1–2.5, 6.1–6.11, 7.1–7.6, 8.5–8.6, 9.10–9.14, 10.1–10.7, 14.1–14.13, design Properties 1–8.

  - [ ]* 12.2 CREATE `Finnova.Tests/ReceiptAuthorization/ReceiptAuthorizationUnitTests.cs`
    - Add xUnit facts/theories for the exact self-authorization message, missing/overlong rejection reason, invalid status, missing/tied workflow, unresolved Account Branch, missing inter-branch configuration, INR-only amounts, payload hash stability, all print-gate states, and safe error mapping. The exact assertion is:
      ```csharp
      Assert.Equal("Maker cannot authorize their own receipt", exception.Message);
      ```
      **Requirements:** 2.6–2.8, 5.7–5.8, 6.1–6.11, 7.7–7.10, 10.1–10.13, 15.1–15.11.

  - [ ]* 12.3 CREATE `Finnova.Tests/ReceiptAuthorization/ReceiptAuthorizationApiTests.cs`
    - Use `WebApplicationFactory<Program>` and controlled test services. Cover missing/invalid JWT = 401 with no mutation, missing role = 403, out-of-scope = 403 without disclosure, review filters/defaults/order, authorize/reject responses, exact status separation, and read-only audit/trace/print operations. Use a fake JVE submission port and never write canonical journals in Accounts tests. **Requirements:** 1.1–1.11, 4.1–4.20, 5.1–5.12, 6.1–6.11, 11.1–11.17, 12.1–12.13.

  - [ ]* 12.4 CREATE `Finnova.Tests/ReceiptAuthorization/ReceiptAuthorizationConcurrencyTests.cs` and `Finnova.Tests/ReceiptAuthorization/ReceiptAuthorizationReconciliationTests.cs`
    - Cover concurrent authorize/authorize and authorize/reject with one terminal decision and one JVE request, identical ClientRequestId replay, changed-payload idempotency conflict, pre-commit rollback, RetryPending, DeadLetter/Failed, Posted, duplicate callback, mismatched request/hash integrity event, and Posted-without-journal print lock. **Requirements:** 4.5, 4.8, 4.11, 4.17–4.20, 9.4–9.14, 13.1–13.13, 14.1–14.13.

  - [ ]* 12.5 CREATE `Finnova.Tests/ReceiptAuthorization/ReceiptAuthorizationGatewayTests.cs`
    - Add gateway-compatible integration coverage for `/api/ua/api/receipts/**`, Accounts forwarding, Authorization header preservation, root and catch-all alias transforms, and absence of an internal callback alias. Include REC-101 Barka-to-Muscat, multiple Account Branch lines, same-branch lines, Third-Party aggregate hold, and final print lock/unlock scenarios. **Requirements:** 7.1–7.11, 8.1–8.15, 11.1, 12.13, 17.1–17.15, 18.1–18.8.

- [ ] 13. Frontend automated tests
  - [ ]* 13.1 CREATE `src/pages/ReceiptAuthorization/ReceiptAuthorization.test.tsx`
    - Use Vitest and React Testing Library. Cover pending list/detail, read-only fields, Account Branch per line, aggregate Third-Party message, authorize/reject interactions, expected version and ClientRequestId, duplicate-submit disabling, error-context preservation, stale refresh, exact self-authorization text, separate Authorized/Posting Pending states, rejection reason, retry/failed print lock, Posted print enablement, mock/real toggle, INR two decimals, and UTC labels. Fixtures must be English-only and contain no RTL data. **Requirements:** 1.10–1.11, 2.9, 3.9–3.10, 4.14–4.16, 5.9–5.12, 6.6–6.7, 9.15, 10.12–10.13, 11.7–11.17.

  - [ ]* 13.2 CREATE `src/services/receiptAuthorization.service.test.ts`
    - Verify the mock service deterministic transitions and the real service URL, HTTP method, payload shape, shared axios usage, and `VITE_USE_MOCK_API` selection. Assert that request bodies contain no Checker identity. **Requirements:** 11.1–11.6, 11.13–11.14, 12.8–12.13.

  - [ ]* 13.3 MODIFY `src/test/setup.ts` or the existing Vitest setup file
    - **GATE:** Use the exact setup path from the UI repository. Add only the existing MUI/RTL test setup needed by the new page. Do not add Redux, RxJS, localization, or RTL direction setup. **Requirements:** 11.7, 11.15–11.17.

  - [ ]* 13.4 MODIFY `package.json` and `vitest.config.ts` only if the existing UI test scripts do not already support Vitest + jsdom
    - **GATE:** Preserve existing scripts and dependencies. Add the minimum existing-project convention for a one-shot test command; do not run it in this workflow. Copyable verification command:
      ```powershell
      npm test -- --run
      ```
      **Requirements:** 11.7–11.17, 18.8.

## Copyable migration and verification commands

Do not execute these commands during spec creation. Run them only after all gates are confirmed and the source changes are applied.

```powershell
# From d:\Projects\Finnova\Finnova-API
 dotnet ef migrations add AddReceiptAuthorizationLifecycle --project Finnova.Repository --startup-project Finnova.AccountsService --output-dir Migrations
 dotnet ef database update --project Finnova.Repository --startup-project Finnova.AccountsService
 dotnet build Finnova.Backend.slnx
 dotnet test Finnova.Tests --configuration Release
```

```powershell
# From E:\Finnova\Finnova-UI\Finnova-UI
 npm test -- --run
 npm run build
```

Before applying the migration, review the generated SQL and verify that it changes only the existing Receipt lifecycle plus the two feature-owned authorization tables. Confirm that no Journal Header, Journal Line, source-link, Cashflow Master, GL Master, or duplicate JVE inbox table is created.

## Notes

- Tasks marked `*` are optional test tasks under the workspace task convention; the implementation tasks are not optional.
- The design properties are tested separately from example/integration tests. Every FsCheck property must run at least 100 iterations.
- All external adapter gates are intentional. A blocking adapter is safer than silently treating missing workflow, branch, GL, JVE, role, or print configuration as valid.
- The JVE owns canonical journal creation, Cashflow Master resolution, GL resolution, retry/dead-letter processing, and final posting atomicity. Accounts owns authorization state, immutable request identity, receipt-facing status, audit reference, and printing eligibility.
- No source file, migration, build, or test was executed while creating this document.
