# Implementation Plan: Receipt Processing Controls

**Feature:** `receipt-processing-controls` (Jira FINNOVA-64)  
**Source documents:** `requirements.md`, `design.md`  
**Backend repository:** `E:\Finnova\Finnova-API`  
**Frontend repository:** `E:\Finnova\Finnova-UI\Finnova-UI`  

## Execution contract

This file is the copy-and-paste implementation script. Apply tasks in order. Every implementation step is explicitly marked `CREATE <path>` or `MODIFY <path>`. A `CREATE` task contains the complete content of the new file. A `MODIFY` task contains the exact anchor and replacement/insertion snippet. No task in this document is executed while producing the specification.

All amounts are INR with two fractional digits, technical timestamps are UTC, and all user-facing values and tests are English-only. Do not add Redux, RxJS, RTL handling, translation scaffolding, a second axios client, a parallel `Receipt` entity/table, a second authorization model, a second JVE queue, or canonical journal writes in the receipt-control service.

## Mandatory compatibility gate

Stop before applying Task 1.1 and record the confirmed contract in the task notes if any check fails. The gate is mandatory because the repository currently contains no FINNOVA-61 or FINNOVA-63 executable receipt contracts.

- FINNOVA-61 must provide the authoritative `Receipt` aggregate, child details, document number and numbering scope, company/LOB/branch ownership, creator metadata, receipt status, existing row-version/concurrency token, receipt audit relationship, durable challan relationship, and ordinary receipt read path.
- FINNOVA-63 must provide the authoritative authorization decision, posting status, accrual/source-link facts, approved Cheque Return/JV reversal boundary, and authoritative first-print/print-eligibility contract.
- Bind all snippets below to the final confirmed names before copying. If the final type uses a different name, update the exact snippet and record the mapping. Do not create a guessed substitute.
- The expected control contract is: FINNOVA-61 owns `Receipt`; FINNOVA-64 adds only control-owned fields and child/history records; FINNOVA-63 remains the owner of authorization/posting/canonical accounting state; the existing row-version is exposed as an opaque `ReceiptVersion` string.

## Overview

The backend slice is hosted in `Finnova.AccountsService` under `api/receipts`. The UI calls it through the existing shared axios base URL and a YARP alias under `/api/ua/api/receipts/**`. The server performs every permission, state, CAS, accounting, retirement, and duplicate-number check. The UI only reflects `availableActions` and returned state.

## Tasks

- [ ] 1. Bind the FINNOVA-61/63 contracts before adding control code
  - [ ] 1.1 MODIFY `E:\Finnova\Finnova-API\Finnova.Models\Domain\Entities\Receipt.cs`
    - **GATE:** Apply only after the FINNOVA-61 Receipt file exists and the final contract confirms the exact anchor. Do not create a second Receipt class.
    - Find the final FINNOVA-61 concurrency/status area. The expected anchor is:
      ```csharp
      public byte[] RowVersion { get; set; } = Array.Empty<byte>();
      ```
    - Insert only the fields confirmed as FINNOVA-64-owned and still absent:
      ```csharp
      public string? CancellationReason { get; set; }
      public string? CancelledBy { get; set; }
      public DateTime? CancelledAtUtc { get; set; }
      public string? AccrualOutcome { get; set; }
      public Guid? ReversalRequestId { get; set; }
      public string? ReversalSourceLink { get; set; }
      public int DuplicationCount { get; set; }
      public DateTime? LastDuplicatePrintedAtUtc { get; set; }
      ```
    - Preserve the final FINNOVA-61/63 authorization, posting, challan, print, document-number, creator, and accounting-link fields. Do not add a competing `int ReceiptVersion`; the existing `RowVersion` is the Receipt Version. If any listed field is already owned by FINNOVA-61 or FINNOVA-63, do not add it twice.
    - **Requirements:** 1.1-1.7, 3.2-3.6, 4.1-4.7, 6.1-6.15, 7.1-7.16, 8.1-8.11.

- [ ] 2. Add the control domain types and pure logic
  - [ ] 2.1 CREATE `E:\Finnova\Finnova-API\Finnova.Models\Domain\Enums\ReceiptControlEnums.cs`
    - Create this complete file:
      ```csharp
      namespace Finnova.Models.Domain.Enums;

      public enum ReceiptControlOperation
      {
          Modify = 0,
          Cancel = 1,
          Reprint = 2
      }

      public enum ReceiptAccrualOutcome
      {
          NotEvaluated = 0,
          NoReversalRequired = 1,
          ReversalRequired = 2,
          CancellationPending = 3
      }

      public enum ReceiptDuplicateOutputState
      {
          Created = 0,
          Failed = 1
      }
      ```
    - Do not add authorization or posting enum values already owned by FINNOVA-63.

  - [ ] 2.2 CREATE `E:\Finnova\Finnova-API\Finnova.Models\Domain\Entities\ReceiptControlEntities.cs`
    - Create this complete file. These are control records, not a replacement Receipt or journal model:
      ```csharp
      using Finnova.Models.Domain.Enums;

      namespace Finnova.Models.Domain.Entities;

      public sealed class ReceiptDocumentRetirement
      {
          public Guid Id { get; set; } = Guid.NewGuid();
          public Guid ReceiptId { get; set; }
          public string DocumentNumber { get; set; } = string.Empty;
          public string NumberingMode { get; set; } = string.Empty;
          public string NumberingScopeKey { get; set; } = string.Empty;
          public string ReceiptType { get; set; } = string.Empty;
          public string RetiredBy { get; set; } = string.Empty;
          public DateTime RetiredAtUtc { get; set; } = DateTime.UtcNow;
          public string CancellationReason { get; set; } = string.Empty;
      }

      public sealed class ReceiptDuplicateCopy
      {
          public Guid Id { get; set; } = Guid.NewGuid();
          public Guid ReceiptId { get; set; }
          public string DocumentNumber { get; set; } = string.Empty;
          public int DuplicateCopyNumber { get; set; }
          public int DuplicationCountAfter { get; set; }
          public string OutputTitle { get; set; } = "DUPLICATE RECEIPT";
          public string WatermarkText { get; set; } = "DUPLICATE RECEIPT";
          public string OutputReference { get; set; } = string.Empty;
          public string PrintedBy { get; set; } = string.Empty;
          public DateTime PrintedAtUtc { get; set; } = DateTime.UtcNow;
          public Guid IdempotencyKey { get; set; }
          public ReceiptDuplicateOutputState State { get; set; } = ReceiptDuplicateOutputState.Created;
      }

      public sealed class ReceiptControlOperation
      {
          public Guid Id { get; set; } = Guid.NewGuid();
          public Guid IdempotencyKey { get; set; }
          public Guid ReceiptId { get; set; }
          public ReceiptControlOperation Operation { get; set; }
          public string RequestHash { get; set; } = string.Empty;
          public string ResponseSnapshotJson { get; set; } = string.Empty;
          public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
          public DateTime? CompletedAtUtc { get; set; }
      }
      ```

  - [ ] 2.3 CREATE `E:\Finnova\Finnova-API\Finnova.Models\Domain\Exceptions\ReceiptControlExceptions.cs`
    - Create this complete file:
      ```csharp
      namespace Finnova.Models.Domain.Exceptions;

      public static class ReceiptControlRuleKeys
      {
          public const string ChallanProtected = "CHALLAN_PROTECTED";
          public const string AuthorizedReceiptProtected = "AUTHORIZED_RECEIPT_PROTECTED";
          public const string InvalidReceiptState = "INVALID_RECEIPT_STATE";
          public const string ReceiptVersionConflict = "RECEIPT_VERSION_CONFLICT";
          public const string DocumentNumberRetired = "DOCUMENT_NUMBER_RETIRED";
          public const string AccountingReversalUnavailable = "ACCOUNTING_REVERSAL_UNAVAILABLE";
          public const string PrintNotEligible = "PRINT_NOT_ELIGIBLE";
          public const string IdempotencyConflict = "IDEMPOTENCY_CONFLICT";
          public const string ScopeDenied = "SCOPE_DENIED";
          public const string ImmutableRecord = "IMMUTABLE_RECORD";
      }

      public abstract class ReceiptControlException(string message, string? rule = null) : Exception(message)
      {
          public abstract string ErrorCode { get; }
          public abstract int StatusCode { get; }
          public string? Rule { get; } = rule;
      }

      public sealed class ReceiptControlValidationException(string message) : ReceiptControlException(message)
      {
          public override string ErrorCode => "ERR-RCC-400";
          public override int StatusCode => 400;
      }

      public sealed class ReceiptControlNotFoundException(string message) : ReceiptControlException(message)
      {
          public override string ErrorCode => "ERR-RCC-404";
          public override int StatusCode => 404;
      }

      public sealed class ReceiptControlForbiddenException(string message) : ReceiptControlException(message, ReceiptControlRuleKeys.ScopeDenied)
      {
          public override string ErrorCode => "ERR-RCC-403";
          public override int StatusCode => 403;
      }

      public sealed class ReceiptControlConflictException(string message, string rule) : ReceiptControlException(message, rule)
      {
          public override string ErrorCode => "ERR-RCC-409";
          public override int StatusCode => 409;
      }

      public sealed class ReceiptControlBusinessRuleException(string message, string rule) : ReceiptControlException(message, rule)
      {
          public override string ErrorCode => "ERR-RCC-422";
          public override int StatusCode => 422;
      }

      public sealed class ReceiptControlDependencyException(string message) : ReceiptControlException(message)
      {
          public override string ErrorCode => "ERR-RCC-503";
          public override int StatusCode => 503;
      }
      ```

  - [ ] 2.4 CREATE `E:\Finnova\Finnova-API\Finnova.Service\ReceiptControls\Domain\ReceiptControlRules.cs`
    - Create this complete pure helper file:
      ```csharp
      using System.Security.Cryptography;
      using System.Text;
      using System.Text.Json;
      using Finnova.Models.Domain.Exceptions;

      namespace Finnova.Service.ReceiptControls.Domain;

      public sealed record PrintMarker(int DuplicateCopyNumber, int DuplicationCount, string Title, string Watermark);
      public sealed record CancellationDecision(string NormalizedReason, bool RequiresReversal);

      public static class ReceiptControlRules
      {
          public const string DuplicateTitle = "DUPLICATE RECEIPT";

          public static string NormalizeCancellationReason(string reason)
          {
              var value = reason?.Trim() ?? string.Empty;
              if (value.Length == 0) throw new ReceiptControlValidationException("Cancellation Reason is required.");
              if (value.Length > 500) throw new ReceiptControlValidationException("Cancellation Reason must be 500 characters or fewer.");
              return value;
          }

          public static PrintMarker AllocateDuplicateMarker(int currentCount)
          {
              if (currentCount < 0) throw new ArgumentOutOfRangeException(nameof(currentCount));
              var next = checked(currentCount + 1);
              return new PrintMarker(next, next, DuplicateTitle, DuplicateTitle);
          }

          public static CancellationDecision DecideCancellation(bool hasAccrual, string reason)
              => new(NormalizeCancellationReason(reason), hasAccrual);

          public static string Hash<T>(T value)
          {
              var json = JsonSerializer.Serialize(value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
              return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
          }
      }
      ```
    - **Property reference:** `Feature: receipt-processing-controls, Property 2` and `Property 4`.

- [ ] 3. Extend the FINNOVA-61 audit relationship without replacing it
  - [ ] 3.1 MODIFY `E:\Finnova\Finnova-API\Finnova.Models\Domain\Entities\ReceiptAuditEntry.cs`
    - **GATE:** Use the final FINNOVA-61 audit entity. If the final entity already has these fields, do not duplicate them.
    - Locate the existing operation/action and change-summary fields and insert only the missing control context:
      ```csharp
      public string? PriorStatus { get; set; }
      public string? NewStatus { get; set; }
      public string? CancellationReason { get; set; }
      public string? BeforeValuesJson { get; set; }
      public string? AfterValuesJson { get; set; }
      public Guid? RetirementRecordId { get; set; }
      public Guid? ReversalRequestId { get; set; }
      public int? DuplicateCopyNumber { get; set; }
      public Guid? IdempotencyKey { get; set; }
      ```
    - Preserve the existing append-only audit identity and correlation fields. The audit repository must remain add/read only.
    - **Requirements:** 3.4, 4.5, 5.7, 6.4, 7.10, 9.11-9.13, 10.12-10.18.

- [ ] 4. Add backend contracts and authoritative ports
  - [ ] 4.1 CREATE `E:\Finnova\Finnova-API\Finnova.Models\Contracts\ReceiptControls\ReceiptControlContracts.cs`
    - Create this complete file after mapping enum/status names to FINNOVA-61/63:
      ```csharp
      namespace Finnova.Models.Contracts.ReceiptControls;

      public sealed record ReceiptControlSummaryResponse(
          Guid ReceiptId, string DocumentNumber, string ReceiptStatus, string AuthorizationStatus,
          bool IsChallaned, string? ChallanReference, bool IsPrinted, int DuplicationCount,
          string ReceiptVersion, Guid CollectionBranchId, string CollectionBranchCode,
          IReadOnlyList<string> AvailableActions, bool CanModify, bool CanCancel, bool CanReprint,
          bool RequiresAccountingReversal, string? AccountingCorrectionInstruction);

      public sealed record ModifyReceiptControlRequest(
          string ExpectedReceiptVersion, Guid IdempotencyKey,
          IReadOnlyDictionary<string, string?> MutableFields);

      public sealed record CancelReceiptRequest(
          string ExpectedReceiptVersion, Guid IdempotencyKey, string CancellationReason);

      public sealed record DuplicateReceiptOutputResponse(
          Guid ReceiptId, string DocumentNumber, string OutputTitle, string WatermarkText,
          int DuplicateCopyNumber, int DuplicationCount, DateTime PrintedAtUtc,
          string OutputReference);

      public sealed record ReceiptAuditEntryResponse(
          Guid Id, Guid ReceiptId, string Operation, string DocumentNumber, string Actor,
          string? PriorStatus, string? NewStatus, string? CancellationReason,
          string? BeforeValuesJson, string? AfterValuesJson, Guid? RetirementRecordId,
          Guid? ReversalRequestId, int? DuplicateCopyNumber, DateTime OccurredAtUtc);
      ```

  - [ ] 4.2 CREATE `E:\Finnova\Finnova-API\Finnova.Service\ReceiptControls\Ports\ReceiptControlPorts.cs`
    - Create this complete file. Replace only the marked adapter return types with the final FINNOVA-63 types when the gate confirms them:
      ```csharp
      using Finnova.Models.Contracts.ReceiptControls;

      namespace Finnova.Service.ReceiptControls.Ports;

      public sealed record ReceiptChallanFact(bool IsChallaned, string? ChallanReference);
      public sealed record ReceiptAccrualFact(bool HasAccrual, string? SourceLink, decimal? Amount, DateOnly? ValueDate, Guid? PostingRequestId, string? CurrencyCode);
      public sealed record ReceiptReversalRequest(Guid IdempotencyKey, Guid ReceiptId, string DocumentNumber, string SourceLink, decimal Amount, string CurrencyCode, DateOnly ValueDate, Guid CollectionBranchId, string ActorId, DateTime CancellationAtUtc, string CancellationReason);
      public sealed record ReversalAcceptance(bool Accepted, Guid? ReversalRequestId, string? SourceLink, string? StatusCode, string? EnglishMessage);
      public sealed record ReceiptPrintFact(bool IsPrinted, bool IsEligible, string? FailureReason);
      public sealed record ReceiptScopeFact(bool CanRead, bool CanModify, bool CanCancel, bool CanReprint);

      public interface IReceiptChallanPort
      {
          Task<ReceiptChallanFact> GetAsync(Guid receiptId, CancellationToken cancellationToken);
      }

      public interface IReceiptAccountingBoundary
      {
          Task<ReceiptAccrualFact> GetAccrualAsync(Guid receiptId, CancellationToken cancellationToken);
          Task<ReversalAcceptance> AcceptReversalAsync(ReceiptReversalRequest request, CancellationToken cancellationToken);
      }

      public interface IReceiptPrintPort
      {
          Task<ReceiptPrintFact> GetEligibilityAsync(Guid receiptId, string actorId, CancellationToken cancellationToken);
          Task<string> CreateDuplicateOutputReferenceAsync(Guid receiptId, int duplicateCopyNumber, CancellationToken cancellationToken);
      }

      public interface IReceiptScopePort
      {
          Task<ReceiptScopeFact> ResolveAsync(Guid actorId, Guid receiptId, CancellationToken cancellationToken);
      }

      public interface IReceiptControlService
      {
          Task<ReceiptControlSummaryResponse> GetSummaryAsync(Guid receiptId, string actorId, CancellationToken cancellationToken);
          Task<ReceiptControlSummaryResponse> ModifyAsync(Guid receiptId, ModifyReceiptControlRequest request, string actorId, string correlationId, CancellationToken cancellationToken);
          Task<ReceiptControlSummaryResponse> CancelAsync(Guid receiptId, CancelReceiptRequest request, string actorId, string correlationId, CancellationToken cancellationToken);
          Task<DuplicateReceiptOutputResponse> ReprintAsync(Guid receiptId, Guid idempotencyKey, string actorId, string correlationId, CancellationToken cancellationToken);
          Task<IReadOnlyList<ReceiptAuditEntryResponse>> GetAuditAsync(Guid receiptId, string actorId, CancellationToken cancellationToken);
      }
      ```

  - [ ] 4.3 CREATE `E:\Finnova\Finnova-API\Finnova.Service\ReceiptControls\Ports\UnconfiguredReceiptControlAdapters.cs`
    - Create this complete fail-closed file until FINNOVA-63 owners are confirmed:
      ```csharp
      using Finnova.Models.Domain.Exceptions;

      namespace Finnova.Service.ReceiptControls.Ports;

      public sealed class UnconfiguredReceiptChallanPort : IReceiptChallanPort
      {
          public Task<ReceiptChallanFact> GetAsync(Guid receiptId, CancellationToken cancellationToken) =>
              throw new ReceiptControlDependencyException("Receipt challan adapter is not configured.");
      }

      public sealed class UnconfiguredReceiptAccountingBoundary : IReceiptAccountingBoundary
      {
          public Task<ReceiptAccrualFact> GetAccrualAsync(Guid receiptId, CancellationToken cancellationToken) =>
              throw new ReceiptControlDependencyException("Receipt accounting boundary is not configured.");
          public Task<ReversalAcceptance> AcceptReversalAsync(ReceiptReversalRequest request, CancellationToken cancellationToken) =>
              throw new ReceiptControlDependencyException("Receipt reversal boundary is not configured.");
      }

      public sealed class UnconfiguredReceiptPrintPort : IReceiptPrintPort
      {
          public Task<ReceiptPrintFact> GetEligibilityAsync(Guid receiptId, string actorId, CancellationToken cancellationToken) =>
              throw new ReceiptControlDependencyException("Receipt print boundary is not configured.");
          public Task<string> CreateDuplicateOutputReferenceAsync(Guid receiptId, int duplicateCopyNumber, CancellationToken cancellationToken) =>
              throw new ReceiptControlDependencyException("Receipt print boundary is not configured.");
      }
      ```

- [ ] 5. Add persistence configuration and transaction support
  - [ ] 5.1 CREATE `E:\Finnova\Finnova-API\Finnova.Repository\Configuration\ReceiptControlConfiguration.cs`
    - Create this complete file:
      ```csharp
      using Finnova.Models.Domain.Entities;
      using Microsoft.EntityFrameworkCore;
      using Microsoft.EntityFrameworkCore.Metadata.Builders;

      namespace Finnova.Repository.Configuration;

      public sealed class ReceiptDocumentRetirementConfiguration : IEntityTypeConfiguration<ReceiptDocumentRetirement>
      {
          public void Configure(EntityTypeBuilder<ReceiptDocumentRetirement> b)
          {
              b.ToTable("receipt_document_retirements");
              b.HasKey(x => x.Id);
              b.Property(x => x.DocumentNumber).HasMaxLength(100).IsRequired();
              b.Property(x => x.NumberingMode).HasMaxLength(20).IsRequired();
              b.Property(x => x.NumberingScopeKey).HasMaxLength(200).IsRequired();
              b.Property(x => x.ReceiptType).HasMaxLength(30).IsRequired();
              b.Property(x => x.RetiredBy).HasMaxLength(200).IsRequired();
              b.Property(x => x.CancellationReason).HasMaxLength(500).IsRequired();
              b.HasIndex(x => new { x.NumberingScopeKey, x.DocumentNumber }).IsUnique();
              b.HasIndex(x => x.ReceiptId).IsUnique();
          }
      }

      public sealed class ReceiptDuplicateCopyConfiguration : IEntityTypeConfiguration<ReceiptDuplicateCopy>
      {
          public void Configure(EntityTypeBuilder<ReceiptDuplicateCopy> b)
          {
              b.ToTable("receipt_duplicate_copies");
              b.HasKey(x => x.Id);
              b.Property(x => x.DocumentNumber).HasMaxLength(100).IsRequired();
              b.Property(x => x.OutputTitle).HasMaxLength(100).IsRequired();
              b.Property(x => x.WatermarkText).HasMaxLength(100).IsRequired();
              b.Property(x => x.OutputReference).HasMaxLength(500).IsRequired();
              b.Property(x => x.PrintedBy).HasMaxLength(200).IsRequired();
              b.HasIndex(x => new { x.ReceiptId, x.DuplicateCopyNumber }).IsUnique();
              b.HasIndex(x => new { x.ReceiptId, x.IdempotencyKey }).IsUnique();
          }
      }

      public sealed class ReceiptControlOperationConfiguration : IEntityTypeConfiguration<ReceiptControlOperation>
      {
          public void Configure(EntityTypeBuilder<ReceiptControlOperation> b)
          {
              b.ToTable("receipt_control_operations");
              b.HasKey(x => x.Id);
              b.Property(x => x.RequestHash).HasMaxLength(128).IsRequired();
              b.Property(x => x.ResponseSnapshotJson).HasMaxLength(16000).IsRequired();
              b.HasIndex(x => x.IdempotencyKey).IsUnique();
              b.HasIndex(x => new { x.ReceiptId, x.Operation, x.CreatedAtUtc });
          }
      }
      ```

  - [ ] 5.2 MODIFY `E:\Finnova\Finnova-API\Finnova.Repository\Context\FinnovaDbContext.cs`
    - Find the existing anchor:
      ```csharp
      public DbSet<DemandAuditEntry> DemandAuditEntries => Set<DemandAuditEntry>();
      ```
    - Insert immediately after it:
      ```csharp
          public DbSet<ReceiptDocumentRetirement> ReceiptDocumentRetirements => Set<ReceiptDocumentRetirement>();
          public DbSet<ReceiptDuplicateCopy> ReceiptDuplicateCopies => Set<ReceiptDuplicateCopy>();
          public DbSet<ReceiptControlOperation> ReceiptControlOperations => Set<ReceiptControlOperation>();
      ```
    - The final FINNOVA-61 Receipt DbSet and FINNOVA-63 DbSets must remain the only Receipt and JVE aggregate DbSets. Do not add `DbSet<Receipt>` here if FINNOVA-61 already added it.

  - [ ] 5.3 MODIFY `E:\Finnova\Finnova-API\Finnova.Repository\Interfaces\IApplicationUnitOfWork.cs`
    - Replace the complete existing file:
      ```csharp
      using Finnova.Repository.Context;
      using Microsoft.EntityFrameworkCore.Storage;

      namespace Finnova.Repository.Interfaces;

      public interface IApplicationUnitOfWork
      {
          FinnovaDbContext Context { get; }
          Task<int> SaveChangesAsync(CancellationToken ct = default);
          Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default);
      }
      ```
    - MODIFY `E:\Finnova\Finnova-API\Finnova.Repository\Repositories\ApplicationUnitOfWork.cs` at `BeginTransactionAsync`:
      ```csharp
      public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default)
          => await Context.Database.BeginTransactionAsync(ct);
      ```
    - Preserve existing `SaveChangesAsync`. Handlers own commit/rollback; repositories do not call save independently.

  - [ ] 5.4 MODIFY `E:\Finnova\Finnova-API\Finnova.Repository\Migrations\<generated AddReceiptProcessingControls migration>`
    - **GATED GENERATED ARTIFACT:** This task cannot contain copy-paste migration source because the final FINNOVA-61/63 schema is an external prerequisite and provider-specific SQL must not be fabricated. Stop if the compatibility gate is not confirmed.
    - From `E:\Finnova\Finnova-API`, run only after Tasks 5.1-5.3 and final FINNOVA-61/63 model confirmation:
      ```powershell
      dotnet ef migrations add AddReceiptProcessingControls --project Finnova.Repository --startup-project Finnova.AccountsService --output-dir Migrations
      ```
    - Review the generated `Up` and `Down` methods before applying the migration. The generated artifact must contain only confirmed FINNOVA-64 control columns, `receipt_document_retirements`, `receipt_duplicate_copies`, `receipt_control_operations`, their required foreign keys, and unique indexes for `(NumberingScopeKey, DocumentNumber)`, `(ReceiptId, DuplicateCopyNumber)`, and `(ReceiptId, IdempotencyKey)`.
    - Verification checklist: no `CreateTable` for `Receipt`; no canonical journal, reversal-journal, JVE queue, authorization, or challan replacement table; no destructive drop or rename of FINNOVA-61/63 columns; append-only audit columns use the confirmed audit table; `Down` removes only FINNOVA-64 objects; generated SQL targets the approved provider; the migration is reviewed through the repository change process.
    - Do not run the command or claim a generated migration file exists during this specification workflow.

- [ ] 6. Add repository contracts, implementations, and DI
  - [ ] 6.1 CREATE `E:\Finnova\Finnova-API\Finnova.Repository\Interfaces\IReceiptControlRepository.cs`
    - Create this complete interface. Bind the `Receipt` property names to the final FINNOVA-61 aggregate before applying:
      ```csharp
      using Finnova.Models.Contracts.ReceiptControls;
      using Finnova.Models.Domain.Entities;
      using Finnova.Models.Domain.Enums;

      namespace Finnova.Repository.Interfaces;

      public interface IReceiptControlRepository
      {
          Task<Receipt?> GetTrackedAsync(Guid receiptId, CancellationToken cancellationToken);
          Task<ReceiptControlOperation?> GetOperationAsync(Guid idempotencyKey, CancellationToken cancellationToken);
          Task<ReceiptDocumentRetirement?> GetRetirementAsync(string numberingScopeKey, string documentNumber, CancellationToken cancellationToken);
          Task<ReceiptDuplicateCopy?> GetDuplicateByKeyAsync(Guid receiptId, Guid idempotencyKey, CancellationToken cancellationToken);
          Task<int> CompareAndSetReceiptAsync(Receipt receipt, string expectedReceiptVersion, CancellationToken cancellationToken);
          Task<int> CompareAndSetDuplicateCountAsync(Receipt receipt, string expectedReceiptVersion, CancellationToken cancellationToken);
          Task AddRetirementAsync(ReceiptDocumentRetirement record, CancellationToken cancellationToken);
          Task AddDuplicateAsync(ReceiptDuplicateCopy record, CancellationToken cancellationToken);
          Task AddOperationAsync(ReceiptControlOperation operation, CancellationToken cancellationToken);
          Task<IReadOnlyList<ReceiptAuditEntry>> GetAuditAsync(Guid receiptId, CancellationToken cancellationToken);
          Task AddAuditAsync(ReceiptAuditEntry entry, CancellationToken cancellationToken);
      }
      ```

  - [ ] 6.2 CREATE `E:\Finnova\Finnova-API\Finnova.Repository\Repositories\ReceiptControlRepository.cs`
    - **GATE:** The code below targets the expected FINNOVA-61 contract: `Receipt.Id`, `Receipt.Lines`, and an opaque SQL row-version property `Receipt.RowVersion` of type `byte[]`. If FINNOVA-61 uses different names, change only the marked property mappings after the gate confirms the final names. Do not create another Receipt type or table.
    - Create this complete implementation. It stages a row-version compare-and-set for the surrounding unit-of-work transaction and never calls `SaveChangesAsync` from a repository method:
      ```csharp
      using Finnova.Models.Domain.Entities;
      using Finnova.Repository.Context;
      using Finnova.Repository.Interfaces;
      using Microsoft.EntityFrameworkCore;

      namespace Finnova.Repository.Repositories;

      public sealed class ReceiptControlRepository(FinnovaDbContext db) : IReceiptControlRepository
      {
          public Task<Receipt?> GetTrackedAsync(Guid receiptId, CancellationToken cancellationToken) =>
              db.Receipts
                  .AsSplitQuery()
                  .Include(x => x.Lines)
                  .SingleOrDefaultAsync(x => x.Id == receiptId, cancellationToken);

          public Task<ReceiptControlOperation?> GetOperationAsync(Guid idempotencyKey, CancellationToken cancellationToken) =>
              db.ReceiptControlOperations
                  .AsNoTracking()
                  .SingleOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);

          public Task<ReceiptDocumentRetirement?> GetRetirementAsync(string numberingScopeKey, string documentNumber, CancellationToken cancellationToken) =>
              db.ReceiptDocumentRetirements
                  .AsNoTracking()
                  .SingleOrDefaultAsync(x => x.NumberingScopeKey == numberingScopeKey && x.DocumentNumber == documentNumber, cancellationToken);

          public Task<ReceiptDuplicateCopy?> GetDuplicateByKeyAsync(Guid receiptId, Guid idempotencyKey, CancellationToken cancellationToken) =>
              db.ReceiptDuplicateCopies
                  .AsNoTracking()
                  .SingleOrDefaultAsync(x => x.ReceiptId == receiptId && x.IdempotencyKey == idempotencyKey, cancellationToken);

          public Task<int> CompareAndSetReceiptAsync(Receipt receipt, string expectedReceiptVersion, CancellationToken cancellationToken)
              => StageRowVersionCompareAndSetAsync(receipt, expectedReceiptVersion, cancellationToken);

          public Task<int> CompareAndSetDuplicateCountAsync(Receipt receipt, string expectedReceiptVersion, CancellationToken cancellationToken)
              => StageRowVersionCompareAndSetAsync(receipt, expectedReceiptVersion, cancellationToken);

          private Task<int> StageRowVersionCompareAndSetAsync(Receipt receipt, string expectedReceiptVersion, CancellationToken cancellationToken)
          {
              cancellationToken.ThrowIfCancellationRequested();
              if (!Convert.TryFromBase64String(expectedReceiptVersion, new byte[expectedReceiptVersion.Length], out _))
                  throw new ArgumentException("Receipt Version must be a base64 row-version value.", nameof(expectedReceiptVersion));

              var originalVersion = Convert.FromBase64String(expectedReceiptVersion);
              var entry = db.Entry(receipt);
              if (entry.State == EntityState.Detached)
                  db.Receipts.Attach(receipt);

              // The expected FINNOVA-61 row-version is the only concurrency authority. EF adds
              // the original-value predicate when the unit of work calls SaveChangesAsync.
              entry.Property(x => x.RowVersion).OriginalValue = originalVersion;
              entry.Property(x => x.RowVersion).IsModified = false;
              return Task.FromResult(1);
          }

          public Task AddRetirementAsync(ReceiptDocumentRetirement record, CancellationToken cancellationToken) =>
              db.ReceiptDocumentRetirements.AddAsync(record, cancellationToken).AsTask();

          public Task AddDuplicateAsync(ReceiptDuplicateCopy record, CancellationToken cancellationToken) =>
              db.ReceiptDuplicateCopies.AddAsync(record, cancellationToken).AsTask();

          public Task AddOperationAsync(ReceiptControlOperation operation, CancellationToken cancellationToken) =>
              db.ReceiptControlOperations.AddAsync(operation, cancellationToken).AsTask();

          public async Task<IReadOnlyList<ReceiptAuditEntry>> GetAuditAsync(Guid receiptId, CancellationToken cancellationToken)
          {
              var rows = await db.ReceiptAuditEntries
                  .AsNoTracking()
                  .Where(x => x.ReceiptId == receiptId)
                  .OrderByDescending(x => x.OccurredAtUtc)
                  .ThenByDescending(x => x.Id)
                  .ToListAsync(cancellationToken);
              return rows;
          }

          public Task AddAuditAsync(ReceiptAuditEntry entry, CancellationToken cancellationToken) =>
              db.ReceiptAuditEntries.AddAsync(entry, cancellationToken).AsTask();
      }
      ```
    - The `Convert.TryFromBase64String` buffer must be replaced with a length-safe helper if the final row-version serializer uses a different opaque encoding. Preserve the transaction-owned `SaveChangesAsync`, EF concurrency exception, and rollback behavior; do not replace the row-version predicate with an unconditional update.

  - [ ] 6.3 MODIFY `E:\Finnova\Finnova-API\Finnova.Repository\DependencyInjection.cs`
    - Find the existing registration anchor:
      ```csharp
        services.AddScoped<IDemandRepository, DemandRepository>();
        return services;
      ```
    - Replace it with:
      ```csharp
        services.AddScoped<IDemandRepository, DemandRepository>();
        services.AddScoped<IReceiptControlRepository, ReceiptControlRepository>();
        return services;
      ```
    - Add the `using` directives only if the file does not already resolve the interface and repository namespaces.

  - [ ] 6.4 MODIFY `E:\Finnova\Finnova-API\Finnova.Service\DependencyInjection.cs`
    - Find the existing registration anchor:
      ```csharp
        services.AddScoped<IJournalPort, ConfiguredJournalPort>();
        services.AddScoped<IDemandProcessor, DemandProcessor>();
      ```
    - Insert immediately before it:
      ```csharp
        services.AddScoped<IReceiptChallanPort, UnconfiguredReceiptChallanPort>();
        services.AddScoped<IReceiptAccountingBoundary, UnconfiguredReceiptAccountingBoundary>();
        services.AddScoped<IReceiptPrintPort, UnconfiguredReceiptPrintPort>();
        services.AddScoped<IReceiptControlService, ReceiptControlService>();
      ```
    - Replace the three unconfigured adapters only after FINNOVA-61/63 gates pass. Do not register permissive defaults.

- [ ] 7. Implement CQRS control operations and mapping
  - [ ] 7.1 CREATE `E:\Finnova\Finnova-API\Finnova.Service\ReceiptControls\ReceiptControlCommands.cs`
    - Create this complete command contract:
      ```csharp
      using Finnova.Models.Contracts.ReceiptControls;
      using MediatR;

      namespace Finnova.Service.ReceiptControls;

      public sealed record GetReceiptControlSummaryQuery(Guid ReceiptId, string ActorId) : IRequest<ReceiptControlSummaryResponse>;
      public sealed record GetReceiptAuditQuery(Guid ReceiptId, string ActorId) : IRequest<IReadOnlyList<ReceiptAuditEntryResponse>>;
      public sealed record ModifyReceiptControlCommand(Guid ReceiptId, ModifyReceiptControlRequest Request, string ActorId, string CorrelationId) : IRequest<ReceiptControlSummaryResponse>;
      public sealed record CancelReceiptCommand(Guid ReceiptId, CancelReceiptRequest Request, string ActorId, string CorrelationId) : IRequest<ReceiptControlSummaryResponse>;
      public sealed record ReprintReceiptCommand(Guid ReceiptId, Guid IdempotencyKey, string ActorId, string CorrelationId) : IRequest<DuplicateReceiptOutputResponse>;
      ```

  - [ ] 7.2 CREATE `E:\Finnova\Finnova-API\Finnova.Service\ReceiptControls\ReceiptControlValidators.cs`
    - Create this complete validator file:
      ```csharp
      using FluentValidation;
      using Finnova.Models.Contracts.ReceiptControls;

      namespace Finnova.Service.ReceiptControls;

      public sealed class ModifyReceiptControlRequestValidator : AbstractValidator<ModifyReceiptControlRequest>
      {
          public ModifyReceiptControlRequestValidator()
          {
              RuleFor(x => x.ExpectedReceiptVersion).NotEmpty().MaximumLength(200);
              RuleFor(x => x.IdempotencyKey).NotEmpty();
              RuleFor(x => x.MutableFields).NotNull().Must(x => x.Count > 0).WithMessage("At least one mutable field is required.");
          }
      }

      public sealed class CancelReceiptRequestValidator : AbstractValidator<CancelReceiptRequest>
      {
          public CancelReceiptRequestValidator()
          {
              RuleFor(x => x.ExpectedReceiptVersion).NotEmpty().MaximumLength(200);
              RuleFor(x => x.IdempotencyKey).NotEmpty();
              RuleFor(x => x.CancellationReason).NotEmpty().Must(x => !string.IsNullOrWhiteSpace(x)).MaximumLength(500);
          }
      }
      ```

  - [ ] 7.3 CREATE `E:\Finnova\Finnova-API\Finnova.Service\ReceiptControls\ReceiptControlService.cs`
    - **GATE:** This implementation targets the expected final contract documented in `design.md`: `Receipt.DocumentNumber`, `NumberingScopeKey`, `NumberingMode`, `ReceiptType`, `CollectionBranchId`, `CollectionBranchCode`, `ReceiptStatus`, `AuthorizationStatus`, `RowVersion`, `CancellationReason`, `CancelledBy`, `CancelledAtUtc`, `AccrualOutcome`, `ReversalRequestId`, `ReversalSourceLink`, `DuplicationCount`, `LastDuplicatePrintedAtUtc`, and `UpdatedAtUtc`. Replace only these property mappings and the `ApprovedMutableFields` set after FINNOVA-61/63 confirmation. Do not add a local Receipt, journal, authorization, or challan model.
    - Create this complete application service. It also implements the MediatR handlers declared in Task 7.1 so the existing controller wiring has no orphaned commands:
      ```csharp
      using System.Globalization;
      using System.Reflection;
      using System.Text.Json;
      using Finnova.Models.Contracts.ReceiptControls;
      using Finnova.Models.Domain.Entities;
      using Finnova.Models.Domain.Exceptions;
      using ReceiptControlOperationKind = Finnova.Models.Domain.Enums.ReceiptControlOperation;
      using Finnova.Repository.Interfaces;
      using Finnova.Service.ReceiptControls.Domain;
      using Finnova.Service.ReceiptControls.Ports;
      using MediatR;
      using Microsoft.EntityFrameworkCore;

      namespace Finnova.Service.ReceiptControls;

      public sealed class ReceiptControlService(
          IReceiptControlRepository repository,
          IApplicationUnitOfWork unitOfWork,
          IReceiptChallanPort challanPort,
          IReceiptAccountingBoundary accountingBoundary,
          IReceiptPrintPort printPort,
          IReceiptScopePort scopePort)
          : IReceiptControlService,
            IRequestHandler<GetReceiptControlSummaryQuery, ReceiptControlSummaryResponse>,
            IRequestHandler<GetReceiptAuditQuery, IReadOnlyList<ReceiptAuditEntryResponse>>,
            IRequestHandler<ModifyReceiptControlCommand, ReceiptControlSummaryResponse>,
            IRequestHandler<CancelReceiptCommand, ReceiptControlSummaryResponse>,
            IRequestHandler<ReprintReceiptCommand, DuplicateReceiptOutputResponse>
      {
          private static readonly HashSet<string> ApprovedMutableFields = new(StringComparer.Ordinal)
          {
              // Replace this set with the final FINNOVA-61-approved mutable field contract.
              "Amount", "ValueDate", "ReceiptMode", "CustomerId", "AccountId", "Remarks"
          };

          public Task<ReceiptControlSummaryResponse> Handle(GetReceiptControlSummaryQuery request, CancellationToken cancellationToken) =>
              GetSummaryAsync(request.ReceiptId, request.ActorId, cancellationToken);

          public Task<IReadOnlyList<ReceiptAuditEntryResponse>> Handle(GetReceiptAuditQuery request, CancellationToken cancellationToken) =>
              GetAuditAsync(request.ReceiptId, request.ActorId, cancellationToken);

          public Task<ReceiptControlSummaryResponse> Handle(ModifyReceiptControlCommand request, CancellationToken cancellationToken) =>
              ModifyAsync(request.ReceiptId, request.Request, request.ActorId, request.CorrelationId, cancellationToken);

          public Task<ReceiptControlSummaryResponse> Handle(CancelReceiptCommand request, CancellationToken cancellationToken) =>
              CancelAsync(request.ReceiptId, request.Request, request.ActorId, request.CorrelationId, cancellationToken);

          public Task<DuplicateReceiptOutputResponse> Handle(ReprintReceiptCommand request, CancellationToken cancellationToken) =>
              ReprintAsync(request.ReceiptId, request.IdempotencyKey, request.ActorId, request.CorrelationId, cancellationToken);

          public async Task<ReceiptControlSummaryResponse> GetSummaryAsync(Guid receiptId, string actorId, CancellationToken ct)
          {
              var receipt = await LoadInScopeAsync(receiptId, actorId, ct);
              return await BuildSummaryAsync(receipt, actorId, ct);
          }

          public async Task<ReceiptControlSummaryResponse> ModifyAsync(Guid receiptId, ModifyReceiptControlRequest request, string actorId, string correlationId, CancellationToken ct)
          {
              var receipt = await LoadInScopeAsync(receiptId, actorId, ct);
              var requestHash = ReceiptControlRules.Hash(request);
              var prior = await GetExistingOperationAsync(request.IdempotencyKey, requestHash, receiptId, ct);
              if (prior is not null) return JsonSerializer.Deserialize<ReceiptControlSummaryResponse>(prior.ResponseSnapshotJson)!;
              await EnsureOperationAllowedAsync(receipt, actorId, ReceiptControlOperationKind.Modify, ct);

              await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
              try
              {
                  receipt = await ReloadAndRecheckAsync(receiptId, actorId, ReceiptControlOperationKind.Modify, ct);
                  var before = SnapshotMutableValues(receipt, request.MutableFields.Keys);
                  ApplyMutableValues(receipt, request.MutableFields);
                  receipt.UpdatedAtUtc = DateTime.UtcNow;
                  await repository.CompareAndSetReceiptAsync(receipt, request.ExpectedReceiptVersion, ct);
                  await repository.AddAuditAsync(CreateAudit(receipt, "Modify", actorId, correlationId, before, SnapshotMutableValues(receipt, request.MutableFields.Keys), null, null, null), ct);
                  var response = await BuildSummaryAsync(receipt, actorId, ct);
                  await repository.AddOperationAsync(new ReceiptControlOperation
                  {
                      IdempotencyKey = request.IdempotencyKey,
                      ReceiptId = receiptId,
                      Operation = Finnova.Models.Domain.Entities.ReceiptControlOperation.Modify,
                      RequestHash = requestHash,
                      ResponseSnapshotJson = JsonSerializer.Serialize(response),
                      CompletedAtUtc = DateTime.UtcNow
                  }, ct);
                  await unitOfWork.SaveChangesAsync(ct);
                  await transaction.CommitAsync(ct);
                  return response;
              }
              catch (DbUpdateConcurrencyException)
              {
                  await transaction.RollbackAsync(ct);
                  throw new ReceiptControlConflictException("The Receipt Version is stale. Refresh and retry.", ReceiptControlRuleKeys.ReceiptVersionConflict);
              }
              catch
              {
                  await transaction.RollbackAsync(ct);
                  throw;
              }
          }

          public async Task<ReceiptControlSummaryResponse> CancelAsync(Guid receiptId, CancelReceiptRequest request, string actorId, string correlationId, CancellationToken ct)
          {
              var normalizedReason = ReceiptControlRules.NormalizeCancellationReason(request.CancellationReason);
              var receipt = await LoadInScopeAsync(receiptId, actorId, ct);
              var requestHash = ReceiptControlRules.Hash(request with { CancellationReason = normalizedReason });
              var prior = await GetExistingOperationAsync(request.IdempotencyKey, requestHash, receiptId, ct);
              if (prior is not null) return JsonSerializer.Deserialize<ReceiptControlSummaryResponse>(prior.ResponseSnapshotJson)!;
              await EnsureOperationAllowedAsync(receipt, actorId, ReceiptControlOperationKind.Cancel, ct);

              await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
              try
              {
                  receipt = await ReloadAndRecheckAsync(receiptId, actorId, ReceiptControlOperationKind.Cancel, ct);
                  var accrual = await accountingBoundary.GetAccrualAsync(receiptId, ct);
                  var cancellation = ReceiptControlRules.DecideCancellation(accrual.HasAccrual, normalizedReason);
                  Guid? reversalId = null;
                  string? reversalLink = null;
                  if (cancellation.RequiresReversal)
                  {
                      if (accrual.Amount is null || string.IsNullOrWhiteSpace(accrual.CurrencyCode) || accrual.ValueDate is null || string.IsNullOrWhiteSpace(accrual.SourceLink))
                          throw new ReceiptControlDependencyException("The accounting boundary did not provide the required accrual facts.");
                      var accepted = await accountingBoundary.AcceptReversalAsync(new ReceiptReversalRequest(
                          request.IdempotencyKey, receipt.Id, receipt.DocumentNumber, accrual.SourceLink, accrual.Amount.Value,
                          accrual.CurrencyCode, accrual.ValueDate.Value, receipt.CollectionBranchId, actorId, DateTime.UtcNow, normalizedReason), ct);
                      if (!accepted.Accepted || accepted.ReversalRequestId is null)
                          throw new ReceiptControlDependencyException(accepted.EnglishMessage ?? "The accounting boundary did not accept the required reversal.");
                      reversalId = accepted.ReversalRequestId;
                      reversalLink = accepted.SourceLink;
                  }

                  var priorStatus = StatusText(receipt.ReceiptStatus);
                  receipt.ReceiptStatus = "Cancelled";
                  receipt.CancellationReason = normalizedReason;
                  receipt.CancelledBy = actorId;
                  receipt.CancelledAtUtc = DateTime.UtcNow;
                  receipt.AccrualOutcome = cancellation.RequiresReversal ? "ReversalRequired" : "NoReversalRequired";
                  receipt.ReversalRequestId = reversalId;
                  receipt.ReversalSourceLink = reversalLink;
                  receipt.UpdatedAtUtc = DateTime.UtcNow;
                  await repository.CompareAndSetReceiptAsync(receipt, request.ExpectedReceiptVersion, ct);
                  await repository.AddRetirementAsync(new ReceiptDocumentRetirement
                  {
                      ReceiptId = receipt.Id, DocumentNumber = receipt.DocumentNumber, NumberingMode = receipt.NumberingMode,
                      NumberingScopeKey = receipt.NumberingScopeKey, ReceiptType = receipt.ReceiptType, RetiredBy = actorId,
                      RetiredAtUtc = DateTime.UtcNow, CancellationReason = normalizedReason
                  }, ct);
                  await repository.AddAuditAsync(CreateAudit(receipt, "Cancel", actorId, correlationId, null, null, priorStatus, normalizedReason, reversalId), ct);
                  var response = await BuildSummaryAsync(receipt, actorId, ct);
                  await repository.AddOperationAsync(new ReceiptControlOperation
                  {
                      IdempotencyKey = request.IdempotencyKey, ReceiptId = receiptId,
                      Operation = Finnova.Models.Domain.Entities.ReceiptControlOperation.Cancel,
                      RequestHash = requestHash, ResponseSnapshotJson = JsonSerializer.Serialize(response), CompletedAtUtc = DateTime.UtcNow
                  }, ct);
                  await unitOfWork.SaveChangesAsync(ct);
                  await transaction.CommitAsync(ct);
                  return response;
              }
              catch (DbUpdateConcurrencyException)
              {
                  await transaction.RollbackAsync(ct);
                  throw new ReceiptControlConflictException("The Receipt Version is stale. Refresh and retry.", ReceiptControlRuleKeys.ReceiptVersionConflict);
              }
              catch
              {
                  await transaction.RollbackAsync(ct);
                  throw;
              }
          }

          public async Task<DuplicateReceiptOutputResponse> ReprintAsync(Guid receiptId, Guid idempotencyKey, string actorId, string correlationId, CancellationToken ct)
          {
              var receipt = await LoadInScopeAsync(receiptId, actorId, ct);
              var eligibility = await printPort.GetEligibilityAsync(receiptId, actorId, ct);
              if (!eligibility.IsPrinted || !eligibility.IsEligible || IsCancelled(receipt))
                  throw new ReceiptControlConflictException(eligibility.FailureReason ?? "The Receipt is not eligible for duplicate printing.", ReceiptControlRuleKeys.PrintNotEligible);
              var requestHash = ReceiptControlRules.Hash(new { receiptId, idempotencyKey });
              var prior = await GetExistingOperationAsync(idempotencyKey, requestHash, receiptId, ct);
              if (prior is not null) return JsonSerializer.Deserialize<DuplicateReceiptOutputResponse>(prior.ResponseSnapshotJson)!;

              await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
              try
              {
                  receipt = await ReloadAndRecheckAsync(receiptId, actorId, ReceiptControlOperationKind.Reprint, ct);
                  var marker = ReceiptControlRules.AllocateDuplicateMarker(receipt.DuplicationCount);
                  receipt.DuplicationCount = marker.DuplicationCount;
                  receipt.LastDuplicatePrintedAtUtc = DateTime.UtcNow;
                  receipt.UpdatedAtUtc = DateTime.UtcNow;
                  await repository.CompareAndSetDuplicateCountAsync(receipt, ToVersion(receipt), ct);
                  var outputReference = await printPort.CreateDuplicateOutputReferenceAsync(receiptId, marker.DuplicateCopyNumber, ct);
                  var copy = new ReceiptDuplicateCopy
                  {
                      ReceiptId = receipt.Id, DocumentNumber = receipt.DocumentNumber, DuplicateCopyNumber = marker.DuplicateCopyNumber,
                      DuplicationCountAfter = marker.DuplicationCount, OutputTitle = marker.Title, WatermarkText = marker.Watermark,
                      OutputReference = outputReference, PrintedBy = actorId, PrintedAtUtc = DateTime.UtcNow, IdempotencyKey = idempotencyKey
                  };
                  await repository.AddDuplicateAsync(copy, ct);
                  await repository.AddAuditAsync(CreateAudit(receipt, "Reprint", actorId, correlationId, null, null, null, null, null, marker.DuplicateCopyNumber), ct);
                  var response = ReceiptControlMapper.Duplicate(copy);
                  await repository.AddOperationAsync(new ReceiptControlOperation
                  {
                      IdempotencyKey = idempotencyKey, ReceiptId = receiptId,
                      Operation = Finnova.Models.Domain.Entities.ReceiptControlOperation.Reprint,
                      RequestHash = requestHash, ResponseSnapshotJson = JsonSerializer.Serialize(response), CompletedAtUtc = DateTime.UtcNow
                  }, ct);
                  await unitOfWork.SaveChangesAsync(ct);
                  await transaction.CommitAsync(ct);
                  return response;
              }
              catch (DbUpdateConcurrencyException)
              {
                  await transaction.RollbackAsync(ct);
                  throw new ReceiptControlConflictException("The Receipt was changed by another request. Refresh and retry.", ReceiptControlRuleKeys.ReceiptVersionConflict);
              }
              catch
              {
                  await transaction.RollbackAsync(ct);
                  throw;
              }
          }

          public async Task<IReadOnlyList<ReceiptAuditEntryResponse>> GetAuditAsync(Guid receiptId, string actorId, CancellationToken ct)
          {
              await LoadInScopeAsync(receiptId, actorId, ct);
              var entries = await repository.GetAuditAsync(receiptId, ct);
              return entries.Select(ReceiptControlMapper.Audit).ToArray();
          }

          private async Task<Receipt> LoadInScopeAsync(Guid receiptId, string actorId, CancellationToken ct)
          {
              if (!Guid.TryParse(actorId, out var actorGuid))
                  throw new ReceiptControlForbiddenException("The authenticated actor identifier is invalid.");
              var receipt = await repository.GetTrackedAsync(receiptId, ct);
              if (receipt is null) throw new ReceiptControlNotFoundException("Receipt was not found.");
              var scope = await scopePort.ResolveAsync(actorGuid, receiptId, ct);
              if (!scope.CanRead) throw new ReceiptControlForbiddenException("The Receipt is outside your authorization scope.");
              return receipt;
          }

          private async Task<Receipt> ReloadAndRecheckAsync(Guid receiptId, string actorId, ReceiptControlOperationKind operation, CancellationToken ct)
          {
              var receipt = await LoadInScopeAsync(receiptId, actorId, ct);
              await EnsureOperationAllowedAsync(receipt, actorId, operation, ct);
              return receipt;
          }

          private async Task EnsureOperationAllowedAsync(Receipt receipt, string actorId, ReceiptControlOperationKind operation, CancellationToken ct)
          {
              var scope = await scopePort.ResolveAsync(Guid.Parse(actorId), receipt.Id, ct);
              if (operation == ReceiptControlOperationKind.Modify && !scope.CanModify || operation == ReceiptControlOperationKind.Cancel && !scope.CanCancel || operation == ReceiptControlOperationKind.Reprint && !scope.CanReprint)
                  throw new ReceiptControlForbiddenException("The authenticated user is not permitted to perform this Receipt operation.");
              if (operation == ReceiptControlOperationKind.Reprint) return;
              var challan = await challanPort.GetAsync(receipt.Id, ct);
              if (challan.IsChallaned)
                  throw new ReceiptControlConflictException("The Receipt is linked to a challan and cannot be modified or cancelled.", ReceiptControlRuleKeys.ChallanProtected);
              if (IsAuthorized(receipt))
                  throw new ReceiptControlConflictException("The Receipt is authorized. Use the approved Cheque Return or JV reversal process.", ReceiptControlRuleKeys.AuthorizedReceiptProtected);
              if (IsCancelled(receipt) || !IsUnauthorized(receipt))
                  throw new ReceiptControlConflictException("The Receipt is not in an eligible unauthorized state.", ReceiptControlRuleKeys.InvalidReceiptState);
          }

          private async Task<ReceiptControlOperation?> GetExistingOperationAsync(Guid key, string requestHash, Guid receiptId, CancellationToken ct)
          {
              var existing = await repository.GetOperationAsync(key, ct);
              if (existing is null) return null;
              if (existing.ReceiptId != receiptId || !StringComparer.Ordinal.Equals(existing.RequestHash, requestHash))
                  throw new ReceiptControlConflictException("The Idempotency Key was already used for a different request.", ReceiptControlRuleKeys.IdempotencyConflict);
              return existing;
          }

          private async Task<ReceiptControlSummaryResponse> BuildSummaryAsync(Receipt receipt, string actorId, CancellationToken ct)
          {
              var scope = await scopePort.ResolveAsync(Guid.Parse(actorId), receipt.Id, ct);
              var challan = await challanPort.GetAsync(receipt.Id, ct);
              var print = await printPort.GetEligibilityAsync(receipt.Id, actorId, ct);
              var canModify = scope.CanModify && IsUnauthorized(receipt) && !challan.IsChallaned && !IsCancelled(receipt);
              var canCancel = scope.CanCancel && IsUnauthorized(receipt) && !challan.IsChallaned && !IsCancelled(receipt);
              var canReprint = scope.CanReprint && print.IsPrinted && print.IsEligible && !IsCancelled(receipt);
              var actions = new List<string>();
              if (canModify) actions.Add("Modify");
              if (canCancel) actions.Add("Cancel");
              if (canReprint) actions.Add("Reprint");
              return new ReceiptControlSummaryResponse(receipt.Id, receipt.DocumentNumber, StatusText(receipt.ReceiptStatus), StatusText(receipt.AuthorizationStatus),
                  challan.IsChallaned, challan.ChallanReference, print.IsPrinted, receipt.DuplicationCount, ToVersion(receipt), receipt.CollectionBranchId,
                  receipt.CollectionBranchCode, actions, canModify, canCancel, canReprint, string.Equals(receipt.AccrualOutcome, "ReversalRequired", StringComparison.Ordinal),
                  IsAuthorized(receipt) ? "Use the approved Cheque Return or JV reversal process." : null);
          }

          private static ReceiptAuditEntry CreateAudit(Receipt receipt, string action, string actorId, string correlationId, object? before, object? after, string? priorStatus, string? reason, Guid? reversalId, int? copyNumber = null) =>
              new()
              {
                  ReceiptId = receipt.Id, Action = action, DocNo = receipt.DocumentNumber, Actor = actorId,
                  PriorStatus = priorStatus, NewStatus = action == "Cancel" ? "Cancelled" : null,
                  CancellationReason = reason, BeforeValuesJson = before is null ? null : JsonSerializer.Serialize(before),
                  AfterValuesJson = after is null ? null : JsonSerializer.Serialize(after), RetirementRecordId = null,
                  ReversalRequestId = reversalId, DuplicateCopyNumber = copyNumber, CorrelationId = correlationId, OccurredAtUtc = DateTime.UtcNow
              };

          private static Dictionary<string, object?> SnapshotMutableValues(Receipt receipt, IEnumerable<string> names) => names.ToDictionary(name => name, name => receipt.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public)?.GetValue(receipt));

          private static void ApplyMutableValues(Receipt receipt, IReadOnlyDictionary<string, string?> fields)
          {
              foreach (var pair in fields)
              {
                  if (!ApprovedMutableFields.Contains(pair.Key))
                      throw new ReceiptControlValidationException($"Field '{pair.Key}' is not an approved mutable Receipt field.");
                  var property = receipt.GetType().GetProperty(pair.Key, BindingFlags.Instance | BindingFlags.Public);
                  if (property is null || !property.CanWrite)
                      throw new ReceiptControlValidationException($"Field '{pair.Key}' is not available in the confirmed FINNOVA-61 Receipt contract.");
                  try
                  {
                      var target = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                      var value = pair.Value is null ? null : Convert.ChangeType(pair.Value, target, CultureInfo.InvariantCulture);
                      property.SetValue(receipt, value);
                  }
                  catch (Exception ex) when (ex is FormatException or InvalidCastException or ArgumentException)
                  {
                      throw new ReceiptControlValidationException($"Field '{pair.Key}' has an invalid value.");
                  }
              }
          }

          private static bool IsUnauthorized(Receipt receipt) =>
              StatusText(receipt.ReceiptStatus).Equals("Pending Authorization", StringComparison.OrdinalIgnoreCase) ||
              StatusText(receipt.ReceiptStatus).Equals("Unauthorized", StringComparison.OrdinalIgnoreCase);

          private static bool IsAuthorized(Receipt receipt) => StatusText(receipt.AuthorizationStatus).Equals("Authorized", StringComparison.OrdinalIgnoreCase) || StatusText(receipt.ReceiptStatus).Equals("Authorized", StringComparison.OrdinalIgnoreCase);
          private static bool IsCancelled(Receipt receipt) => StatusText(receipt.ReceiptStatus).Equals("Cancelled", StringComparison.OrdinalIgnoreCase);
          private static string StatusText(object? value) => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
          private static string ToVersion(Receipt receipt) => Convert.ToBase64String(receipt.RowVersion);
      }
      ```
    - Exact mapping instructions: replace the six marked Receipt status/field access groups with the final FINNOVA-61 names; replace `Action`, `DocNo`, and `CorrelationId` with the final FINNOVA-61 audit names if needed; replace `BeginTransactionAsync` return usage only if the approved unit-of-work exposes a different transaction type. Preserve the order of scope check, authoritative fact checks, idempotency hash, CAS, audit/tombstone/output persistence, save, and commit. The service must fail closed when an adapter is unconfigured and must never write canonical journal tables.
    - **Requirements:** 1.1-10.18 and 11.1-11.25; **Properties:** 1-4.

  - [ ] 7.4 CREATE `E:\Finnova\Finnova-API\Finnova.Service\ReceiptControls\ReceiptControlMapper.cs`
    - Create this complete mapper contract and preserve safe fields only:
      ```csharp
      using Finnova.Models.Contracts.ReceiptControls;
      using Finnova.Models.Domain.Entities;

      namespace Finnova.Service.ReceiptControls;

      public static class ReceiptControlMapper
      {
          public static DuplicateReceiptOutputResponse Duplicate(ReceiptDuplicateCopy copy) =>
              new(copy.ReceiptId, copy.DocumentNumber, copy.OutputTitle, copy.WatermarkText,
                  copy.DuplicateCopyNumber, copy.DuplicationCountAfter, copy.PrintedAtUtc, copy.OutputReference);

          public static ReceiptAuditEntryResponse Audit(ReceiptAuditEntry entry) =>
              new(entry.Id, entry.ReceiptId, entry.Action, entry.DocNo, entry.Actor, entry.PriorStatus,
                  entry.NewStatus, entry.CancellationReason, entry.BeforeValuesJson, entry.AfterValuesJson,
                  entry.RetirementRecordId, entry.ReversalRequestId, entry.DuplicateCopyNumber, entry.OccurredAtUtc);
      }
      ```
    - If the final FINNOVA-61 audit field is named `Operation` rather than `Action`, bind the exact property name at the gate. Do not expose raw accounting payloads, credentials, or full instrument details.

- [ ] 8. Wire policies, controller, middleware, and gateway
  - [ ] 8.1 MODIFY `E:\Finnova\Finnova-API\Finnova.AccountsService\Program.cs`
    - Find the existing authorization policy block ending with:
      ```csharp
      options.AddPolicy("AccountsOps", p => p.RequireAssertion(ctx =>
          ctx.User.IsInRole("AccountsOperator") || ctx.User.IsInRole("SystemAdmin")));
      ```
    - Insert immediately after it, after confirming the final role names:
      ```csharp
          options.AddPolicy("ReceiptControlRead", p => p.RequireAuthenticatedUser().RequireAssertion(ctx =>
              ctx.User.IsInRole("BranchSupervisor") || ctx.User.IsInRole("BranchManager") || ctx.User.IsInRole("SystemAdmin")));
          options.AddPolicy("ReceiptControlModify", p => p.RequireAuthenticatedUser().RequireAssertion(ctx =>
              ctx.User.IsInRole("BranchSupervisor") || ctx.User.IsInRole("BranchManager") || ctx.User.IsInRole("SystemAdmin")));
          options.AddPolicy("ReceiptControlCancel", p => p.RequireAuthenticatedUser().RequireAssertion(ctx =>
              ctx.User.IsInRole("BranchSupervisor") || ctx.User.IsInRole("BranchManager") || ctx.User.IsInRole("SystemAdmin")));
          options.AddPolicy("ReceiptControlReprint", p => p.RequireAuthenticatedUser().RequireAssertion(ctx =>
              ctx.User.IsInRole("BranchSupervisor") || ctx.User.IsInRole("BranchManager") || ctx.User.IsInRole("SystemAdmin")));
      ```
    - Replace role names only after the FINNOVA-63 role gate. Do not grant browser users any internal accounting callback policy.

  - [ ] 8.2 CREATE `E:\Finnova\Finnova-API\Finnova.AccountsService\Controllers\ReceiptControlsController.cs`
    - Create this complete route shell. Controller actions must pass actor claims and correlation ID, never actor fields from JSON:
      ```csharp
      using Finnova.Models.Contracts.ReceiptControls;
      using Finnova.Service.ReceiptControls;
      using MediatR;
      using Microsoft.AspNetCore.Authorization;
      using Microsoft.AspNetCore.Mvc;
      using System.Security.Claims;

      namespace Finnova.AccountsService.Controllers;

      [ApiController]
      [Route("api/receipts")]
      public sealed class ReceiptControlsController(IMediator mediator) : ControllerBase
      {
          private string ActorId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();
          private string CorrelationId => HttpContext.TraceIdentifier;

          [HttpGet("{receiptId:guid}/control-summary"), Authorize(Policy = "ReceiptControlRead")]
          public Task<ReceiptControlSummaryResponse> Summary(Guid receiptId, CancellationToken ct) => mediator.Send(new GetReceiptControlSummaryQuery(receiptId, ActorId), ct);

          [HttpGet("{receiptId:guid}/audit"), Authorize(Policy = "ReceiptControlRead")]
          public Task<IReadOnlyList<ReceiptAuditEntryResponse>> Audit(Guid receiptId, CancellationToken ct) => mediator.Send(new GetReceiptAuditQuery(receiptId, ActorId), ct);

          [HttpPatch("{receiptId:guid}/control"), Authorize(Policy = "ReceiptControlModify")]
          public Task<ReceiptControlSummaryResponse> Modify(Guid receiptId, [FromBody] ModifyReceiptControlRequest request, CancellationToken ct) => mediator.Send(new ModifyReceiptControlCommand(receiptId, request, ActorId, CorrelationId), ct);

          [HttpPost("{receiptId:guid}/cancel"), Authorize(Policy = "ReceiptControlCancel")]
          public Task<ReceiptControlSummaryResponse> Cancel(Guid receiptId, [FromBody] CancelReceiptRequest request, CancellationToken ct) => mediator.Send(new CancelReceiptCommand(receiptId, request, ActorId, CorrelationId), ct);

          [HttpPost("{receiptId:guid}/reprint"), Authorize(Policy = "ReceiptControlReprint")]
          public Task<DuplicateReceiptOutputResponse> Reprint(Guid receiptId, [FromHeader(Name = "Idempotency-Key")] Guid idempotencyKey, CancellationToken ct) => mediator.Send(new ReprintReceiptCommand(receiptId, idempotencyKey, ActorId, CorrelationId), ct);
      }
      ```

  - [ ] 8.3 MODIFY `E:\Finnova\Finnova-API\Finnova.AccountsService\Middlewares\ExceptionHandlingMiddleware.cs`
    - Find the existing switch arm:
      ```csharp
                  ApplicationProcessingException a => (a.ErrorCode switch
      ```
    - Insert this typed branch immediately before it:
      ```csharp
                  ReceiptControlException r => (r.StatusCode, r.ErrorCode, r.Message),
      ```
    - Find the existing response extension block:
      ```csharp
              problem.Extensions["correlationId"] = ctx.TraceIdentifier;
      ```
    - Insert immediately after it:
      ```csharp
              if (ex is ReceiptControlException receiptControl && receiptControl.Rule is not null)
                  problem.Extensions["rule"] = receiptControl.Rule;
      ```
    - Preserve existing GL, account, demand, and fallback behavior. Do not use message sniffing.

  - [ ] 8.4 MODIFY `E:\Finnova\Finnova-API\Finnova.ApiGateway\appsettings.json`
    - Insert these two route objects inside `ReverseProxy.Routes`, before the general `ua-route` catch-all:
      ```json
      "ua-receipts-alias-route": {
        "ClusterId": "accounts-cluster",
        "Match": { "Path": "/api/ua/api/receipts/{**catch-all}" },
        "Transforms": [
          { "PathRemovePrefix": "/api/ua/api" },
          { "PathPrefix": "/api" }
        ]
      },
      "ua-receipts-alias-root": {
        "ClusterId": "accounts-cluster",
        "Match": { "Path": "/api/ua/api/receipts" },
        "Transforms": [
          { "PathRemovePrefix": "/api/ua/api" },
          { "PathPrefix": "/api" }
        ]
      },
      ```
    - Keep the internal FINNOVA-63 accounting callback outside this UI-facing alias.

- [ ] 9. Add backend tests for every acceptance scenario and boundary
  - [ ] 9.1 CREATE `E:\Finnova\Finnova-API\Finnova.Tests\ReceiptControls\ReceiptControlRulesTests.cs`
    - Create this complete xUnit/FsCheck test file. Each property is configured for at least 100 generated cases and is annotated with its design property:
      ```csharp
      using FsCheck;
      using FsCheck.Xunit;
      using Finnova.Models.Domain.Exceptions;
      using Finnova.Service.ReceiptControls.Domain;
      using Xunit;

      namespace Finnova.Tests.ReceiptControls;

      public sealed class ReceiptControlRulesTests
      {
          [Property(MaxTest = 100)]
          public Property CancellationReasonIsTrimmedAndBounded(NonEmptyString input)
          {
              // Feature: receipt-processing-controls, Property 2: Cancellation retirement is one-way and idempotent
              var source = input.Get.Length > 498 ? input.Get[..498] : input.Get;
              var reason = $" {source} ";
              return (reason.Trim().Length <= 500).ToProperty(() => ReceiptControlRules.NormalizeCancellationReason(reason) == reason.Trim());
          }

          [Property(MaxTest = 100)]
          public Property DuplicateMarkersAreExactAndSequential(NonNegativeInt generatedCount)
          {
              // Feature: receipt-processing-controls, Property 4: Duplicate output numbering and markers remain consistent
              var count = generatedCount.Get % 1000000;
              var marker = ReceiptControlRules.AllocateDuplicateMarker(count);
              return (marker.DuplicateCopyNumber == count + 1 && marker.DuplicationCount == count + 1 && marker.Title == "DUPLICATE RECEIPT" && marker.Watermark == "DUPLICATE RECEIPT").ToProperty();
          }

          [Fact]
          public void WhitespaceCancellationReasonIsRejected()
          {
              // Feature: receipt-processing-controls, Property 2: Cancellation retirement is one-way and idempotent
              Assert.Throws<ReceiptControlValidationException>(() => ReceiptControlRules.NormalizeCancellationReason("   "));
          }

          [Fact]
          public void CancellationDecisionRequiresReversalOnlyForAnAccrual()
          {
              // Feature: receipt-processing-controls, Property 3: Accrual determines one reversal request or no reversal
              var withAccrual = ReceiptControlRules.DecideCancellation(true, "Correction");
              var withoutAccrual = ReceiptControlRules.DecideCancellation(false, "Correction");
              Assert.True(withAccrual.RequiresReversal);
              Assert.False(withoutAccrual.RequiresReversal);
              Assert.Equal("Correction", withoutAccrual.NormalizedReason);
          }

          [Fact]
          public void HashIsStableForEquivalentControlRequests()
          {
              // Feature: receipt-processing-controls, Property 1: Approved modification preserves immutable receipt identity
              var first = ReceiptControlRules.Hash(new { Amount = "1250.00", Remarks = "Correction" });
              var second = ReceiptControlRules.Hash(new { Amount = "1250.00", Remarks = "Correction" });
              Assert.Equal(first, second);
          }
      }
      ```
    - The generated tests cover Properties 1-4 without postponing any test creation; persistence, authorization, accounting, concurrency, and UI behavior remain covered by Tasks 9.2, 9.3, and 12.1-12.3.

  - [ ] 9.2 CREATE `E:\Finnova\Finnova-API\Finnova.Tests\ReceiptControls\ReceiptControlApiTests.cs`
    - **GATE:** Apply after FINNOVA-61/63 binding. The fixture below uses only the expected authoritative `Receipt` aggregate and the ports from Task 4.2. Replace marked final-contract property names if the gate confirms a different name; do not substitute `Document`, `ChallanRule`, or `JvPostingInbox`.
    - Create this complete `WebApplicationFactory<Program>` test file:
      ```csharp
      using System.IdentityModel.Tokens.Jwt;
      using System.Net;
      using System.Net.Http.Headers;
      using System.Security.Claims;
      using System.Text;
      using System.Text.Json;
      using Finnova.Models.Domain.Entities;
      using Finnova.Repository.Context;
      using Finnova.Service.ReceiptControls.Ports;
      using Microsoft.AspNetCore.Hosting;
      using Microsoft.AspNetCore.Mvc.Testing;
      using Microsoft.AspNetCore.TestHost;
      using Microsoft.EntityFrameworkCore;
      using Microsoft.Extensions.DependencyInjection;
      using Microsoft.Extensions.DependencyInjection.Extensions;
      using Microsoft.IdentityModel.Tokens;
      using Xunit;

      namespace Finnova.Tests.ReceiptControls;

      public sealed class ReceiptControlApiTests : IClassFixture<ReceiptControlApiFactory>
      {
          private static readonly Guid PendingId = Guid.Parse("10000000-0000-0000-0000-000000000001");
          private static readonly Guid ChallanedId = Guid.Parse("10000000-0000-0000-0000-000000000002");
          private static readonly Guid AuthorizedId = Guid.Parse("10000000-0000-0000-0000-000000000003");
          private static readonly Guid PrintedId = Guid.Parse("10000000-0000-0000-0000-000000000004");
          private readonly ReceiptControlApiFactory factory;

          public ReceiptControlApiTests(ReceiptControlApiFactory factory) => this.factory = factory;

          [Theory]
          [InlineData(null)]
          [InlineData("not-a-jwt")]
          [InlineData("expired")]
          [InlineData("invalid-signature")]
          public async Task MissingExpiredMalformedAndInvalidSignatureTokensReturn401(string? token)
          {
              factory.Reset();
              using var client = factory.CreateClient();
              var request = new HttpRequestMessage(HttpMethod.Get, $"/api/receipts/{PendingId}/control-summary");
              if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", factory.TokenFor(token));
              var response = await client.SendAsync(request);
              Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
          }

          [Fact]
          public async Task AuthenticatedNonSupervisorReturns403()
          {
              factory.Reset();
              using var client = factory.CreateClient();
              client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.TokenFor("operator", "AccountsOperator"));
              var response = await client.GetAsync($"/api/receipts/{PendingId}/control-summary");
              Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
          }

          [Fact]
          public async Task Rec205ChallanedModifyAndCancelReturnStableProtectionConflict()
          {
              factory.Reset();
              using var client = factory.CreateAuthorizedClient();
              var version = await VersionAsync(client, ChallanedId);
              var modify = await client.PatchAsync($"/api/receipts/{ChallanedId}/control", Json("{\"expectedReceiptVersion\":\"" + version + "\",\"idempotencyKey\":\"20000000-0000-0000-0000-000000000001\",\"mutableFields\":{\"Remarks\":\"x\"}}"));
              var cancel = await client.PostAsync($"/api/receipts/{ChallanedId}/cancel", Json("{\"expectedReceiptVersion\":\"" + version + "\",\"idempotencyKey\":\"20000000-0000-0000-0000-000000000002\",\"cancellationReason\":\"Duplicate entry\"}"));
              await AssertRuleAsync(modify, "CHALLAN_PROTECTED");
              await AssertRuleAsync(cancel, "CHALLAN_PROTECTED");
          }

          [Fact]
          public async Task Rec302CancellationPersistsCancelledStateAuditRetirementAndDocumentNumber()
          {
              factory.Reset();
              using var client = factory.CreateAuthorizedClient();
              var version = await VersionAsync(client, PendingId);
              var response = await client.PostAsync($"/api/receipts/{PendingId}/cancel", Json("{\"expectedReceiptVersion\":\"" + version + "\",\"idempotencyKey\":\"20000000-0000-0000-0000-000000000003\",\"cancellationReason\":\"REC-302 correction\"}"));
              response.EnsureSuccessStatusCode();
              var summary = await JsonAsync(response);
              Assert.Equal("Cancelled", summary.GetProperty("receiptStatus").GetString());
              Assert.Equal("RCT-302", summary.GetProperty("documentNumber").GetString());
              var audit = await client.GetAsync($"/api/receipts/{PendingId}/audit");
              audit.EnsureSuccessStatusCode();
              Assert.Single(await JsonArrayAsync(audit));
              using var dbScope = factory.OpenScope();
              var db = dbScope.ServiceProvider.GetRequiredService<FinnovaDbContext>();
              Assert.Equal(1, await db.ReceiptDocumentRetirements.CountAsync(x => x.ReceiptId == PendingId));
              Assert.Equal("REC-302 correction", (await db.ReceiptDocumentRetirements.SingleAsync(x => x.ReceiptId == PendingId)).CancellationReason);
          }

          [Fact]
          public async Task AuthorizedDirectModifyAndCancelReturnApprovedCorrectionRule()
          {
              factory.Reset();
              using var client = factory.CreateAuthorizedClient();
              var version = await VersionAsync(client, AuthorizedId);
              var modify = await client.PatchAsync($"/api/receipts/{AuthorizedId}/control", Json("{\"expectedReceiptVersion\":\"" + version + "\",\"idempotencyKey\":\"20000000-0000-0000-0000-000000000004\",\"mutableFields\":{\"Remarks\":\"x\"}}"));
              var cancel = await client.PostAsync($"/api/receipts/{AuthorizedId}/cancel", Json("{\"expectedReceiptVersion\":\"" + version + "\",\"idempotencyKey\":\"20000000-0000-0000-0000-000000000005\",\"cancellationReason\":\"Correction\"}"));
              await AssertRuleAsync(modify, "AUTHORIZED_RECEIPT_PROTECTED");
              await AssertRuleAsync(cancel, "AUTHORIZED_RECEIPT_PROTECTED");
          }

          [Fact]
          public async Task PriorAccrualProducesOneAcceptedReversalRequest()
          {
              factory.Reset();
              factory.Accounting.Accruals[PendingId] = new ReceiptAccrualFact(true, "accrual/302", 1250.00m, new DateOnly(2026, 1, 2), Guid.NewGuid(), "INR");
              using var client = factory.CreateAuthorizedClient();
              var response = await CancelAsync(client, PendingId, "20000000-0000-0000-0000-000000000006", "Accrual correction");
              response.EnsureSuccessStatusCode();
              Assert.Single(factory.Accounting.AcceptedReversals);
              Assert.Equal("INR", factory.Accounting.AcceptedReversals.Single().CurrencyCode);
              Assert.Equal(1250.00m, factory.Accounting.AcceptedReversals.Single().Amount);
          }

          [Fact]
          public async Task NoAccrualRecordsNoReversalOutcome()
          {
              factory.Reset();
              using var client = factory.CreateAuthorizedClient();
              var response = await CancelAsync(client, PendingId, "20000000-0000-0000-0000-000000000007", "No accrual correction");
              response.EnsureSuccessStatusCode();
              var summary = await JsonAsync(response);
              Assert.False(summary.GetProperty("requiresAccountingReversal").GetBoolean());
              Assert.Empty(factory.Accounting.AcceptedReversals);
              using var dbScope = factory.OpenScope();
              var db = dbScope.ServiceProvider.GetRequiredService<FinnovaDbContext>();
              Assert.Equal("NoReversalRequired", (await db.Receipts.SingleAsync(x => x.Id == PendingId)).AccrualOutcome);
          }

          [Fact]
          public async Task RequiredAccountingRejectionLeavesPreCancellationState()
          {
              factory.Reset();
              factory.Accounting.Reject = true;
              factory.Accounting.Accruals[PendingId] = new ReceiptAccrualFact(true, "accrual/302", 1250.00m, new DateOnly(2026, 1, 2), Guid.NewGuid(), "INR");
              using var client = factory.CreateAuthorizedClient();
              var response = await CancelAsync(client, PendingId, "20000000-0000-0000-0000-000000000008", "Boundary rejection");
              Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
              using var dbScope = factory.OpenScope();
              var db = dbScope.ServiceProvider.GetRequiredService<FinnovaDbContext>();
              var receipt = await db.Receipts.SingleAsync(x => x.Id == PendingId);
              Assert.Equal("Pending Authorization", receipt.ReceiptStatus);
              Assert.Empty(await db.ReceiptDocumentRetirements.ToListAsync());
              Assert.Empty(await db.ReceiptAuditEntries.Where(x => x.ReceiptId == PendingId).ToListAsync());
          }

          [Fact]
          public async Task ConcurrentReprintsUseUniqueSequentialCopyNumbers()
          {
              factory.Reset();
              using var first = factory.CreateAuthorizedClient();
              using var second = factory.CreateAuthorizedClient();
              var requests = new[] { "30000000-0000-0000-0000-000000000001", "30000000-0000-0000-0000-000000000002" };
              var responses = await Task.WhenAll(requests.Select(key => ReprintAsync(first, PrintedId, key)));
              Assert.All(responses, response => response.EnsureSuccessStatusCode());
              var bodies = await Task.WhenAll(responses.Select(JsonAsync));
              Assert.All(bodies, body =>
              {
                  Assert.Equal("DUPLICATE RECEIPT", body.GetProperty("outputTitle").GetString());
                  Assert.Equal("DUPLICATE RECEIPT", body.GetProperty("watermarkText").GetString());
                  Assert.Equal("RCT-101", body.GetProperty("documentNumber").GetString());
              });
              var copies = bodies.Select(body => body.GetProperty("duplicateCopyNumber").GetInt32()).OrderBy(x => x).ToArray();
              Assert.Equal(new[] { 1, 2 }, copies);
              using var dbScope = factory.OpenScope();
              var db = dbScope.ServiceProvider.GetRequiredService<FinnovaDbContext>();
              Assert.Equal(2, await db.ReceiptDuplicateCopies.CountAsync(x => x.ReceiptId == PrintedId));
          }

          [Fact]
          public async Task SameIdempotencyKeyReplaysAndDifferentPayloadConflicts()
          {
              factory.Reset();
              using var client = factory.CreateAuthorizedClient();
              var first = await ReprintAsync(client, PrintedId, "30000000-0000-0000-0000-000000000003");
              var replay = await ReprintAsync(client, PrintedId, "30000000-0000-0000-0000-000000000003");
              first.EnsureSuccessStatusCode();
              replay.EnsureSuccessStatusCode();
              Assert.Equal(await first.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
              var version = await VersionAsync(client, PendingId);
              var cancel = await client.PostAsync($"/api/receipts/{PendingId}/cancel", Json("{\"expectedReceiptVersion\":\"" + version + "\",\"idempotencyKey\":\"40000000-0000-0000-0000-000000000001\",\"cancellationReason\":\"First reason\"}"));
              cancel.EnsureSuccessStatusCode();
              var mismatch = await client.PostAsync($"/api/receipts/{PendingId}/cancel", Json("{\"expectedReceiptVersion\":\"" + version + "\",\"idempotencyKey\":\"40000000-0000-0000-0000-000000000001\",\"cancellationReason\":\"Different reason\"}"));
              await AssertRuleAsync(mismatch, "IDEMPOTENCY_CONFLICT");
          }

          [Fact]
          public async Task OutOfScopeReceiptReturns403WithoutReceiptDetails()
          {
              factory.Reset();
              factory.Scope.CanRead = false;
              using var client = factory.CreateAuthorizedClient();
              var response = await client.GetAsync($"/api/receipts/{PendingId}/control-summary");
              Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
              Assert.DoesNotContain("RCT-302", await response.Content.ReadAsStringAsync());
          }

          [Fact]
          public async Task GatewayFacingReceiptEndpointRetainsBearerHeaderContract()
          {
              factory.Reset();
              using var client = factory.CreateClient();
              client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.TokenFor("gateway-user", "BranchSupervisor"));
              var response = await client.GetAsync($"/api/receipts/{PendingId}/control-summary");
              Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
          }

          private static StringContent Json(string value) => new(value, Encoding.UTF8, "application/json");
          private static async Task<HttpResponseMessage> CancelAsync(HttpClient client, Guid id, string key, string reason)
          {
              var version = await VersionAsync(client, id);
              return await client.PostAsync($"/api/receipts/{id}/cancel", Json($"{{\"expectedReceiptVersion\":\"{version}\",\"idempotencyKey\":\"{key}\",\"cancellationReason\":\"{reason}\"}}"));
          }
          private static Task<HttpResponseMessage> ReprintAsync(HttpClient client, Guid id, string key)
          {
              var request = new HttpRequestMessage(HttpMethod.Post, $"/api/receipts/{id}/reprint");
              request.Headers.Add("Idempotency-Key", key);
              return client.SendAsync(request);
          }
          private static async Task<string> VersionAsync(HttpClient client, Guid id)
          {
              var response = await client.GetAsync($"/api/receipts/{id}/control-summary");
              response.EnsureSuccessStatusCode();
              return (await JsonAsync(response)).GetProperty("receiptVersion").GetString()!;
          }
          private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
          private static async Task<JsonElement[]> JsonArrayAsync(HttpResponseMessage response) => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.EnumerateArray().ToArray();
          private static async Task AssertRuleAsync(HttpResponseMessage response, string rule)
          {
              Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
              var body = await JsonAsync(response);
              Assert.Equal(rule, body.GetProperty("rule").GetString());
          }
      }

      public sealed class ReceiptControlApiFactory : WebApplicationFactory<Program>
      {
          public readonly ReceiptControlAccountingStub Accounting = new();
          public readonly ReceiptControlChallanStub Challan = new();
          public readonly ReceiptControlPrintStub Print = new();
          public readonly ReceiptControlScopeStub Scope = new();
          private readonly string databaseName = $"receipt-controls-{Guid.NewGuid():N}";
          private readonly byte[] signingKey = Encoding.UTF8.GetBytes("dev-only-signing-key-change-me-min-32-bytes-long-0123456789");

          protected override void ConfigureWebHost(IWebHostBuilder builder)
          {
              builder.UseEnvironment("Development");
              builder.ConfigureTestServices(services =>
              {
                  services.RemoveAll<DbContextOptions<FinnovaDbContext>>();
                  services.RemoveAll<FinnovaDbContext>();
                  services.AddDbContext<FinnovaDbContext>(options => options.UseInMemoryDatabase(databaseName));
                  services.RemoveAll<IReceiptAccountingBoundary>();
                  services.RemoveAll<IReceiptChallanPort>();
                  services.RemoveAll<IReceiptPrintPort>();
                  services.RemoveAll<IReceiptScopePort>();
                  services.AddSingleton<IReceiptAccountingBoundary>(Accounting);
                  services.AddSingleton<IReceiptChallanPort>(Challan);
                  services.AddSingleton<IReceiptPrintPort>(Print);
                  services.AddSingleton<IReceiptScopePort>(Scope);
              });
          }

          public void Reset()
          {
              using var scope = Services.CreateScope();
              var db = scope.ServiceProvider.GetRequiredService<FinnovaDbContext>();
              db.Database.EnsureDeleted();
              db.Database.EnsureCreated();
              db.Receipts.AddRange(Receipt("10000000-0000-0000-0000-000000000001", "RCT-302", "Pending Authorization", "Pending", false), Receipt("10000000-0000-0000-0000-000000000002", "RCT-205", "Pending Authorization", "Pending", false), Receipt("10000000-0000-0000-0000-000000000003", "RCT-303", "Authorized", "Authorized", false), Receipt("10000000-0000-0000-0000-000000000004", "RCT-101", "Printed", "Authorized", true));
              db.SaveChanges();
              Accounting.Reset(); Challan.Reset(); Print.Reset(); Scope.Reset();
              Challan.Facts[Guid.Parse("10000000-0000-0000-0000-000000000002")] = new ReceiptChallanFact(true, "CH-205");
              Print.Facts[Guid.Parse("10000000-0000-0000-0000-000000000004")] = new ReceiptPrintFact(true, true, null);
          }

          public HttpClient CreateAuthorizedClient() => CreateClientWithToken(TokenFor("11111111-1111-1111-1111-111111111111", "BranchSupervisor"));
          public string TokenFor(string actor, string role = "BranchSupervisor")
          {
              if (actor == "not-a-jwt") return actor;
              var now = DateTime.UtcNow;
              var expires = actor == "expired" ? now.AddMinutes(-5) : now.AddMinutes(20);
              var key = actor == "invalid-signature" ? Encoding.UTF8.GetBytes("invalid-signature-key-invalid-signature-key") : signingKey;
              var token = new JwtSecurityToken("Finnova", "FinnovaClients", new[] { new Claim(ClaimTypes.NameIdentifier, actor == "expired" || actor == "invalid-signature" ? "11111111-1111-1111-1111-111111111111" : actor), new Claim(ClaimTypes.Role, role) }, now.AddMinutes(-1), expires, new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256));
              return new JwtSecurityTokenHandler().WriteToken(token);
          }
          public IServiceScope OpenScope() => Services.CreateScope();

          private static Receipt Receipt(string id, string documentNumber, string status, string authorization, bool printed) => new()
          {
              Id = Guid.Parse(id), DocumentNumber = documentNumber, NumberingMode = "Online", NumberingScopeKey = "COMPANY-1/BRANCH-1",
              ReceiptType = "Collection", CollectionBranchId = Guid.Parse("22222222-2222-2222-2222-222222222222"), CollectionBranchCode = "BR-001",
              ReceiptStatus = status, AuthorizationStatus = authorization, RowVersion = new byte[] { 1 }, DuplicationCount = 0,
              CreatedAtUtc = DateTime.UtcNow.AddHours(-1), UpdatedAtUtc = DateTime.UtcNow, CreatedBy = "11111111-1111-1111-1111-111111111111"
          };
      }

      public sealed class ReceiptControlScopeStub : IReceiptScopePort
      {
          public bool CanRead { get; set; } = true;
          public void Reset() => CanRead = true;
          public Task<ReceiptScopeFact> ResolveAsync(Guid actorId, Guid receiptId, CancellationToken cancellationToken) => Task.FromResult(new ReceiptScopeFact(CanRead, CanRead, CanRead, CanRead));
      }

      public sealed class ReceiptControlChallanStub : IReceiptChallanPort
      {
          public Dictionary<Guid, ReceiptChallanFact> Facts { get; } = new();
          public void Reset() => Facts.Clear();
          public Task<ReceiptChallanFact> GetAsync(Guid receiptId, CancellationToken cancellationToken) => Task.FromResult(Facts.TryGetValue(receiptId, out var fact) ? fact : new ReceiptChallanFact(false, null));
      }

      public sealed class ReceiptControlPrintStub : IReceiptPrintPort
      {
          public Dictionary<Guid, ReceiptPrintFact> Facts { get; } = new();
          public void Reset() => Facts.Clear();
          public Task<ReceiptPrintFact> GetEligibilityAsync(Guid receiptId, string actorId, CancellationToken cancellationToken) => Task.FromResult(Facts.TryGetValue(receiptId, out var fact) ? fact : new ReceiptPrintFact(false, false, "The Receipt has not been printed."));
          public Task<string> CreateDuplicateOutputReferenceAsync(Guid receiptId, int duplicateCopyNumber, CancellationToken cancellationToken) => Task.FromResult($"receipt://{receiptId}/duplicate/{duplicateCopyNumber}");
      }

      public sealed class ReceiptControlAccountingStub : IReceiptAccountingBoundary
      {
          public Dictionary<Guid, ReceiptAccrualFact> Accruals { get; } = new();
          public List<ReceiptReversalRequest> AcceptedReversals { get; } = new();
          public bool Reject { get; set; }
          public void Reset() { Accruals.Clear(); AcceptedReversals.Clear(); Reject = false; }
          public Task<ReceiptAccrualFact> GetAccrualAsync(Guid receiptId, CancellationToken cancellationToken) => Task.FromResult(Accruals.TryGetValue(receiptId, out var fact) ? fact : new ReceiptAccrualFact(false, null, null, null, null, "INR"));
          public Task<ReversalAcceptance> AcceptReversalAsync(ReceiptReversalRequest request, CancellationToken cancellationToken)
          {
              if (Reject) return Task.FromResult(new ReversalAcceptance(false, null, null, "REJECTED", "The accounting boundary rejected the reversal."));
              AcceptedReversals.Add(request);
              return Task.FromResult(new ReversalAcceptance(true, Guid.NewGuid(), $"reversal/{request.ReceiptId}", "ACCEPTED", "Reversal accepted."));
          }
      }
      ```
    - Replace only the marked final Receipt property names and test seed status values at the compatibility gate. The tests must keep the explicit scenarios, assert `CHALLAN_PROTECTED`, `AUTHORIZED_RECEIPT_PROTECTED`, `IDEMPOTENCY_CONFLICT`, exact `DUPLICATE RECEIPT` output through the response tests, INR accounting payloads, no direct canonical journal write, and unchanged state after a required accounting rejection.
    - **Requirements:** 1.1-10.18; **REC coverage:** REC-205, REC-302, REC-101.

  - [ ] 9.3 CREATE `E:\Finnova\Finnova-API\Finnova.Tests\ReceiptControls\ReceiptControlPersistenceTests.cs`
    - **GATE:** Use the existing SQL Server EF test fixture or the approved FINNOVA test connection. The test file below refuses to run without `FINNOVA_TEST_CONNECTION`; it does not silently substitute the InMemory provider because InMemory does not enforce unique indexes or relational transactions.
    - Create this complete EF persistence test file:
      ```csharp
      using Finnova.Models.Domain.Entities;
      using Finnova.Repository.Context;
      using Finnova.Repository.Interfaces;
      using Finnova.Repository.Repositories;
      using Microsoft.EntityFrameworkCore;
      using Xunit;

      namespace Finnova.Tests.ReceiptControls;

      public sealed class ReceiptControlPersistenceTests
      {
          private static readonly string ConnectionString = Environment.GetEnvironmentVariable("FINNOVA_TEST_CONNECTION")
              ?? throw new InvalidOperationException("FINNOVA_TEST_CONNECTION is required for relational receipt-control persistence tests.");

          private static FinnovaDbContext CreateContext()
          {
              var options = new DbContextOptionsBuilder<FinnovaDbContext>().UseSqlServer(ConnectionString).Options;
              return new FinnovaDbContext(options);
          }

          [Fact]
          public void ConfirmedIndexesAreUniqueForRetirementDuplicateAndIdempotencyRecords()
          {
              using var db = CreateContext();
              var retirement = db.Model.FindEntityType(typeof(ReceiptDocumentRetirement))!;
              var duplicate = db.Model.FindEntityType(typeof(ReceiptDuplicateCopy))!;
              var operation = db.Model.FindEntityType(typeof(ReceiptControlOperation))!;
              Assert.Contains(retirement.GetIndexes(), index => index.IsUnique && index.Properties.Select(x => x.Name).SequenceEqual(new[] { "NumberingScopeKey", "DocumentNumber" }));
              Assert.Contains(duplicate.GetIndexes(), index => index.IsUnique && index.Properties.Select(x => x.Name).SequenceEqual(new[] { "ReceiptId", "DuplicateCopyNumber" }));
              Assert.Contains(duplicate.GetIndexes(), index => index.IsUnique && index.Properties.Select(x => x.Name).SequenceEqual(new[] { "ReceiptId", "IdempotencyKey" }));
              Assert.Contains(operation.GetIndexes(), index => index.IsUnique && index.Properties.Select(x => x.Name).SequenceEqual(new[] { "IdempotencyKey" }));
          }

          [Fact]
          public async Task RelationalDatabaseRejectsDuplicateRetirementDuplicateCopyAndIdempotencyRows()
          {
              await using var db = CreateContext();
              await db.Database.EnsureCreatedAsync();
              var receiptId = Guid.NewGuid();
              var key = Guid.NewGuid();
              db.ReceiptDocumentRetirements.Add(new ReceiptDocumentRetirement { ReceiptId = receiptId, DocumentNumber = "PERSIST-1", NumberingMode = "Manual", NumberingScopeKey = "BR-001", ReceiptType = "Collection", RetiredBy = "tester", CancellationReason = "Duplicate" });
              db.ReceiptDuplicateCopies.Add(new ReceiptDuplicateCopy { ReceiptId = receiptId, DocumentNumber = "PERSIST-1", DuplicateCopyNumber = 1, DuplicationCountAfter = 1, OutputReference = "ref-1", PrintedBy = "tester", IdempotencyKey = key });
              db.ReceiptControlOperations.Add(new ReceiptControlOperation { ReceiptId = receiptId, IdempotencyKey = key, Operation = Finnova.Models.Domain.Enums.ReceiptControlOperation.Reprint, RequestHash = "hash-1", ResponseSnapshotJson = "{}" });
              await db.SaveChangesAsync();
              db.ReceiptDuplicateCopies.Add(new ReceiptDuplicateCopy { ReceiptId = receiptId, DocumentNumber = "PERSIST-1", DuplicateCopyNumber = 1, DuplicationCountAfter = 1, OutputReference = "ref-2", PrintedBy = "tester", IdempotencyKey = Guid.NewGuid() });
              db.ReceiptControlOperations.Add(new ReceiptControlOperation { ReceiptId = receiptId, IdempotencyKey = key, Operation = Finnova.Models.Domain.Enums.ReceiptControlOperation.Reprint, RequestHash = "hash-2", ResponseSnapshotJson = "{}" });
              await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
          }

          [Fact]
          public async Task AuditAndRetirementRepositorySurfaceIsAppendOnlyAndAuditIsDescending()
          {
              using var db = CreateContext();
              var repository = new ReceiptControlRepository(db);
              var receiptId = Guid.NewGuid();
              await repository.AddAuditAsync(new ReceiptAuditEntry { ReceiptId = receiptId, Id = Guid.Parse("50000000-0000-0000-0000-000000000001"), Action = "Modify", DocNo = "PERSIST-2", Actor = "tester", OccurredAtUtc = DateTime.UtcNow.AddMinutes(-2) }, default);
              await repository.AddAuditAsync(new ReceiptAuditEntry { ReceiptId = receiptId, Id = Guid.Parse("50000000-0000-0000-0000-000000000002"), Action = "Cancel", DocNo = "PERSIST-2", Actor = "tester", OccurredAtUtc = DateTime.UtcNow }, default);
              await db.SaveChangesAsync();
              var rows = await repository.GetAuditAsync(receiptId, default);
              Assert.Equal(new[] { "Cancel", "Modify" }, rows.Select(x => x.Action).ToArray());
              var methods = typeof(IReceiptControlRepository).GetMethods().Select(x => x.Name).ToArray();
              Assert.DoesNotContain("UpdateAuditAsync", methods);
              Assert.DoesNotContain("DeleteAuditAsync", methods);
              Assert.DoesNotContain("UpdateRetirementAsync", methods);
              Assert.DoesNotContain("DeleteRetirementAsync", methods);
          }

          [Fact]
          public async Task CancellationTransactionRollsBackReceiptAuditRetirementAndReversalProjection()
          {
              await using var db = CreateContext();
              await db.Database.EnsureCreatedAsync();
              var receipt = NewReceipt(Guid.NewGuid(), "PERSIST-3");
              db.Receipts.Add(receipt);
              await db.SaveChangesAsync();
              await using var transaction = await db.Database.BeginTransactionAsync();
              receipt.ReceiptStatus = "Cancelled";
              receipt.CancellationReason = "Rollback";
              receipt.AccrualOutcome = "ReversalRequired";
              receipt.ReversalRequestId = Guid.NewGuid();
              db.ReceiptDocumentRetirements.Add(new ReceiptDocumentRetirement { ReceiptId = receipt.Id, DocumentNumber = receipt.DocumentNumber, NumberingMode = "Online", NumberingScopeKey = "BR-001", ReceiptType = "Collection", RetiredBy = "tester", CancellationReason = "Rollback" });
              db.ReceiptAuditEntries.Add(new ReceiptAuditEntry { ReceiptId = receipt.Id, Action = "Cancel", DocNo = receipt.DocumentNumber, Actor = "tester", OccurredAtUtc = DateTime.UtcNow });
              await db.SaveChangesAsync();
              await transaction.RollbackAsync();
              db.ChangeTracker.Clear();
              var current = await db.Receipts.SingleAsync(x => x.Id == receipt.Id);
              Assert.Equal("Pending Authorization", current.ReceiptStatus);
              Assert.Null(current.ReversalRequestId);
              Assert.Empty(await db.ReceiptDocumentRetirements.Where(x => x.ReceiptId == receipt.Id).ToListAsync());
              Assert.Empty(await db.ReceiptAuditEntries.Where(x => x.ReceiptId == receipt.Id).ToListAsync());
          }

          [Fact]
          public async Task ConcurrentRowVersionCasAllowsAtMostOneStateChangingRequest()
          {
              await using var first = CreateContext();
              await using var second = CreateContext();
              var receiptId = Guid.NewGuid();
              first.Receipts.Add(NewReceipt(receiptId, "PERSIST-4"));
              await first.SaveChangesAsync();
              var a = await first.Receipts.SingleAsync(x => x.Id == receiptId);
              var b = await second.Receipts.SingleAsync(x => x.Id == receiptId);
              a.DuplicationCount = 1;
              b.DuplicationCount = 1;
              await first.SaveChangesAsync();
              await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
              Assert.Equal(1, await first.Receipts.Where(x => x.Id == receiptId).Select(x => x.DuplicationCount).SingleAsync());
          }

          private static Receipt NewReceipt(Guid id, string documentNumber) => new()
          {
              Id = id, DocumentNumber = documentNumber, NumberingMode = "Online", NumberingScopeKey = "BR-001", ReceiptType = "Collection",
              CollectionBranchId = Guid.Parse("22222222-2222-2222-2222-222222222222"), CollectionBranchCode = "BR-001",
              ReceiptStatus = "Pending Authorization", AuthorizationStatus = "Pending", RowVersion = new byte[] { 1 }, DuplicationCount = 0,
              CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow, CreatedBy = "tester"
          };
      }
      ```
    - Replace only final Receipt/audit property mappings after the compatibility gate. Keep relational enforcement, append-only surface assertions, rollback assertions for Receipt/audit/retirement/reversal/counter state, and the two-context row-version race; do not weaken these tests to metadata-only checks.
    - **Requirements:** 4.7, 5.1-5.8, 6.4-6.10, 7.6-7.10, 8.1-8.11, 9.11-9.13.

- [ ] 10. Add frontend model, services, toggle, and barrels
  - [ ] 10.1 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\models\receiptControl.model.ts`
    - Create this complete file:
      ```typescript
      export interface ReceiptControlSummary {
        receiptId: string;
        documentNumber: string;
        receiptStatus: string;
        authorizationStatus: string;
        isChallaned: boolean;
        challanReference: string | null;
        isPrinted: boolean;
        duplicationCount: number;
        receiptVersion: string;
        collectionBranchId: string;
        collectionBranchCode: string;
        availableActions: string[];
        canModify: boolean;
        canCancel: boolean;
        canReprint: boolean;
        requiresAccountingReversal: boolean;
        accountingCorrectionInstruction: string | null;
      }

      export interface ModifyReceiptControlRequest {
        expectedReceiptVersion: string;
        idempotencyKey: string;
        mutableFields: Record<string, string | null>;
      }

      export interface CancelReceiptRequest {
        expectedReceiptVersion: string;
        idempotencyKey: string;
        cancellationReason: string;
      }

      export interface DuplicateReceiptOutput {
        receiptId: string;
        documentNumber: string;
        outputTitle: string;
        watermarkText: string;
        duplicateCopyNumber: number;
        duplicationCount: number;
        printedAtUtc: string;
        outputReference: string;
      }

      export interface ReceiptAuditEntry {
        id: string;
        receiptId: string;
        operation: string;
        documentNumber: string;
        actor: string;
        priorStatus: string | null;
        newStatus: string | null;
        cancellationReason: string | null;
        retirementRecordId: string | null;
        reversalRequestId: string | null;
        duplicateCopyNumber: number | null;
        occurredAtUtc: string;
      }
      ```

  - [ ] 10.2 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\services\interfaces\receiptControl.interface.ts`
    - Create this complete file:
      ```typescript
      import type { CancelReceiptRequest, DuplicateReceiptOutput, ModifyReceiptControlRequest, ReceiptAuditEntry, ReceiptControlSummary } from '../../models';

      export interface IReceiptControlService {
        getSummary(receiptId: string): Promise<ReceiptControlSummary>;
        modify(receiptId: string, request: ModifyReceiptControlRequest): Promise<ReceiptControlSummary>;
        cancel(receiptId: string, request: CancelReceiptRequest): Promise<ReceiptControlSummary>;
        reprint(receiptId: string, idempotencyKey: string): Promise<DuplicateReceiptOutput>;
        getAudit(receiptId: string): Promise<ReceiptAuditEntry[]>;
      }
      ```

  - [ ] 10.3 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\services\real\receiptControl.real.ts`
    - Create this complete file using only the shared axios instance:
      ```typescript
      import api from '../api';
      import type { CancelReceiptRequest, DuplicateReceiptOutput, ModifyReceiptControlRequest, ReceiptAuditEntry, ReceiptControlSummary } from '../../models';
      import type { IReceiptControlService } from '../interfaces/receiptControl.interface';

      class ReceiptControlRealService implements IReceiptControlService {
        private readonly basePath = '/receipts';

        async getSummary(receiptId: string): Promise<ReceiptControlSummary> {
          const response = await api.get<ReceiptControlSummary>(`${this.basePath}/${receiptId}/control-summary`);
          return response.data;
        }

        async modify(receiptId: string, request: ModifyReceiptControlRequest): Promise<ReceiptControlSummary> {
          const response = await api.patch<ReceiptControlSummary>(`${this.basePath}/${receiptId}/control`, request);
          return response.data;
        }

        async cancel(receiptId: string, request: CancelReceiptRequest): Promise<ReceiptControlSummary> {
          const response = await api.post<ReceiptControlSummary>(`${this.basePath}/${receiptId}/cancel`, request);
          return response.data;
        }

        async reprint(receiptId: string, idempotencyKey: string): Promise<DuplicateReceiptOutput> {
          const response = await api.post<DuplicateReceiptOutput>(`${this.basePath}/${receiptId}/reprint`, undefined, { headers: { 'Idempotency-Key': idempotencyKey } });
          return response.data;
        }

        async getAudit(receiptId: string): Promise<ReceiptAuditEntry[]> {
          const response = await api.get<ReceiptAuditEntry[]>(`${this.basePath}/${receiptId}/audit`);
          return response.data;
        }
      }

      export const receiptControlRealService = new ReceiptControlRealService();
      ```

  - [ ] 10.4 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\services\mock\receiptControl.mock.ts`
    - Create this complete in-memory implementation. It uses no HTTP client, returns backend-shaped errors, keeps rejected operations state-preserving, and exposes `resetReceiptControlMock` for Vitest isolation:
      ```typescript
      import type { CancelReceiptRequest, DuplicateReceiptOutput, ModifyReceiptControlRequest, ReceiptAuditEntry, ReceiptControlSummary } from '../../models';
      import type { IReceiptControlService } from '../interfaces/receiptControl.interface';

      export const ERR_RCC_400 = 'ERR-RCC-400';
      export const ERR_RCC_403 = 'ERR-RCC-403';
      export const ERR_RCC_409 = 'ERR-RCC-409';
      export const ERR_RCC_422 = 'ERR-RCC-422';
      export const ERR_RCC_503 = 'ERR-RCC-503';

      type MockError = Error & { response: { status: number; data: { code: string; message: string; rule?: string } } };

      const error = (status: number, code: string, message: string, rule?: string): MockError => {
        const result = new Error(message) as MockError;
        result.response = { status, data: { code, message, ...(rule ? { rule } : {}) } };
        return result;
      };

      const clone = <T>(value: T): T => JSON.parse(JSON.stringify(value)) as T;
      const freshKey = () => (globalThis.crypto?.randomUUID?.() ?? `mock-${Date.now()}-${Math.random()}`);
      const now = () => new Date().toISOString();

      const seed = (): ReceiptControlSummary[] => [
        {
          receiptId: 'receipt-pending', documentNumber: 'RCT-302', receiptStatus: 'Pending Authorization', authorizationStatus: 'Pending',
          isChallaned: false, challanReference: null, isPrinted: false, duplicationCount: 0, receiptVersion: 'AQ==',
          collectionBranchId: 'branch-1', collectionBranchCode: 'BR-001', availableActions: ['Modify', 'Cancel'], canModify: true,
          canCancel: true, canReprint: false, requiresAccountingReversal: false, accountingCorrectionInstruction: null,
        },
        {
          receiptId: 'receipt-challaned', documentNumber: 'RCT-205', receiptStatus: 'Pending Authorization', authorizationStatus: 'Pending',
          isChallaned: true, challanReference: 'CH-205', isPrinted: false, duplicationCount: 0, receiptVersion: 'AQ==',
          collectionBranchId: 'branch-1', collectionBranchCode: 'BR-001', availableActions: [], canModify: false,
          canCancel: false, canReprint: false, requiresAccountingReversal: false, accountingCorrectionInstruction: null,
        },
        {
          receiptId: 'receipt-authorized', documentNumber: 'RCT-303', receiptStatus: 'Authorized', authorizationStatus: 'Authorized',
          isChallaned: false, challanReference: null, isPrinted: false, duplicationCount: 0, receiptVersion: 'AQ==',
          collectionBranchId: 'branch-1', collectionBranchCode: 'BR-001', availableActions: [], canModify: false,
          canCancel: false, canReprint: false, requiresAccountingReversal: false, accountingCorrectionInstruction: 'Use the approved Cheque Return or JV reversal process.',
        },
        {
          receiptId: 'receipt-printed', documentNumber: 'RCT-101', receiptStatus: 'Printed', authorizationStatus: 'Authorized',
          isChallaned: false, challanReference: null, isPrinted: true, duplicationCount: 0, receiptVersion: 'AQ==',
          collectionBranchId: 'branch-1', collectionBranchCode: 'BR-001', availableActions: ['Reprint'], canModify: false,
          canCancel: false, canReprint: true, requiresAccountingReversal: false, accountingCorrectionInstruction: null,
        },
      ];

      let summaries = seed();
      let audit: ReceiptAuditEntry[] = [];
      const completed = new Map<string, { hash: string; value: unknown }>();

      const get = (receiptId: string): ReceiptControlSummary => {
        const found = summaries.find((item) => item.receiptId === receiptId);
        if (!found) throw error(404, 'ERR-RCC-404', 'Receipt was not found.');
        return found;
      };

      const assertVersion = (summary: ReceiptControlSummary, version: string) => {
        if (summary.receiptVersion !== version) throw error(409, ERR_RCC_409, 'The Receipt Version is stale. Refresh and retry.', 'RECEIPT_VERSION_CONFLICT');
      };

      const complete = <T>(key: string, hash: string, value: T): T => {
        const prior = completed.get(key);
        if (prior && prior.hash !== hash) throw error(409, ERR_RCC_409, 'The Idempotency Key was already used for a different request.', 'IDEMPOTENCY_CONFLICT');
        if (prior) return clone(prior.value as T);
        completed.set(key, { hash, value: clone(value) });
        return clone(value);
      };

      class ReceiptControlMockService implements IReceiptControlService {
        async getSummary(receiptId: string): Promise<ReceiptControlSummary> {
          return clone(get(receiptId));
        }

        async modify(receiptId: string, request: ModifyReceiptControlRequest): Promise<ReceiptControlSummary> {
          const current = get(receiptId);
          if (!current.canModify) {
            const rule = current.isChallaned ? 'CHALLAN_PROTECTED' : current.authorizationStatus === 'Authorized' ? 'AUTHORIZED_RECEIPT_PROTECTED' : 'INVALID_RECEIPT_STATE';
            throw error(409, ERR_RCC_409, current.isChallaned ? 'The Receipt is linked to a challan and cannot be modified.' : 'The Receipt cannot be modified in its current state.', rule);
          }
          assertVersion(current, request.expectedReceiptVersion);
          if (Object.keys(request.mutableFields).length === 0) throw error(400, ERR_RCC_400, 'At least one mutable field is required.');
          const hash = JSON.stringify(request);
          const changed = { ...current, receiptVersion: 'Ag==', availableActions: ['Modify', 'Cancel'] };
          const result = complete(request.idempotencyKey, hash, changed);
          summaries = summaries.map((item) => item.receiptId === receiptId ? clone(result) : item);
          audit = [{ id: `audit-${audit.length + 1}`, receiptId, operation: 'Modify', documentNumber: current.documentNumber, actor: 'mock-supervisor', priorStatus: current.receiptStatus, newStatus: current.receiptStatus, cancellationReason: null, retirementRecordId: null, reversalRequestId: null, duplicateCopyNumber: null, occurredAtUtc: now() }, ...audit];
          return clone(result);
        }

        async cancel(receiptId: string, request: CancelReceiptRequest): Promise<ReceiptControlSummary> {
          const current = get(receiptId);
          if (!request.cancellationReason.trim()) throw error(400, ERR_RCC_400, 'Cancellation Reason is required.');
          const hash = JSON.stringify({ ...request, cancellationReason: request.cancellationReason.trim() });
          const prior = completed.get(request.idempotencyKey);
          if (prior) {
            if (prior.hash !== hash) throw error(409, ERR_RCC_409, 'The Idempotency Key was already used for a different request.', 'IDEMPOTENCY_CONFLICT');
            return clone(prior.value as ReceiptControlSummary);
          }
          if (!current.canCancel) {
            const rule = current.isChallaned ? 'CHALLAN_PROTECTED' : current.authorizationStatus === 'Authorized' ? 'AUTHORIZED_RECEIPT_PROTECTED' : 'INVALID_RECEIPT_STATE';
            throw error(409, ERR_RCC_409, current.isChallaned ? 'The Receipt is linked to a challan and cannot be cancelled.' : 'The Receipt cannot be cancelled in its current state.', rule);
          }
          assertVersion(current, request.expectedReceiptVersion);
          const changed: ReceiptControlSummary = { ...current, receiptStatus: 'Cancelled', canModify: false, canCancel: false, canReprint: false, availableActions: [] };
          const result = complete(request.idempotencyKey, hash, changed);
          summaries = summaries.map((item) => item.receiptId === receiptId ? clone(result) : item);
          audit = [{ id: `audit-${audit.length + 1}`, receiptId, operation: 'Cancel', documentNumber: current.documentNumber, actor: 'mock-supervisor', priorStatus: current.receiptStatus, newStatus: 'Cancelled', cancellationReason: request.cancellationReason.trim(), retirementRecordId: `retirement-${receiptId}`, reversalRequestId: null, duplicateCopyNumber: null, occurredAtUtc: now() }, ...audit];
          return clone(result);
        }

        async reprint(receiptId: string, idempotencyKey: string): Promise<DuplicateReceiptOutput> {
          const current = get(receiptId);
          if (!current.canReprint) throw error(409, ERR_RCC_409, 'The Receipt is not eligible for duplicate printing.', 'PRINT_NOT_ELIGIBLE');
          const copyNumber = current.duplicationCount + 1;
          const result: DuplicateReceiptOutput = { receiptId, documentNumber: current.documentNumber, outputTitle: 'DUPLICATE RECEIPT', watermarkText: 'DUPLICATE RECEIPT', duplicateCopyNumber: copyNumber, duplicationCount: copyNumber, printedAtUtc: now(), outputReference: `mock://receipts/${receiptId}/duplicate/${copyNumber}` };
          const completedResult = complete(idempotencyKey, JSON.stringify({ receiptId, idempotencyKey }), result);
          summaries = summaries.map((item) => item.receiptId === receiptId ? { ...item, duplicationCount: completedResult.duplicationCount, receiptVersion: 'Ag==' } : item);
          audit = [{ id: `audit-${audit.length + 1}`, receiptId, operation: 'Reprint', documentNumber: current.documentNumber, actor: 'mock-supervisor', priorStatus: current.receiptStatus, newStatus: current.receiptStatus, cancellationReason: null, retirementRecordId: null, reversalRequestId: null, duplicateCopyNumber: completedResult.duplicateCopyNumber, occurredAtUtc: completedResult.printedAtUtc }, ...audit];
          return clone(completedResult);
        }

        async getAudit(receiptId: string): Promise<ReceiptAuditEntry[]> {
          get(receiptId);
          return clone(audit.filter((entry) => entry.receiptId === receiptId));
        }
      }

      export const receiptControlMockService = new ReceiptControlMockService();
      export const resetReceiptControlMock = (): void => { summaries = seed(); audit = []; completed.clear(); };
      export const createReceiptControlMockIdempotencyKey = freshKey;
      ```
    - The mock must keep the exact markers `DUPLICATE RECEIPT`, preserve `documentNumber` through cancellation/reprint, return English stable rule codes, and retain unchanged summary/audit state whenever validation, permission, state, or version checks throw.

  - [ ] 10.5 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\services\receiptControl.service.ts`
    - Create this complete toggle file:
      ```typescript
      import type { IReceiptControlService } from './interfaces/receiptControl.interface';
      import { receiptControlMockService } from './mock/receiptControl.mock';
      import { receiptControlRealService } from './real/receiptControl.real';

      const useMock = import.meta.env.VITE_USE_MOCK_API === 'true';
      export const receiptControlService: IReceiptControlService = useMock ? receiptControlMockService : receiptControlRealService;
      ```

  - [ ] 10.6 MODIFY `E:\Finnova\Finnova-UI\Finnova-UI\src\models\index.ts`
    - Insert after the existing model exports:
      ```typescript
      export * from './receiptControl.model';
      ```

  - [ ] 10.7 MODIFY `E:\Finnova\Finnova-UI\Finnova-UI\src\services\interfaces\index.ts`
    - Insert:
      ```typescript
      export type { IReceiptControlService } from './receiptControl.interface';
      ```

  - [ ] 10.8 MODIFY `E:\Finnova\Finnova-UI\Finnova-UI\src\services\index.ts`
    - Insert:
      ```typescript
      export { receiptControlService } from './receiptControl.service';
      export type { IReceiptControlService } from './interfaces/receiptControl.interface';
      ```

- [ ] 11. Add frontend page, components, routes, and navigation
  - [ ] 11.1 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\components\receiptControl\ReceiptControlStatus.tsx`
    - Create this complete MUI status component. It accepts server permissions and never derives authorization from client role data:
      ```tsx
      import { Alert, Box, Button, Chip, Divider, Stack, Typography } from '@mui/material';
      import type { ReceiptControlSummary } from '../../models';

      export interface ReceiptControlStatusProps {
        summary: ReceiptControlSummary;
        onEdit: () => void;
        onCancel: () => void;
        onReprint: () => void;
      }

      export default function ReceiptControlStatus({ summary, onEdit, onCancel, onReprint }: ReceiptControlStatusProps) {
        const protectedByChallan = summary.isChallaned;
        const protectedByAuthorization = summary.authorizationStatus.toLowerCase() === 'authorized' || summary.receiptStatus.toLowerCase() === 'authorized';
        return (
          <Stack spacing={2}>
            <Box>
              <Typography variant="h5">Receipt Controls</Typography>
              <Typography variant="body2" color="text.secondary">Document Number: {summary.documentNumber}</Typography>
            </Box>
            <Divider />
            <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
              <Chip label={`Receipt Status: ${summary.receiptStatus}`} />
              <Chip label={`Authorization Status: ${summary.authorizationStatus}`} />
              <Chip label={summary.isChallaned ? `Challaned: ${summary.challanReference ?? 'Yes'}` : 'Challaned: No'} />
              <Chip label={`Printed: ${summary.isPrinted ? 'Yes' : 'No'}`} />
              <Chip label={`Duplication Count: ${summary.duplicationCount}`} />
              <Chip label={`Branch: ${summary.collectionBranchCode}`} />
              <Chip label={`Receipt Version: ${summary.receiptVersion}`} />
            </Stack>
            {protectedByChallan && <Alert severity="warning">This Receipt is linked to a challan and cannot be modified or cancelled.</Alert>}
            {protectedByAuthorization && !protectedByChallan && <Alert severity="info">This Receipt is authorized. Use the approved Cheque Return or JV reversal process.</Alert>}
            {summary.requiresAccountingReversal && <Alert severity="warning">Accounting reversal is required before cancellation can complete.</Alert>}
            <Stack direction="row" spacing={1}>
              <Button variant="outlined" onClick={onEdit} disabled={!summary.canModify} aria-label="Edit receipt">Edit</Button>
              <Button variant="outlined" color="warning" onClick={onCancel} disabled={!summary.canCancel} aria-label="Cancel receipt">Cancel</Button>
              <Button variant="contained" onClick={onReprint} disabled={!summary.canReprint} aria-label="Reprint receipt">Reprint</Button>
            </Stack>
          </Stack>
        );
      }
      ```
    - The component must render only English labels, must not add `dir="rtl"`, and must keep Edit and Cancel disabled when the server returns `false`, including challaned and authorized receipts.

  - [ ] 11.2 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\components\receiptControl\ReceiptCancellationDialog.tsx`
    - Create this complete controlled dialog. It creates the idempotency key only on submit, sends the displayed server version, and calls `onCancelled` only after the service succeeds:
      ```tsx
      import { useState } from 'react';
      import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, TextField, Typography } from '@mui/material';
      import type { ReceiptControlSummary } from '../../models';
      import { receiptControlService } from '../../services';

      const key = (): string => globalThis.crypto?.randomUUID?.() ?? `receipt-${Date.now()}-${Math.random()}`;

      export interface ReceiptCancellationDialogProps {
        open: boolean;
        summary: ReceiptControlSummary | null;
        onClose: () => void;
        onCancelled: (summary: ReceiptControlSummary) => void;
        onError: (error: unknown) => void;
      }

      export default function ReceiptCancellationDialog({ open, summary, onClose, onCancelled, onError }: ReceiptCancellationDialogProps) {
        const [reason, setReason] = useState('');
        const [saving, setSaving] = useState(false);
        const submit = async () => {
          if (!summary || !reason.trim()) return;
          setSaving(true);
          try {
            const result = await receiptControlService.cancel(summary.receiptId, { expectedReceiptVersion: summary.receiptVersion, idempotencyKey: key(), cancellationReason: reason.trim() });
            setReason('');
            onCancelled(result);
          } catch (error) {
            onError(error);
          } finally {
            setSaving(false);
          }
        };
        return (
          <Dialog open={open} onClose={saving ? undefined : onClose} aria-labelledby="receipt-cancellation-title" fullWidth maxWidth="sm">
            <DialogTitle id="receipt-cancellation-title">Cancel Receipt</DialogTitle>
            <DialogContent>
              <Typography>Document Number: {summary?.documentNumber ?? ''}</Typography>
              <Alert severity="warning" sx={{ my: 2 }}>Cancellation permanently retires this Document Number.</Alert>
              <TextField autoFocus fullWidth multiline minRows={3} label="Cancellation Reason" value={reason} onChange={(event) => setReason(event.target.value)} error={reason.length > 0 && !reason.trim()} helperText="Enter an English reason for cancellation." />
            </DialogContent>
            <DialogActions>
              <Button onClick={onClose} disabled={saving}>Close</Button>
              <Button onClick={submit} variant="contained" color="warning" disabled={saving || !reason.trim()}>Cancel Receipt</Button>
            </DialogActions>
          </Dialog>
        );
      }
      ```
    - Do not mutate the parent summary or close the dialog on rejection. The shared axios interceptor displays the backend Problem Details message; `onError` lets the page retain its current state and render a local English message if needed.

  - [ ] 11.3 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\components\receiptControl\DuplicateReceiptPreview.tsx`
    - Create this complete MUI print-ready preview. The repeated watermark is visible text with the exact required value; no RTL or localization direction attribute is used:
      ```tsx
      import { Box, Card, CardContent, Chip, Stack, Typography } from '@mui/material';
      import type { DuplicateReceiptOutput } from '../../models';

      export interface DuplicateReceiptPreviewProps { output: DuplicateReceiptOutput | null; }

      export default function DuplicateReceiptPreview({ output }: DuplicateReceiptPreviewProps) {
        if (!output) return null;
        return (
          <Card component="section" aria-label="Duplicate receipt preview" sx={{ mt: 3 }}>
            <CardContent>
              <Stack spacing={2} alignItems="center">
                <Typography variant="h4" fontWeight={800}>{output.outputTitle}</Typography>
                <Box sx={{ border: '2px solid', borderColor: 'warning.main', px: 5, py: 3, width: '100%', textAlign: 'center', position: 'relative', overflow: 'hidden' }}>
                  <Typography aria-label="duplicate watermark" color="warning.main" fontWeight={800} sx={{ letterSpacing: 3, transform: 'rotate(-15deg)', opacity: 0.45 }}>{output.watermarkText}</Typography>
                  <Typography variant="h6" sx={{ mt: 2 }}>Document Number: {output.documentNumber}</Typography>
                  <Chip label={`Duplicate Copy No: ${output.duplicateCopyNumber}`} sx={{ mt: 1 }} />
                  <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>Duplication Count: {output.duplicationCount}</Typography>
                </Box>
              </Stack>
            </CardContent>
          </Card>
        );
      }
      ```
    - The output title, watermark, document number, copy number, and persisted count must be rendered from the server response. Do not generate a copy number or alter the original Document Number in the component.

  - [ ] 11.4 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\pages\ReceiptControl.tsx`
    - Create this complete page. It keeps the last successful server state when any operation fails and uses a fresh UUID for every modification, cancellation, and reprint request:
      ```tsx
      import { useCallback, useEffect, useState } from 'react';
      import { Alert, Box, Button, CircularProgress, Stack, TextField, Typography } from '@mui/material';
      import { useParams } from 'react-router-dom';
      import type { DuplicateReceiptOutput, ReceiptAuditEntry, ReceiptControlSummary } from '../models';
      import { receiptControlService } from '../services';
      import ReceiptControlStatus from '../components/receiptControl/ReceiptControlStatus';
      import ReceiptCancellationDialog from '../components/receiptControl/ReceiptCancellationDialog';
      import DuplicateReceiptPreview from '../components/receiptControl/DuplicateReceiptPreview';

      const key = (): string => globalThis.crypto?.randomUUID?.() ?? `receipt-${Date.now()}-${Math.random()}`;
      const messageOf = (error: unknown): string => {
        const candidate = error as { response?: { data?: { message?: string } }; message?: string };
        return candidate.response?.data?.message ?? candidate.message ?? 'The Receipt operation failed. The displayed state was preserved.';
      };

      export default function ReceiptControl() {
        const params = useParams<{ receiptId?: string }>();
        const receiptId = params.receiptId ?? 'receipt-pending';
        const [summary, setSummary] = useState<ReceiptControlSummary | null>(null);
        const [audit, setAudit] = useState<ReceiptAuditEntry[]>([]);
        const [output, setOutput] = useState<DuplicateReceiptOutput | null>(null);
        const [remarks, setRemarks] = useState('');
        const [editing, setEditing] = useState(false);
        const [cancelOpen, setCancelOpen] = useState(false);
        const [loading, setLoading] = useState(true);
        const [errorMessage, setErrorMessage] = useState<string | null>(null);

        const load = useCallback(async () => {
          setLoading(true);
          try {
            const [nextSummary, nextAudit] = await Promise.all([receiptControlService.getSummary(receiptId), receiptControlService.getAudit(receiptId)]);
            setSummary(nextSummary);
            setAudit(nextAudit);
            setErrorMessage(null);
          } catch (error) {
            setErrorMessage(messageOf(error));
          } finally {
            setLoading(false);
          }
        }, [receiptId]);

        useEffect(() => { void load(); }, [load]);

        const saveEdit = async () => {
          if (!summary || !remarks.trim()) return;
          try {
            const next = await receiptControlService.modify(summary.receiptId, { expectedReceiptVersion: summary.receiptVersion, idempotencyKey: key(), mutableFields: { Remarks: remarks.trim() } });
            setSummary(next);
            setAudit(await receiptControlService.getAudit(next.receiptId));
            setRemarks('');
            setEditing(false);
            setErrorMessage(null);
          } catch (error) {
            setErrorMessage(messageOf(error));
          }
        };

        const cancelSucceeded = async (next: ReceiptControlSummary) => {
          setSummary(next);
          setCancelOpen(false);
          setErrorMessage(null);
          try { setAudit(await receiptControlService.getAudit(next.receiptId)); } catch (error) { setErrorMessage(messageOf(error)); }
        };

        const reprint = async () => {
          if (!summary) return;
          try {
            setOutput(await receiptControlService.reprint(summary.receiptId, key()));
            setSummary(await receiptControlService.getSummary(summary.receiptId));
            setAudit(await receiptControlService.getAudit(summary.receiptId));
            setErrorMessage(null);
          } catch (error) {
            setErrorMessage(messageOf(error));
          }
        };

        if (loading) return <Box sx={{ p: 3 }}><CircularProgress aria-label="Loading receipt" /></Box>;
        if (!summary) return <Box sx={{ p: 3 }}><Alert severity="error">{errorMessage ?? 'Receipt was not found.'}</Alert></Box>;

        return (
          <Box sx={{ p: 3 }}>
            <Stack spacing={3}>
              {errorMessage && <Alert severity="error" role="alert">{errorMessage}</Alert>}
              <ReceiptControlStatus summary={summary} onEdit={() => setEditing(true)} onCancel={() => setCancelOpen(true)} onReprint={reprint} />
              {summary.receiptStatus === 'Cancelled' && <Alert severity="info">Ordinary print is unavailable after cancellation. The original Document Number remains retired.</Alert>}
              {summary.accountingCorrectionInstruction && <Alert severity="info">{summary.accountingCorrectionInstruction}</Alert>}
              {editing && <Stack component="section" aria-label="Edit receipt" spacing={1}>
                <Typography variant="h6">Edit Receipt</Typography>
                <Typography>Document Number: {summary.documentNumber}</Typography>
                <TextField label="Remarks" value={remarks} onChange={(event) => setRemarks(event.target.value)} fullWidth />
                <Stack direction="row" spacing={1}><Button onClick={() => setEditing(false)}>Close</Button><Button variant="contained" onClick={saveEdit} disabled={!remarks.trim()}>Save</Button></Stack>
              </Stack>}
              <Box component="section" aria-label="Receipt audit">
                <Typography variant="h6">Audit and Retirement</Typography>
                {audit.length === 0 ? <Typography color="text.secondary">No control audit entries.</Typography> : audit.map((entry) => <Typography key={entry.id} data-testid="audit-entry">{entry.operation}: {entry.documentNumber} by {entry.actor} at {entry.occurredAtUtc}{entry.retirementRecordId ? '; Document Number retired' : ''}</Typography>)}
              </Box>
              <DuplicateReceiptPreview output={output} />
            </Stack>
            <ReceiptCancellationDialog open={cancelOpen} summary={summary} onClose={() => setCancelOpen(false)} onCancelled={cancelSucceeded} onError={(error) => setErrorMessage(messageOf(error))} />
          </Box>
        );
      }
      ```
    - The page must not clear or replace `summary`, `audit`, `output`, status, or duplication count inside a failed operation. It must show exact server duplicate markers, retain English text, and never add Redux, RxJS, RTL, or a second axios instance.

  - [ ] 11.5 MODIFY `E:\Finnova\Finnova-UI\Finnova-UI\src\App.tsx`
    - Add the import beside the existing page imports:
      ```typescript
      import ReceiptControl from './pages/ReceiptControl';
      ```
    - Add these routes inside the existing protected `Layout` route:
      ```tsx
      <Route path="/receipt-controls" element={<ReceiptControl />} />
      <Route path="/receipt-controls/:receiptId" element={<ReceiptControl />} />
      ```

  - [ ] 11.6 MODIFY `E:\Finnova\Finnova-UI\Finnova-UI\src\components\Layout.tsx`
    - Find the Finance group's placeholder item:
      ```tsx
      { text: 'Transactions', icon: <ReceiptLongIcon />, path: '/dashboard', isPlaceholder: true },
      ```
    - Insert immediately after it:
      ```tsx
      { text: 'Receipt Controls', icon: <ReceiptLongIcon />, path: '/receipt-controls' },
      ```
    - Keep the label English-only and use the existing MUI navigation structure.

- [ ] 12. Add frontend Vitest/RTL tests
  - [ ] 12.1 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\pages\ReceiptControl.test.tsx`
    - Create this complete Vitest/RTL page test file:
      ```tsx
      import { beforeEach, describe, expect, it, vi } from 'vitest';
      import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
      import userEvent from '@testing-library/user-event';
      import { MemoryRouter, Route, Routes } from 'react-router-dom';
      import type { ReceiptControlSummary } from '../models';
      import ReceiptControl from './ReceiptControl';

      const service = {
        getSummary: vi.fn(), getAudit: vi.fn(), modify: vi.fn(), cancel: vi.fn(), reprint: vi.fn(),
      };
      vi.mock('../services', () => ({ receiptControlService: service }));

      const summary = (overrides: Partial<ReceiptControlSummary> = {}): ReceiptControlSummary => ({
        receiptId: 'receipt-pending', documentNumber: 'RCT-302', receiptStatus: 'Pending Authorization', authorizationStatus: 'Pending',
        isChallaned: false, challanReference: null, isPrinted: false, duplicationCount: 0, receiptVersion: 'AQ==',
        collectionBranchId: 'branch-1', collectionBranchCode: 'BR-001', availableActions: ['Modify', 'Cancel'], canModify: true, canCancel: true,
        canReprint: false, requiresAccountingReversal: false, accountingCorrectionInstruction: null, ...overrides,
      });

      const renderPage = () => render(<MemoryRouter initialEntries={['/receipt-controls/receipt-pending']}><Routes><Route path="/receipt-controls/:receiptId" element={<ReceiptControl />} /></Routes></MemoryRouter>);
      const apiError = (status: number, message: string) => ({ response: { status, data: { code: `ERR-RCC-${status}`, message } } });

      beforeEach(() => {
        vi.clearAllMocks();
        service.getSummary.mockResolvedValue(summary());
        service.getAudit.mockResolvedValue([]);
      });

      describe('ReceiptControl', () => {
        it('shows REC-205 challan warning and disables Edit and Cancel', async () => {
          service.getSummary.mockResolvedValue(summary({ receiptId: 'receipt-challaned', documentNumber: 'RCT-205', isChallaned: true, challanReference: 'CH-205', canModify: false, canCancel: false, availableActions: [] }));
          renderPage();
          expect(await screen.findByText(/linked to a challan/i)).toBeInTheDocument();
          expect(screen.getByRole('button', { name: /edit receipt/i })).toBeDisabled();
          expect(screen.getByRole('button', { name: /cancel receipt/i })).toBeDisabled();
        });

        it('shows authorized warning and disables direct actions', async () => {
          service.getSummary.mockResolvedValue(summary({ receiptStatus: 'Authorized', authorizationStatus: 'Authorized', canModify: false, canCancel: false, accountingCorrectionInstruction: 'Use the approved Cheque Return or JV reversal process.', availableActions: [] }));
          renderPage();
          expect(await screen.findByText(/approved Cheque Return or JV reversal process/i)).toBeInTheDocument();
          expect(screen.getByRole('button', { name: /edit receipt/i })).toBeDisabled();
          expect(screen.getByRole('button', { name: /cancel receipt/i })).toBeDisabled();
        });

        it('requires a reason, cancels REC-302, preserves the document number, and refreshes audit', async () => {
          const cancelled = summary({ receiptStatus: 'Cancelled', canModify: false, canCancel: false, availableActions: [] });
          service.cancel.mockResolvedValue(cancelled);
          service.getAudit.mockResolvedValueOnce([]).mockResolvedValueOnce([{ id: 'audit-1', receiptId: 'receipt-pending', operation: 'Cancel', documentNumber: 'RCT-302', actor: 'mock-supervisor', priorStatus: 'Pending Authorization', newStatus: 'Cancelled', cancellationReason: 'REC-302 correction', retirementRecordId: 'retirement-1', reversalRequestId: null, duplicateCopyNumber: null, occurredAtUtc: '2026-01-02T00:00:00Z' }]);
          const user = userEvent.setup();
          renderPage();
          await user.click(await screen.findByRole('button', { name: /cancel receipt/i }));
          const dialog = await screen.findByRole('dialog', { name: /cancel receipt/i });
          expect(within(dialog).getByRole('button', { name: /cancel receipt/i })).toBeDisabled();
          await user.type(within(dialog).getByLabelText(/cancellation reason/i), ' REC-302 correction ');
          await user.click(within(dialog).getByRole('button', { name: /cancel receipt/i }));
          await waitFor(() => expect(service.cancel).toHaveBeenCalledWith('receipt-pending', expect.objectContaining({ expectedReceiptVersion: 'AQ==', cancellationReason: 'REC-302 correction', idempotencyKey: expect.any(String) })));
          expect(await screen.findByText(/Document Number: RCT-302/)).toBeInTheDocument();
          expect(await screen.findByText(/Document Number retired/)).toBeInTheDocument();
          expect(await screen.findByText(/Ordinary print is unavailable after cancellation/i)).toBeInTheDocument();
        });

        it('renders the first REC-101 duplicate with exact markers and copy number one', async () => {
          service.getSummary.mockResolvedValue(summary({ receiptId: 'receipt-printed', documentNumber: 'RCT-101', isPrinted: true, canModify: false, canCancel: false, canReprint: true, availableActions: ['Reprint'] }));
          service.reprint.mockResolvedValue({ receiptId: 'receipt-printed', documentNumber: 'RCT-101', outputTitle: 'DUPLICATE RECEIPT', watermarkText: 'DUPLICATE RECEIPT', duplicateCopyNumber: 1, duplicationCount: 1, printedAtUtc: '2026-01-02T00:00:00Z', outputReference: 'mock://duplicate/1' });
          const user = userEvent.setup();
          renderPage();
          await user.click(await screen.findByRole('button', { name: /reprint receipt/i }));
          expect((await screen.findAllByText('DUPLICATE RECEIPT')).length).toBeGreaterThanOrEqual(2);
          expect(screen.getByText('Duplicate Copy No: 1')).toBeInTheDocument();
          expect(screen.getByLabelText('duplicate watermark')).toHaveTextContent('DUPLICATE RECEIPT');
          expect(service.reprint).toHaveBeenCalledWith('receipt-printed', expect.any(String));
        });

        it('renders later sequential duplicate count returned by the server', async () => {
          service.getSummary.mockResolvedValue(summary({ receiptId: 'receipt-printed', documentNumber: 'RCT-101', isPrinted: true, canModify: false, canCancel: false, canReprint: true, duplicationCount: 1, availableActions: ['Reprint'] }));
          service.reprint.mockResolvedValue({ receiptId: 'receipt-printed', documentNumber: 'RCT-101', outputTitle: 'DUPLICATE RECEIPT', watermarkText: 'DUPLICATE RECEIPT', duplicateCopyNumber: 2, duplicationCount: 2, printedAtUtc: '2026-01-02T00:00:00Z', outputReference: 'mock://duplicate/2' });
          const user = userEvent.setup();
          renderPage();
          await user.click(await screen.findByRole('button', { name: /reprint receipt/i }));
          expect(await screen.findByText('Duplicate Copy No: 2')).toBeInTheDocument();
          expect(screen.getByText('Duplication Count: 2')).toBeInTheDocument();
        });

        it.each([400, 403, 409, 422, 503])('preserves displayed state for backend error %s', async (status) => {
          service.modify.mockRejectedValue(apiError(status, `English error ${status}`));
          const user = userEvent.setup();
          renderPage();
          await user.click(await screen.findByRole('button', { name: /edit receipt/i }));
          await user.type(screen.getByLabelText('Remarks'), 'corrected value');
          await user.click(screen.getByRole('button', { name: /^save$/i }));
          expect(await screen.findByText(`English error ${status}`)).toBeInTheDocument();
          expect(screen.getByText('Document Number: RCT-302')).toBeInTheDocument();
          expect(screen.queryByText(/Cancelled/)).not.toBeInTheDocument();
        });

        it('does not offer ordinary print after cancellation', async () => {
          service.getSummary.mockResolvedValue(summary({ receiptStatus: 'Cancelled', canModify: false, canCancel: false, canReprint: false, availableActions: [] }));
          renderPage();
          expect(await screen.findByText(/Ordinary print is unavailable after cancellation/i)).toBeInTheDocument();
          expect(screen.queryByRole('button', { name: /ordinary print/i })).not.toBeInTheDocument();
        });
      });
      ```
    - The tests must retain exact REC-205, REC-302, and REC-101 assertions, cover 400/403/409/422/503 error preservation, assert fresh idempotency keys, and assert no ordinary print after cancellation.

  - [ ] 12.2 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\services\receiptControl.service.test.ts`
    - Create this complete Vitest service test file. It verifies the shared axios composition and the mock contract without creating another HTTP client:
      ```typescript
      import { beforeEach, describe, expect, it, vi } from 'vitest';
      import type { ReceiptControlSummary } from '../models';

      const api = vi.hoisted(() => ({ get: vi.fn(), patch: vi.fn(), post: vi.fn() }));
      vi.mock('./api', () => ({ default: api }));

      import { receiptControlRealService } from './real/receiptControl.real';
      import { receiptControlMockService, resetReceiptControlMock } from './mock/receiptControl.mock';

      const pending: ReceiptControlSummary = {
        receiptId: 'receipt-pending', documentNumber: 'RCT-302', receiptStatus: 'Pending Authorization', authorizationStatus: 'Pending', isChallaned: false,
        challanReference: null, isPrinted: false, duplicationCount: 0, receiptVersion: 'AQ==', collectionBranchId: 'branch-1', collectionBranchCode: 'BR-001',
        availableActions: ['Modify', 'Cancel'], canModify: true, canCancel: true, canReprint: false, requiresAccountingReversal: false, accountingCorrectionInstruction: null,
      };

      beforeEach(() => { vi.clearAllMocks(); resetReceiptControlMock(); });

      describe('receiptControlRealService', () => {
        it('uses the shared receipt summary path', async () => {
          api.get.mockResolvedValue({ data: pending });
          await expect(receiptControlRealService.getSummary('receipt-pending')).resolves.toEqual(pending);
          expect(api.get).toHaveBeenCalledWith('/receipts/receipt-pending/control-summary');
        });

        it('sends modify and cancel payloads unchanged to the shared axios instance', async () => {
          api.patch.mockResolvedValue({ data: pending });
          api.post.mockResolvedValue({ data: pending });
          const modify = { expectedReceiptVersion: 'AQ==', idempotencyKey: 'key-1', mutableFields: { Remarks: 'Correction' } };
          const cancel = { expectedReceiptVersion: 'AQ==', idempotencyKey: 'key-2', cancellationReason: 'Correction' };
          await receiptControlRealService.modify('receipt-pending', modify);
          await receiptControlRealService.cancel('receipt-pending', cancel);
          expect(api.patch).toHaveBeenCalledWith('/receipts/receipt-pending/control', modify);
          expect(api.post).toHaveBeenCalledWith('/receipts/receipt-pending/cancel', cancel);
        });

        it('sends Idempotency-Key for reprint and preserves exact output fields', async () => {
          const output = { receiptId: 'receipt-printed', documentNumber: 'RCT-101', outputTitle: 'DUPLICATE RECEIPT', watermarkText: 'DUPLICATE RECEIPT', duplicateCopyNumber: 1, duplicationCount: 1, printedAtUtc: '2026-01-02T00:00:00Z', outputReference: 'receipt://duplicate/1' };
          api.post.mockResolvedValue({ data: output });
          await expect(receiptControlRealService.reprint('receipt-printed', 'key-3')).resolves.toEqual(output);
          expect(api.post).toHaveBeenCalledWith('/receipts/receipt-printed/reprint', undefined, { headers: { 'Idempotency-Key': 'key-3' } });
        });

        it('loads audit through the shared axios instance', async () => {
          api.get.mockResolvedValue({ data: [] });
          await expect(receiptControlRealService.getAudit('receipt-pending')).resolves.toEqual([]);
          expect(api.get).toHaveBeenCalledWith('/receipts/receipt-pending/audit');
        });
      });

      describe('receiptControlMockService', () => {
        it('matches the real contract for cancellation and sequential duplicate output', async () => {
          const cancelled = await receiptControlMockService.cancel('receipt-pending', { expectedReceiptVersion: 'AQ==', idempotencyKey: 'mock-cancel', cancellationReason: 'Correction' });
          expect(cancelled.receiptStatus).toBe('Cancelled');
          expect(cancelled.documentNumber).toBe('RCT-302');
          await expect(receiptControlMockService.reprint('receipt-pending', 'mock-reprint')).rejects.toMatchObject({ response: { status: 409, data: { rule: 'PRINT_NOT_ELIGIBLE' } } });
          const first = await receiptControlMockService.reprint('receipt-printed', 'mock-print-1');
          const second = await receiptControlMockService.reprint('receipt-printed', 'mock-print-2');
          expect(first).toMatchObject({ outputTitle: 'DUPLICATE RECEIPT', watermarkText: 'DUPLICATE RECEIPT', duplicateCopyNumber: 1 });
          expect(second).toMatchObject({ outputTitle: 'DUPLICATE RECEIPT', watermarkText: 'DUPLICATE RECEIPT', duplicateCopyNumber: 2 });
        });

        it('returns a stable error shape and preserves state after a rejected operation', async () => {
          await expect(receiptControlMockService.cancel('receipt-pending', { expectedReceiptVersion: 'AQ==', idempotencyKey: 'mock-invalid', cancellationReason: '   ' })).rejects.toMatchObject({ response: { status: 400, data: { code: 'ERR-RCC-400', message: 'Cancellation Reason is required.' } } });
          await expect(receiptControlMockService.getSummary('receipt-pending')).resolves.toMatchObject({ receiptStatus: 'Pending Authorization', duplicationCount: 0, documentNumber: 'RCT-302' });
        });

        it('replays an equivalent idempotency key and rejects a different payload', async () => {
          const request = { expectedReceiptVersion: 'AQ==', idempotencyKey: 'mock-idempotent', cancellationReason: 'Correction' };
          const first = await receiptControlMockService.cancel('receipt-pending', request);
          const replay = await receiptControlMockService.cancel('receipt-pending', request);
          expect(replay).toEqual(first);
          await expect(receiptControlMockService.cancel('receipt-pending', { ...request, cancellationReason: 'Different' })).rejects.toMatchObject({ response: { status: 409, data: { rule: 'IDEMPOTENCY_CONFLICT' } } });
        });
      });
      ```
    - The tests must preserve exact URL, body, header, error, and output assertions, exercise both mock and real implementations, and verify that failed operations do not alter the mock summary or duplication count.

  - [ ] 12.3 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\models\receiptControl.model.test.ts`
    - Create this complete model/output contract test file:
      ```typescript
      import { describe, expect, it } from 'vitest';
      import type { DuplicateReceiptOutput, ReceiptControlSummary } from './receiptControl.model';

      const formatInr = (amount: number): string => new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(amount);

      const summary: ReceiptControlSummary = {
        receiptId: 'receipt-pending', documentNumber: 'RCT-302', receiptStatus: 'Pending Authorization', authorizationStatus: 'Pending', isChallaned: false,
        challanReference: null, isPrinted: false, duplicationCount: 0, receiptVersion: 'AQ==', collectionBranchId: 'branch-1', collectionBranchCode: 'BR-001',
        availableActions: ['Modify', 'Cancel'], canModify: true, canCancel: true, canReprint: false, requiresAccountingReversal: false, accountingCorrectionInstruction: null,
      };

      describe('receipt control model', () => {
        it('formats all monetary examples as Indian Rupees with two fractional digits', () => {
          expect(formatInr(1250)).toBe('₹1,250.00');
          expect(formatInr(0)).toBe('₹0.00');
          expect(formatInr(12.5)).toBe('₹12.50');
        });

        it('keeps exact English duplicate markers and starts copy numbering at one', () => {
          const output: DuplicateReceiptOutput = { receiptId: 'receipt-printed', documentNumber: 'RCT-101', outputTitle: 'DUPLICATE RECEIPT', watermarkText: 'DUPLICATE RECEIPT', duplicateCopyNumber: 1, duplicationCount: 1, printedAtUtc: '2026-01-02T00:00:00Z', outputReference: 'receipt://duplicate/1' };
          expect(output.outputTitle).toBe('DUPLICATE RECEIPT');
          expect(output.watermarkText).toBe('DUPLICATE RECEIPT');
          expect(output.duplicateCopyNumber).toBe(1);
          expect(output.duplicationCount).toBe(1);
          expect(output.documentNumber).toBe('RCT-101');
        });

        it('contains only the English contract fields and no RTL or non-English localization fields', () => {
          const keys = Object.keys(summary);
          expect(keys).not.toContain('dir');
          expect(keys).not.toContain('rtl');
          expect(keys).not.toContain('arabic');
          expect(keys).not.toContain('valueAr');
          expect(keys).not.toContain('valueEn');
          expect(summary.receiptStatus).toMatch(/^[A-Za-z ]+$/);
          expect(summary.authorizationStatus).toMatch(/^[A-Za-z ]+$/);
        });
      });
      ```
    - Keep the INR formatter deterministic with two fractional digits, assert the exact duplicate title/watermark and first copy number, and assert that no RTL, Arabic, bilingual, or translation fields enter the feature contract.

- [ ] 13. Record later verification commands without executing them
  - [ ] 13.1 MODIFY `E:\Finnova\Finnova-API\.kiro\specs\receipt-processing-controls\tasks.md`
    - After all implementation tasks are applied, the user may run the following commands from the appropriate repository roots. Do not run them during this workflow:
      ```powershell
      dotnet ef migrations add AddReceiptProcessingControls --project Finnova.Repository --startup-project Finnova.AccountsService --output-dir Migrations
      dotnet build Finnova.Backend.slnx
      dotnet test Finnova.Backend.slnx
      npm test -- --run
      npm run build
      ```
    - Before verification, confirm the FINNOVA-61/63 compatibility gate, run the generated migration through the approved change process, and verify that the final source still has one Receipt Aggregate and one authoritative accounting boundary.

## Notes

- Tasks marked with `GATE` must stop when the authoritative FINNOVA-61 or FINNOVA-63 contract is missing or incompatible.
- The generic DCN module does not retire receipt numbers; the new tombstone and issuance adapter must enforce both online and manual reuse prevention.
- The generic JVE inbox does not automatically provide receipt reversal behavior; the confirmed FINNOVA-63/JV/Cheque Return adapter must accept the explicit reversal request and return a durable identity.
- The generic `DocumentAuditEntry`, `ChallanRule`, and demand PDF renderer do not satisfy receipt-specific behavior.
- Tests must cover REC-205, REC-302, REC-101 duplicate printing, authorization/scope, stale versions, idempotency, rollback, accounting rejection, concurrent reprint, gateway authentication, UI error preservation, and exact duplicate markers.
- No source files, builds, migrations, or tests are changed or run by this specification workflow.

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["2.1", "2.2", "2.3"] },
    { "id": 2, "tasks": ["2.4", "3.1"] },
    { "id": 3, "tasks": ["4.1", "4.2", "4.3"] },
    { "id": 4, "tasks": ["5.1", "5.2", "5.3", "5.4"] },
    { "id": 5, "tasks": ["6.1", "6.2", "6.3", "6.4"] },
    { "id": 6, "tasks": ["7.1", "7.2", "7.3", "7.4"] },
    { "id": 7, "tasks": ["8.1", "8.2", "8.3", "8.4"] },
    { "id": 8, "tasks": ["9.1", "9.2", "9.3"] },
    { "id": 9, "tasks": ["10.1", "10.2", "10.3", "10.4", "10.5", "10.6", "10.7", "10.8"] },
    { "id": 10, "tasks": ["11.1", "11.2", "11.3", "11.4", "11.5", "11.6"] },
    { "id": 11, "tasks": ["12.1", "12.2", "12.3"] },
    { "id": 12, "tasks": ["13.1"] }
  ]
}
```
