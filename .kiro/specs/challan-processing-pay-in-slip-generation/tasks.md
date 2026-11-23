# Implementation Plan: FINNOVA-66 Challan Processing and Pay-In-Slip Generation

**Source documents:** `requirements.md`, `design.md`  
**Implementation languages:** C#/.NET, SQL Server/EF Core, TypeScript/React 18/MUI.  
**Execution rule:** This file is a copy-paste implementation script. Apply one task at a time in order. Do not run migrations, builds, or tests as part of spec delivery. Tasks marked `*` are optional tests in the workflow, but remain in the dependency graph.

## Overview

This plan creates the FINNOVA-66 server-authoritative challan workflow and connects it to the existing JVE submission boundary. The backend owns Challan selection and receipt locking; JVE owns Cashflow/GL validation and canonical journal posting. Apply the compatibility gates before copying implementation tasks.

## Mandatory compatibility gates

Complete these checks before copying Task 1. Do not invent a parallel contract when a gate identifies an existing owner.

- Confirm FINNOVA-61 Receipt entity, query owner, ID, date, amount, receipt type, instrument type, customer/entity fields, active/cancelled/bounced/challaned fields, and date timezone.
- Confirm FINNOVA-63 Cashier authorization/posting owner and whether Challan/PostingStatus already exists. Extend the existing owner rather than creating duplicate status fields.
- Confirm FINNOVA-64 lock/edit-protection method and transaction participation. The generation service must use that boundary.
- Confirm FINNOVA-65 `IChallanRoutingResolver` and snapshot fields, including the deposit-bank clearing GL reference. Use the returned snapshot unchanged after resolution.
- Confirm the existing `Finnova.Models.Contracts.JVPosting.JvPostingSubmissionRequest`, `JvPostingSubmissionService`, durable request status, and canonical JVE event/rule key. The default key is `Collections.ChallanPayInSlip` until confirmed.
- Confirm whether Document Number Control allocates Challan_No. Use that owner if present; otherwise obtain an approved server-side sequence boundary before copying the number adapter.
- Confirm the controller host. The default paths below use `Finnova.AccountsService` only if no existing receipt/Collections host owns the endpoint.
- Confirm the shared UI service paths and route composition. Use `src/services/api.ts`, the existing interface/real/mock/toggle pattern, `App.tsx`, `Layout.tsx`, and `LocationMaster.tsx` as references.
- Use only India/English fixtures: `LN - Loan`, `Mumbai`, `HDFC Bank`, `SBI`, `Cash`, `Cheque`, `PDC`, `Local`, `Outstation`, `DD`, and INR. Legacy Oman/Muscat/Arabic/OMR values are visual references only and must not enter code or tests.
- FINNOVA-66 must not add a Journal Header, Journal Line, canonical source-link repository, Cashflow rule table, GL mapping table, Receipt aggregate, or JVE worker. The only accounting write is the approved JVE Posting Request.

## Tasks

- [ ] 1. Create backend domain models, statuses, and typed exceptions
  - [ ] 1.1 CREATE `Finnova.Models/Domain/Enums/ChallanProcessingEnums.cs`
    ```csharp
    namespace Finnova.Models.Domain.Enums;

    public enum ChallanStatus
    {
        Draft = 0,
        PostingPending = 1,
        Approved = 2,
        PostingFailed = 3
    }

    public enum ChallanPostingStatus
    {
        Ready = 0,
        Processing = 1,
        Posted = 2,
        Failed = 3,
        DeadLetter = 4
    }

    public enum ChallanReceiptTypeFilter
    {
        All = 0,
        Cash = 1,
        Cheque = 2,
        Pdc = 3
    }

    public enum ChallanInstrumentTypeFilter
    {
        All = 0,
        Local = 1,
        Outstation = 2,
        Dd = 3
    }
    ```
    _Requirements: 3.3, 3.4, 7.4, 8.1, 8.2, 11.1_

  - [ ] 1.2 CREATE `Finnova.Models/Domain/Entities/ChallanProcessingEntities.cs`
    ```csharp
    using Finnova.Models.Domain.Enums;

    namespace Finnova.Models.Domain.Entities;

    public sealed class Challan
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string ChallanNumber { get; set; } = string.Empty;
        public Guid LineOfBusinessId { get; set; }
        public Guid BranchId { get; set; }
        public Guid DepositBankId { get; set; }
        public string DepositBankNameSnapshot { get; set; } = string.Empty;
        public DateOnly ChallanDate { get; set; }
        public decimal TotalAmountInr { get; set; }
        public int SelectedReceiptCount { get; set; }
        public ChallanStatus Status { get; set; } = ChallanStatus.Draft;
        public ChallanPostingStatus PostingStatus { get; set; } = ChallanPostingStatus.Ready;
        public Guid? JveRequestId { get; set; }
        public Guid? JournalId { get; set; }
        public string? PostingFailureCode { get; set; }
        public string? PostingFailureMessage { get; set; }
        public string CreatedByUserId { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
        public ICollection<ChallanReceipt> Receipts { get; set; } = new List<ChallanReceipt>();
    }

    public sealed class ChallanReceipt
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ChallanId { get; set; }
        public Guid ReceiptId { get; set; }
        public decimal AmountInr { get; set; }
        public DateOnly InstrumentDate { get; set; }
        public string ReceiptNumberSnapshot { get; set; } = string.Empty;
        public string EntityCustomerCodeSnapshot { get; set; } = string.Empty;
        public string EntityCustomerNameSnapshot { get; set; } = string.Empty;
        public string? DraweeBankSnapshot { get; set; }
        public string LocationSnapshot { get; set; } = string.Empty;
        public RoutingSnapshot RoutingSnapshot { get; set; } = null!;
    }

    public sealed class RoutingSnapshot
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ChallanReceiptId { get; set; }
        public Guid RuleSetId { get; set; }
        public long RuleVersion { get; set; }
        public string MatchedDraweeBankKey { get; set; } = string.Empty;
        public bool UsedOthers { get; set; }
        public Guid DepositBankAccountId { get; set; }
        public string DepositBankCode { get; set; } = string.Empty;
        public string DepositBankName { get; set; } = string.Empty;
        public string AccountNumberSnapshot { get; set; } = string.Empty;
        public string? DraweeBankClearingGlReference { get; set; }
        public DateTime ResolvedAtUtc { get; set; }
        public string CorrelationId { get; set; } = string.Empty;
    }

    public sealed class ChallanAuditEntry
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ChallanId { get; set; }
        public Guid? ReceiptId { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string ActorId { get; set; } = string.Empty;
        public string CorrelationId { get; set; } = string.Empty;
        public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
        public string? SafeDetailsJson { get; set; }
    }

    public sealed class ChallanGenerationIdempotency
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Operation { get; set; } = "Generate";
        public string Key { get; set; } = string.Empty;
        public string Fingerprint { get; set; } = string.Empty;
        public Guid ChallanId { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
    ```
    _Requirements: 5.2–5.8, 6.2, 7.2–7.6, 8.2, 10.5_

  - [ ] 1.3 CREATE `Finnova.Models/Domain/Exceptions/ChallanProcessingExceptions.cs`
    ```csharp
    namespace Finnova.Models.Domain.Exceptions;

    public abstract class ChallanProcessingException : Exception
    {
        protected ChallanProcessingException(string message, string code) : base(message) => Code = code;
        public string Code { get; }
    }

    public sealed class ChallanProcessingValidationException(string message)
        : ChallanProcessingException(message, "ERR-CHL-400");

    public sealed class ChallanProcessingNotFoundException(string message)
        : ChallanProcessingException(message, "ERR-CHL-404");

    public sealed class ChallanProcessingAuthorizationException(string message)
        : ChallanProcessingException(message, "ERR-CHL-403");

    public sealed class ChallanProcessingConflictException(string message)
        : ChallanProcessingException(message, "ERR-CHL-409");

    public sealed class ChallanProcessingConfigurationException(string message)
        : ChallanProcessingException(message, "ERR-CHL-422");

    public sealed class ChallanProcessingDependencyException(string message)
        : ChallanProcessingException(message, "ERR-CHL-503");
    ```
    _Requirements: 2.4, 5.5, 6.3, 7.7, 8.5, 9.4, 10.1–10.2_

- [ ] 2. Create backend contracts and adapter ports
  - [ ] 2.1 CREATE `Finnova.Models/Contracts/ChallanProcessing/ChallanProcessingContracts.cs`
    ```csharp
    using Finnova.Models.Domain.Enums;

    namespace Finnova.Models.Contracts.ChallanProcessing;

    public sealed record ChallanListingRequest(
        Guid LineOfBusinessId,
        Guid BranchId,
        DateOnly ChallanDate,
        Guid DepositBankId,
        string ReceiptType,
        string InstrumentType,
        bool PastReceipts,
        int Page,
        int PageSize);

    public sealed record EligibleReceiptRow(
        Guid ReceiptId,
        DateOnly InstrumentDate,
        string ReceiptNumber,
        string EntityCustomerCode,
        string EntityCustomerName,
        string? DraweeBank,
        string Location,
        decimal AmountInr,
        string ReceiptType,
        string? InstrumentType,
        bool IsEligible);

    public sealed record ChallanListingResponse(
        IReadOnlyList<EligibleReceiptRow> Items,
        int Page,
        int PageSize,
        int TotalCount,
        string CorrelationId);

    public sealed record GenerateChallanRequest(
        Guid LineOfBusinessId,
        Guid BranchId,
        DateOnly ChallanDate,
        Guid DepositBankId,
        string ReceiptType,
        string InstrumentType,
        bool PastReceipts,
        IReadOnlyList<Guid> ReceiptIds,
        string IdempotencyKey);

    public sealed record GenerateChallanResponse(
        Guid ChallanId,
        string ChallanNumber,
        int SelectedReceiptCount,
        decimal TotalAmountInr,
        string ChallanStatus,
        string PostingStatus,
        Guid? JveRequestId,
        Guid? JournalId,
        string CorrelationId);

    public sealed record ChallanStatusResponse(
        Guid ChallanId,
        string ChallanNumber,
        string ChallanStatus,
        string PostingStatus,
        Guid? JveRequestId,
        Guid? JournalId,
        string? PostingFailureCode,
        string? PostingFailureMessage,
        DateTime UpdatedAtUtc);

    public sealed record LineOfBusinessOption(Guid Id, string DisplayName);
    public sealed record BranchOption(Guid Id, string DisplayName);
    public sealed record DepositBankOption(Guid Id, string DisplayName);

    public sealed record ChallanRoutingResolveRequest(
        Guid LineOfBusinessId,
        Guid BranchId,
        Guid DepositBankId,
        string? DraweeBankKey,
        DateOnly EvaluationDate,
        string CorrelationId);

    public sealed record ChallanRoutingResolveResponse(
        Guid RuleSetId,
        long RuleVersion,
        string MatchedDraweeBankKey,
        bool UsedOthers,
        Guid DepositBankAccountId,
        string DepositBankCode,
        string DepositBankName,
        string AccountNumberSnapshot,
        string? DraweeBankClearingGlReference,
        DateTime ResolvedAtUtc,
        string CorrelationId);
    ```
    _Requirements: 2.5, 3.7–3.8, 4.6, 6.1–6.5, 7.1–7.6, 11.1–11.4_

  - [ ] 2.2 CREATE `Finnova.Service/ChallanProcessing/Ports/ChallanProcessingPorts.cs`
    ```csharp
    using System.Security.Claims;
    using Finnova.Models.Contracts.ChallanProcessing;
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;

    namespace Finnova.Service.ChallanProcessing.Ports;

    public sealed record ReceiptForChallan(
        Guid ReceiptId,
        DateOnly InstrumentDate,
        DateOnly CreatedDate,
        string ReceiptNumber,
        string EntityCustomerCode,
        string EntityCustomerName,
        string? DraweeBank,
        string Location,
        decimal AmountInr,
        string ReceiptType,
        string? InstrumentType,
        bool IsActive,
        bool IsCancelled,
        bool IsBounced,
        bool IsChallaned);

    public interface IReceiptEligibilityReader
    {
        Task<(IReadOnlyList<ReceiptForChallan> Items, int Total)> ListAsync(ChallanListingRequest request, CancellationToken ct);
        Task<IReadOnlyList<ReceiptForChallan>> GetForGenerationAsync(IReadOnlyCollection<Guid> receiptIds, ChallanListingRequest context, CancellationToken ct);
    }

    public interface IReceiptLockBoundary
    {
        Task MarkChallanedAsync(IReadOnlyCollection<Guid> receiptIds, Guid challanId, CancellationToken ct);
    }

    public interface IChallanRoutingResolver
    {
        Task<ChallanRoutingResolveResponse> ResolveAsync(ChallanRoutingResolveRequest request, CancellationToken ct);
    }

    public interface IChallanNumberSource
    {
        Task<string> AllocateAsync(Guid branchId, DateOnly challanDate, CancellationToken ct);
    }

    public interface ICompanyContext
    {
        Task<Guid> GetCompanyIdAsync(Guid branchId, CancellationToken ct);
    }

    public interface IJveStatusReader
    {
        Task<(ChallanPostingStatus Status, Guid? JournalId, string? FailureCode, string? FailureMessage)> GetAsync(Guid requestId, CancellationToken ct);
    }

    public sealed record ChallanPostingPayload(
        Challan Challan,
        IReadOnlyList<ChallanReceipt> Receipts,
        string RuleKey,
        ClaimsPrincipal Caller);

    public sealed record JveAcceptedResult(Guid RequestId, string Status, bool ExistingEquivalentRequest);

    public interface IJveChallanSubmissionAdapter
    {
        Task<JveAcceptedResult> SubmitApprovedAsync(ChallanPostingPayload payload, CancellationToken ct);
        Task<(ChallanPostingStatus Status, Guid? JournalId, string? FailureCode, string? FailureMessage)> GetStatusAsync(Guid requestId, CancellationToken ct);
    }

    public interface IChallanRepository
    {
        Task<Challan?> GetAsync(Guid id, CancellationToken ct);
        Task<ChallanGenerationIdempotency?> FindIdempotencyAsync(string key, CancellationToken ct);
        Task<Challan?> GetByIdempotencyResultAsync(Guid challanId, CancellationToken ct);
        Task CommitGenerationAsync(Challan challan, ChallanGenerationIdempotency idempotency, CancellationToken ct);
        Task MarkJveAcceptedAsync(Guid challanId, Guid requestId, string status, CancellationToken ct);
        Task MarkJveFailedAsync(Guid challanId, string code, string message, CancellationToken ct);
        Task SetPostingResultAsync(Guid challanId, ChallanPostingStatus status, Guid? journalId, string? code, string? message, CancellationToken ct);
    }

    public interface IChallanAuditWriter
    {
        Task AppendAsync(ChallanAuditEntry entry, CancellationToken ct);
    }
    ```
    _Requirements: 5.1–5.8, 6.1–6.5, 7.1–7.7, 8.3–8.8_

- [ ] 3. Create pure eligibility, total, pagination, lifecycle, and idempotency helpers
  - [ ] 3.1 CREATE `Finnova.Service/ChallanProcessing/Domain/ChallanRules.cs`
    ```csharp
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using Finnova.Models.Contracts.ChallanProcessing;
    using Finnova.Models.Domain.Enums;
    using Finnova.Service.ChallanProcessing.Ports;

    namespace Finnova.Service.ChallanProcessing.Domain;

    public static class ChallanRules
    {
        public static bool IsEligible(ReceiptForChallan receipt, ChallanListingRequest request)
        {
            var receiptTypeMatches = string.Equals(request.ReceiptType, "All", StringComparison.OrdinalIgnoreCase)
                || string.Equals(receipt.ReceiptType, request.ReceiptType, StringComparison.OrdinalIgnoreCase);
            var instrumentMatches = string.Equals(request.InstrumentType, "All", StringComparison.OrdinalIgnoreCase)
                || string.Equals(receipt.InstrumentType, request.InstrumentType, StringComparison.OrdinalIgnoreCase);
            var dateMatches = request.PastReceipts
                ? receipt.CreatedDate < request.ChallanDate
                : receipt.CreatedDate <= request.ChallanDate;
            return receipt.IsActive
                && !receipt.IsCancelled
                && !receipt.IsBounced
                && !receipt.IsChallaned
                && receiptTypeMatches
                && instrumentMatches
                && dateMatches;
        }

        public static IReadOnlyList<ReceiptForChallan> OrderAndPage(
            IEnumerable<ReceiptForChallan> receipts,
            ChallanListingRequest request)
        {
            var page = request.Page < 1 ? 1 : request.Page;
            var size = request.PageSize is < 1 or > 100 ? 10 : request.PageSize;
            return receipts
                .OrderBy(x => x.InstrumentDate)
                .ThenBy(x => x.ReceiptNumber)
                .ThenBy(x => x.ReceiptId)
                .Skip((page - 1) * size)
                .Take(size)
                .ToArray();
        }

        public static decimal CalculateTotal(IEnumerable<ReceiptForChallan> selected)
            => decimal.Round(selected.Sum(x => x.AmountInr), 2, MidpointRounding.ToEven);

        public static void EnsureValidSelection(IReadOnlyCollection<Guid> receiptIds)
        {
            if (receiptIds.Count == 0)
                throw new InvalidOperationException("ERR-CHL-NO-SELECTION");
            if (receiptIds.Distinct().Count() != receiptIds.Count)
                throw new InvalidOperationException("ERR-CHL-DUPLICATE-RECEIPT");
        }

        public static void EnsureTransition(ChallanStatus current, ChallanStatus target)
        {
            var allowed = current switch
            {
                ChallanStatus.Draft => target == ChallanStatus.PostingPending,
                ChallanStatus.PostingPending => target is ChallanStatus.Approved or ChallanStatus.PostingFailed,
                ChallanStatus.Approved => target == ChallanStatus.Approved,
                ChallanStatus.PostingFailed => target is ChallanStatus.PostingFailed or ChallanStatus.Approved,
                _ => false
            };
            if (!allowed)
                throw new InvalidOperationException($"ERR-CHL-INVALID-STATUS:{current}:{target}");
        }

        public static string Fingerprint(GenerateChallanRequest request)
        {
            var canonical = JsonSerializer.Serialize(new
            {
                request.LineOfBusinessId,
                request.BranchId,
                request.ChallanDate,
                request.DepositBankId,
                request.ReceiptType,
                request.InstrumentType,
                request.PastReceipts,
                ReceiptIds = request.ReceiptIds.OrderBy(x => x).ToArray()
            });
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        }
    }
    ```
    _Requirements: 3.1–3.8, 4.4–4.7, 5.1, 8.2, 8.7–8.8, 12.1_

  - [ ] 3.2 CREATE `Finnova.Service/ChallanProcessing/Domain/ChallanStatusRules.cs`
    ```csharp
    using Finnova.Models.Domain.Enums;

    namespace Finnova.Service.ChallanProcessing.Domain;

    public static class ChallanStatusRules
    {
        public static bool CanPrint(ChallanStatus status, ChallanPostingStatus postingStatus)
            => status == ChallanStatus.Approved
                && postingStatus is ChallanPostingStatus.Ready or ChallanPostingStatus.Processing or ChallanPostingStatus.Posted;

        public static bool IsReceiptLocked(ChallanStatus status)
            => status is ChallanStatus.PostingPending or ChallanStatus.Approved or ChallanStatus.PostingFailed;
    }
    ```
    _Requirements: 8.2–8.5, 9.1–9.4_

- [ ] 4. Add EF Core configuration, DbSets, and the migration command
  - [ ] 4.1 MODIFY `Finnova.Repository/Context/FinnovaDbContext.cs` — add these DbSets beside `JvPostingInboxes`; do not add Receipt, Journal Header, or Journal Line DbSets
    ```csharp
    public DbSet<Challan> Challans => Set<Challan>();
    public DbSet<ChallanReceipt> ChallanReceipts => Set<ChallanReceipt>();
    public DbSet<RoutingSnapshot> RoutingSnapshots => Set<RoutingSnapshot>();
    public DbSet<ChallanAuditEntry> ChallanAuditEntries => Set<ChallanAuditEntry>();
    public DbSet<ChallanGenerationIdempotency> ChallanGenerationIdempotencies => Set<ChallanGenerationIdempotency>();
    ```
    Add `using Finnova.Models.Domain.Entities;` if it is absent. _Requirements: 5.2–5.4, 6.2, 8.7, 10.5_

  - [ ] 4.2 CREATE `Finnova.Repository/Configuration/ChallanProcessingConfiguration.cs`
    ```csharp
    using Finnova.Models.Domain.Entities;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;

    namespace Finnova.Repository.Configuration;

    public sealed class ChallanConfiguration : IEntityTypeConfiguration<Challan>
    {
        public void Configure(EntityTypeBuilder<Challan> builder)
        {
            builder.ToTable("challans");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.ChallanNumber).IsRequired().HasMaxLength(80);
            builder.Property(x => x.DepositBankNameSnapshot).IsRequired().HasMaxLength(200);
            builder.Property(x => x.TotalAmountInr).HasPrecision(18, 2);
            builder.Property(x => x.CreatedByUserId).IsRequired().HasMaxLength(200);
            builder.Property(x => x.RowVersion).IsRowVersion();
            builder.HasIndex(x => x.ChallanNumber).IsUnique();
            builder.HasMany(x => x.Receipts).WithOne().HasForeignKey(x => x.ChallanId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    public sealed class ChallanReceiptConfiguration : IEntityTypeConfiguration<ChallanReceipt>
    {
        public void Configure(EntityTypeBuilder<ChallanReceipt> builder)
        {
            builder.ToTable("challan_receipts");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.AmountInr).HasPrecision(18, 2);
            builder.Property(x => x.ReceiptNumberSnapshot).IsRequired().HasMaxLength(100);
            builder.Property(x => x.EntityCustomerCodeSnapshot).IsRequired().HasMaxLength(100);
            builder.Property(x => x.EntityCustomerNameSnapshot).IsRequired().HasMaxLength(200);
            builder.Property(x => x.LocationSnapshot).IsRequired().HasMaxLength(150);
            builder.HasIndex(x => x.ReceiptId).IsUnique();
            builder.HasOne(x => x.RoutingSnapshot).WithOne().HasForeignKey<RoutingSnapshot>(x => x.ChallanReceiptId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    public sealed class RoutingSnapshotConfiguration : IEntityTypeConfiguration<RoutingSnapshot>
    {
        public void Configure(EntityTypeBuilder<RoutingSnapshot> builder)
        {
            builder.ToTable("challan_routing_snapshots");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.MatchedDraweeBankKey).IsRequired().HasMaxLength(100);
            builder.Property(x => x.DepositBankCode).IsRequired().HasMaxLength(80);
            builder.Property(x => x.DepositBankName).IsRequired().HasMaxLength(200);
            builder.Property(x => x.AccountNumberSnapshot).IsRequired().HasMaxLength(100);
            builder.Property(x => x.DraweeBankClearingGlReference).HasMaxLength(100);
            builder.Property(x => x.CorrelationId).IsRequired().HasMaxLength(100);
            builder.HasIndex(x => x.ChallanReceiptId).IsUnique();
        }
    }

    public sealed class ChallanAuditEntryConfiguration : IEntityTypeConfiguration<ChallanAuditEntry>
    {
        public void Configure(EntityTypeBuilder<ChallanAuditEntry> builder)
        {
            builder.ToTable("challan_audit_entries");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.EventType).IsRequired().HasMaxLength(80);
            builder.Property(x => x.ActorId).IsRequired().HasMaxLength(200);
            builder.Property(x => x.CorrelationId).IsRequired().HasMaxLength(100);
            builder.Property(x => x.SafeDetailsJson).HasColumnType("nvarchar(max)");
            builder.HasIndex(x => new { x.ChallanId, x.OccurredAtUtc, x.Id });
        }
    }

    public sealed class ChallanGenerationIdempotencyConfiguration : IEntityTypeConfiguration<ChallanGenerationIdempotency>
    {
        public void Configure(EntityTypeBuilder<ChallanGenerationIdempotency> builder)
        {
            builder.ToTable("challan_generation_idempotencies");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Operation).IsRequired().HasMaxLength(40);
            builder.Property(x => x.Key).IsRequired().HasMaxLength(120);
            builder.Property(x => x.Fingerprint).IsRequired().HasMaxLength(128);
            builder.HasIndex(x => new { x.Operation, x.Key }).IsUnique();
        }
    }
    ```
    The unique `challan_receipts.receipt_id` index is the database backstop against selecting one receipt twice. Preserve any FINNOVA-63-owned equivalent table instead of creating a duplicate. _Requirements: 5.2–5.8, 8.7–8.8, 10.5_

  - [ ] 4.3 MODIFY `<generated EF migration files>` — generate after Tasks 1–4.2 and all gates pass
    ```powershell
    dotnet ef migrations add AddChallanProcessingPayInSlip --project Finnova.Repository --startup-project Finnova.AccountsService
    ```
    The generated migration must create only feature-owned Challan, membership, RoutingSnapshot, audit, and idempotency tables, unique Challan_No/ReceiptId/idempotency indexes, and concurrency columns. Do not run this command during spec delivery and do not add canonical journal tables. _Requirements: 5.2–5.8, 8.7, 10.5_

- [ ] 5. Create repository, lookup, lock, number, and DI registrations
  - [ ] 5.1 CREATE `Finnova.Repository/Interfaces/IChallanProcessingRepository.cs`
    ```csharp
    using Finnova.Models.Domain.Entities;

    namespace Finnova.Repository.Interfaces;

    public interface IChallanProcessingRepository
    {
        Task<Challan?> GetAsync(Guid id, CancellationToken ct);
        Task<ChallanGenerationIdempotency?> FindIdempotencyAsync(string key, CancellationToken ct);
        Task CommitGenerationAsync(Challan challan, ChallanGenerationIdempotency idempotency, CancellationToken ct);
        Task MarkJveAcceptedAsync(Guid challanId, Guid requestId, string status, CancellationToken ct);
        Task MarkJveFailedAsync(Guid challanId, string code, string message, CancellationToken ct);
        Task SetPostingResultAsync(Guid challanId, Finnova.Models.Domain.Enums.ChallanPostingStatus status, Guid? journalId, string? code, string? message, CancellationToken ct);
    }
    ```
    _Requirements: 5.2–5.8, 7.4, 8.5–8.8_

  - [ ] 5.2 CREATE `Finnova.Repository/Repositories/ChallanProcessingRepository.cs`
    ```csharp
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;
    using Finnova.Repository.Context;
    using Finnova.Repository.Interfaces;
    using Microsoft.EntityFrameworkCore;

    namespace Finnova.Repository.Repositories;

    public sealed class ChallanProcessingRepository(FinnovaDbContext db) : IChallanProcessingRepository
    {
        public Task<Challan?> GetAsync(Guid id, CancellationToken ct)
            => db.Challans.Include(x => x.Receipts).ThenInclude(x => x.RoutingSnapshot).SingleOrDefaultAsync(x => x.Id == id, ct);

        public Task<ChallanGenerationIdempotency?> FindIdempotencyAsync(string key, CancellationToken ct)
            => db.ChallanGenerationIdempotencies.SingleOrDefaultAsync(x => x.Operation == "Generate" && x.Key == key, ct);

        public async Task CommitGenerationAsync(Challan challan, ChallanGenerationIdempotency idempotency, CancellationToken ct)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            db.Challans.Add(challan);
            db.ChallanGenerationIdempotencies.Add(idempotency);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }

        public async Task MarkJveAcceptedAsync(Guid challanId, Guid requestId, string status, CancellationToken ct)
        {
            var challan = await db.Challans.SingleAsync(x => x.Id == challanId, ct);
            challan.JveRequestId = requestId;
            challan.Status = ChallanStatus.Approved;
            challan.PostingStatus = Enum.TryParse<ChallanPostingStatus>(status, true, out var parsed) ? parsed : ChallanPostingStatus.Ready;
            challan.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        public async Task MarkJveFailedAsync(Guid challanId, string code, string message, CancellationToken ct)
        {
            var challan = await db.Challans.SingleAsync(x => x.Id == challanId, ct);
            challan.Status = ChallanStatus.PostingFailed;
            challan.PostingStatus = ChallanPostingStatus.Failed;
            challan.PostingFailureCode = code;
            challan.PostingFailureMessage = message;
            challan.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        public async Task SetPostingResultAsync(Guid challanId, ChallanPostingStatus status, Guid? journalId, string? code, string? message, CancellationToken ct)
        {
            var challan = await db.Challans.SingleAsync(x => x.Id == challanId, ct);
            challan.PostingStatus = status;
            challan.JournalId = journalId;
            challan.PostingFailureCode = code;
            challan.PostingFailureMessage = message;
            challan.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }
    ```
    Before copying, replace only the `CommitGenerationAsync` transaction internals if FINNOVA-64 exposes a required shared transaction; keep the same atomic behavior and do not add journal writes. _Requirements: 5.2–5.8, 8.5–8.8_

  - [ ] 5.3 MODIFY `Finnova.Repository/DependencyInjection.cs` — add the registration beside existing repository registrations
    ```csharp
    services.AddScoped<IChallanProcessingRepository, ChallanProcessingRepository>();
    ```
    Add the corresponding `using Finnova.Repository.Interfaces;` and `using Finnova.Repository.Repositories;` only when absent. _Requirements: 11.1–11.4_

- [ ] 6. Implement CQRS listing, generation, status, print, and JVE adapter
  - [ ] 6.1 CREATE `Finnova.Service/ChallanProcessing/ChallanProcessingService.cs`
    ```csharp
    using System.Security.Claims;
    using Finnova.Models.Contracts.ChallanProcessing;
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Service.ChallanProcessing.Domain;
    using Finnova.Service.ChallanProcessing.Ports;
    using Finnova.Repository.Interfaces;

    namespace Finnova.Service.ChallanProcessing;

    public sealed class ChallanProcessingService(
        IReceiptEligibilityReader receipts,
        IReceiptLockBoundary receiptLock,
        IChallanRoutingResolver routing,
        IChallanNumberSource numbers,
        IJveChallanSubmissionAdapter jve,
        IChallanProcessingRepository repository,
        IChallanAuditWriter audit)
    {
        public async Task<ChallanListingResponse> ListAsync(ChallanListingRequest request, string correlationId, CancellationToken ct)
        {
            ValidateListing(request);
            var result = await receipts.ListAsync(request, ct);
            var filtered = result.Items.Where(x => ChallanRules.IsEligible(x, request)).ToArray();
            var page = ChallanRules.OrderAndPage(filtered, request);
            var rows = page.Select(x => new EligibleReceiptRow(x.ReceiptId, x.InstrumentDate, x.ReceiptNumber, x.EntityCustomerCode, x.EntityCustomerName, x.DraweeBank, x.Location, x.AmountInr, x.ReceiptType, x.InstrumentType, true)).ToArray();
            return new ChallanListingResponse(rows, request.Page < 1 ? 1 : request.Page, request.PageSize, filtered.Length, correlationId);
        }

        public async Task<GenerateChallanResponse> GenerateAsync(GenerateChallanRequest request, ClaimsPrincipal caller, string correlationId, CancellationToken ct)
        {
            ValidateGeneration(request);
            var fingerprint = ChallanRules.Fingerprint(request);
            var existingKey = await repository.FindIdempotencyAsync(request.IdempotencyKey, ct);
            if (existingKey is not null)
            {
                if (!string.Equals(existingKey.Fingerprint, fingerprint, StringComparison.Ordinal))
                    throw new ChallanProcessingConflictException("The Idempotency_Key is already used for different challan data.");
                var existing = await repository.GetAsync(existingKey.ChallanId, ct) ?? throw new ChallanProcessingNotFoundException("The original Challan was not found.");
                return ToResponse(existing, correlationId);
            }

            var context = new ChallanListingRequest(request.LineOfBusinessId, request.BranchId, request.ChallanDate, request.DepositBankId, request.ReceiptType, request.InstrumentType, request.PastReceipts, 1, 100);
            var selected = await receipts.GetForGenerationAsync(request.ReceiptIds, context, ct);
            var missing = request.ReceiptIds.Except(selected.Select(x => x.ReceiptId)).ToArray();
            var invalid = selected.Where(x => !ChallanRules.IsEligible(x, context)).Select(x => x.ReceiptId).ToArray();
            if (missing.Length > 0 || invalid.Length > 0)
                throw new ChallanProcessingConflictException($"Receipt selection changed before commit: {string.Join(",", missing.Concat(invalid))}.");

            var challanId = Guid.NewGuid();
            var memberships = new List<ChallanReceipt>();
            foreach (var receipt in selected.OrderBy(x => x.ReceiptId))
            {
                var route = await routing.ResolveAsync(new ChallanRoutingResolveRequest(request.LineOfBusinessId, request.BranchId, request.DepositBankId, receipt.DraweeBank, request.ChallanDate, correlationId), ct);
                var membership = new ChallanReceipt
                {
                    ChallanId = challanId,
                    ReceiptId = receipt.ReceiptId,
                    AmountInr = decimal.Round(receipt.AmountInr, 2, MidpointRounding.ToEven),
                    InstrumentDate = receipt.InstrumentDate,
                    ReceiptNumberSnapshot = receipt.ReceiptNumber,
                    EntityCustomerCodeSnapshot = receipt.EntityCustomerCode,
                    EntityCustomerNameSnapshot = receipt.EntityCustomerName,
                    DraweeBankSnapshot = receipt.DraweeBank,
                    LocationSnapshot = receipt.Location,
                    RoutingSnapshot = new RoutingSnapshot
                    {
                        ChallanReceiptId = Guid.NewGuid(),
                        RuleSetId = route.RuleSetId,
                        RuleVersion = route.RuleVersion,
                        MatchedDraweeBankKey = route.MatchedDraweeBankKey,
                        UsedOthers = route.UsedOthers,
                        DepositBankAccountId = route.DepositBankAccountId,
                        DepositBankCode = route.DepositBankCode,
                        DepositBankName = route.DepositBankName,
                        AccountNumberSnapshot = route.AccountNumberSnapshot,
                        DraweeBankClearingGlReference = route.DraweeBankClearingGlReference,
                        ResolvedAtUtc = route.ResolvedAtUtc,
                        CorrelationId = route.CorrelationId
                    }
                };
                memberships.Add(membership);
            }

            var challan = new Challan
            {
                Id = challanId,
                ChallanNumber = await numbers.AllocateAsync(request.BranchId, request.ChallanDate, ct),
                LineOfBusinessId = request.LineOfBusinessId,
                BranchId = request.BranchId,
                DepositBankId = request.DepositBankId,
                DepositBankNameSnapshot = memberships[0].RoutingSnapshot.DepositBankName,
                ChallanDate = request.ChallanDate,
                TotalAmountInr = ChallanRules.CalculateTotal(selected),
                SelectedReceiptCount = memberships.Count,
                Status = ChallanStatus.PostingPending,
                PostingStatus = ChallanPostingStatus.Ready,
                CreatedByUserId = caller.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new ChallanProcessingAuthorizationException("Cashier identity is required."),
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                Receipts = memberships
            };
            await receiptLock.MarkChallanedAsync(request.ReceiptIds, challan.Id, ct);
            await repository.CommitGenerationAsync(challan, new ChallanGenerationIdempotency { Key = request.IdempotencyKey, Fingerprint = fingerprint, ChallanId = challan.Id }, ct);
            await audit.AppendAsync(new ChallanAuditEntry { ChallanId = challan.Id, EventType = "ChallanCommitted", ActorId = challan.CreatedByUserId, CorrelationId = correlationId }, ct);

            try
            {
                var accepted = await jve.SubmitApprovedAsync(new ChallanPostingPayload(challan, memberships, "Collections.ChallanPayInSlip", caller), ct);
                await repository.MarkJveAcceptedAsync(challan.Id, accepted.RequestId, accepted.Status, ct);
                var saved = await repository.GetAsync(challan.Id, ct) ?? challan;
                return ToResponse(saved, correlationId);
            }
            catch (Exception exception)
            {
                await repository.MarkJveFailedAsync(challan.Id, "ERR-CHL-JVE-SUBMISSION", "JVE submission failed; the Challan remains locked for controlled recovery.", ct);
                await audit.AppendAsync(new ChallanAuditEntry { ChallanId = challan.Id, EventType = "JveSubmissionFailed", ActorId = challan.CreatedByUserId, CorrelationId = correlationId, SafeDetailsJson = "{\"code\":\"ERR-CHL-JVE-SUBMISSION\"}" }, ct);
                throw new ChallanProcessingDependencyException($"JVE submission failed. Correlation ID: {correlationId}");
            }
        }

        public async Task<ChallanStatusResponse> GetStatusAsync(Guid challanId, CancellationToken ct)
        {
            var challan = await repository.GetAsync(challanId, ct) ?? throw new ChallanProcessingNotFoundException("Challan was not found.");
            if (challan.JveRequestId is Guid requestId)
            {
                var result = await jve.GetStatusAsync(requestId, ct);
                await repository.SetPostingResultAsync(challanId, result.Status, result.JournalId, result.FailureCode, result.FailureMessage, ct);
                challan.PostingStatus = result.Status;
                challan.JournalId = result.JournalId;
                challan.PostingFailureCode = result.FailureCode;
                challan.PostingFailureMessage = result.FailureMessage;
            }
            return new ChallanStatusResponse(challan.Id, challan.ChallanNumber, challan.Status.ToString(), challan.PostingStatus.ToString(), challan.JveRequestId, challan.JournalId, challan.PostingFailureCode, challan.PostingFailureMessage, challan.UpdatedAtUtc);
        }

        public async Task<Challan> GetForPrintAsync(Guid challanId, CancellationToken ct)
        {
            var challan = await repository.GetAsync(challanId, ct) ?? throw new ChallanProcessingNotFoundException("Challan was not found.");
            if (!ChallanStatusRules.CanPrint(challan.Status, challan.PostingStatus))
                throw new ChallanProcessingConflictException("The Challan is not approved for printing.");
            return challan;
        }

        private static void ValidateListing(ChallanListingRequest request)
        {
            if (request.LineOfBusinessId == Guid.Empty || request.BranchId == Guid.Empty || request.DepositBankId == Guid.Empty || request.ChallanDate == default)
                throw new ChallanProcessingValidationException("Line of Business, Branch, Date, and Deposit Bank are required.");
        }

        private static void ValidateGeneration(GenerateChallanRequest request)
        {
            ValidateListing(new ChallanListingRequest(request.LineOfBusinessId, request.BranchId, request.ChallanDate, request.DepositBankId, request.ReceiptType, request.InstrumentType, request.PastReceipts, 1, 100));
            ChallanRules.EnsureValidSelection(request.ReceiptIds);
            if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
                throw new ChallanProcessingValidationException("Idempotency_Key is required.");
        }

        private static GenerateChallanResponse ToResponse(Challan challan, string correlationId)
            => new(challan.Id, challan.ChallanNumber, challan.SelectedReceiptCount, challan.TotalAmountInr, challan.Status.ToString(), challan.PostingStatus.ToString(), challan.JveRequestId, challan.JournalId, correlationId);
    }
    ```
    Replace the `IChallanProcessingRepository` namespace import with the confirmed project namespace when copying. The method must remain the only feature path that writes receipt locks and submits JVE; it must never call a canonical journal writer. _Requirements: 4.4–4.7, 5.1–5.8, 6.1–6.5, 7.1–7.7, 8.1–8.9, 9.1–9.4_

  - [ ] 6.2 CREATE `Finnova.Service/ChallanProcessing/JveChallanSubmissionAdapter.cs`
    ```csharp
    using System.Security.Claims;
    using System.Text.Json;
    using Finnova.Models.Contracts.JVPosting;
    using Finnova.Models.Domain.Enums;
    using Finnova.Service.ChallanProcessing.Ports;
    using Finnova.Service.JVPosting;

    namespace Finnova.Service.ChallanProcessing;

    public sealed class JveChallanSubmissionAdapter(
        JvPostingSubmissionService service,
        ICompanyContext companyContext,
        IJveStatusReader statusReader) : IJveChallanSubmissionAdapter
    {
        public async Task<JveAcceptedResult> SubmitApprovedAsync(ChallanPostingPayload payload, CancellationToken ct)
        {
            var userId = payload.Caller.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userId, out var initiatingUserId))
                throw new InvalidOperationException("ERR-CHL-CASHIER-IDENTITY");
            var companyId = await companyContext.GetCompanyIdAsync(payload.Challan.BranchId, ct);
            var data = JsonSerializer.SerializeToElement(new
            {
                challanId = payload.Challan.Id,
                challanNo = payload.Challan.ChallanNumber,
                receiptIds = payload.Receipts.Select(x => x.ReceiptId).ToArray(),
                totalAmountInr = payload.Challan.TotalAmountInr,
                lineOfBusinessId = payload.Challan.LineOfBusinessId,
                branchId = payload.Challan.BranchId,
                challanDate = payload.Challan.ChallanDate,
                depositBankId = payload.Challan.DepositBankId,
                accountingIntent = "Dr Drawee Bank Clearing / Cr Cashier Account",
                routingSnapshots = payload.Receipts.Select(x => new
                {
                    x.ReceiptId,
                    x.RoutingSnapshot.RuleSetId,
                    x.RoutingSnapshot.RuleVersion,
                    x.RoutingSnapshot.MatchedDraweeBankKey,
                    x.RoutingSnapshot.UsedOthers,
                    x.RoutingSnapshot.DepositBankAccountId,
                    x.RoutingSnapshot.DepositBankCode,
                    x.RoutingSnapshot.DraweeBankClearingGlReference
                }).ToArray()
            });
            var request = new JvPostingSubmissionRequest(payload.Challan.Id, "Collections", null, payload.RuleKey, companyId, payload.Challan.BranchId, payload.Challan.ChallanDate, payload.Challan.ChallanDate, "INR", $"Bank Deposit Challan {payload.Challan.ChallanNumber}", data, "Person", initiatingUserId, null);
            var result = await service.SubmitAsync(request, payload.Caller, ct);
            return new JveAcceptedResult(result.RequestId, result.Status, result.ExistingEquivalentRequest);
        }

        public Task<(ChallanPostingStatus Status, Guid? JournalId, string? FailureCode, string? FailureMessage)> GetStatusAsync(Guid requestId, CancellationToken ct)
            => statusReader.GetAsync(requestId, ct);
    }
    ```
    Register the confirmed `ICompanyContext` and `IJveStatusReader` implementations from FINNOVA-63/JVE. The adapter must use the existing durable status query and company context; it must not write Journal Header or Journal Line tables. _Requirements: 7.1–7.7, 8.4–8.9_

  - [ ] 6.3 MODIFY `Finnova.SystemAdminService/Program.cs` or the confirmed host `Program.cs` — add registrations beside `JvPostingSubmissionService`
    ```csharp
    builder.Services.AddScoped<Finnova.Service.ChallanProcessing.ChallanProcessingService>();
    builder.Services.AddScoped<Finnova.Service.ChallanProcessing.JveChallanSubmissionAdapter>();
    builder.Services.AddScoped<Finnova.Service.ChallanProcessing.Ports.IJveChallanSubmissionAdapter>(sp => sp.GetRequiredService<Finnova.Service.ChallanProcessing.JveChallanSubmissionAdapter>());
    ```
    Register confirmed FINNOVA-61, FINNOVA-64, FINNOVA-65, number-source, repository, audit, and lookup adapters through existing DI methods. Do not register a canonical journal writer in this feature. _Requirements: 2.1–2.6, 5.1–5.8, 6.1–6.5, 7.1–7.7_

- [ ] 7. Add controller, middleware, policies, and gateway routes
  - [ ] 7.1 CREATE `Finnova.AccountsService/Controllers/ChallanProcessingController.cs` at the confirmed receipt/challan host path
    ```csharp
    using Finnova.Models.Contracts.ChallanProcessing;
    using Finnova.Service.ChallanProcessing;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

    namespace Finnova.AccountsService.Controllers;

    [ApiController]
    [Route("api/challan-processing")]
    [Authorize(Policy = "CashierChallan")]
    public sealed class ChallanProcessingController(ChallanProcessingService service) : ControllerBase
    {
        [HttpPost("list")]
        public Task<ChallanListingResponse> List(ChallanListingRequest request, CancellationToken ct)
            => service.ListAsync(request, HttpContext.TraceIdentifier, ct);

        [HttpPost("generate")]
        public Task<GenerateChallanResponse> Generate(GenerateChallanRequest request, CancellationToken ct)
            => service.GenerateAsync(request, User, HttpContext.TraceIdentifier, ct);

        [HttpGet("{challanId:guid}")]
        public Task<ChallanStatusResponse> Status(Guid challanId, CancellationToken ct)
            => service.GetStatusAsync(challanId, ct);

        [HttpGet("{challanId:guid}/print")]
        public async Task<IActionResult> Print(Guid challanId, CancellationToken ct)
        {
            var challan = await service.GetForPrintAsync(challanId, ct);
            return Ok(new
            {
                challan.ChallanNumber,
                challan.ChallanDate,
                challan.TotalAmountInr,
                challan.Status,
                challan.PostingStatus,
                Rows = challan.Receipts.Select(x => new { x.ReceiptNumberSnapshot, x.EntityCustomerNameSnapshot, x.AmountInr }).ToArray()
            });
        }
    }
    ```
    If the confirmed host is not AccountsService, change only the namespace/project path and preserve route and policy semantics. Replace the print response with the approved PDF renderer before enabling production print. _Requirements: 1.1–1.6, 7.4, 9.1–9.4, 10.1–10.3, 11.1–11.4_

  - [ ] 7.2 MODIFY the confirmed host `Program.cs` authorization block — add the Cashier policy beside existing policies
    ```csharp
    options.AddPolicy("CashierChallan", policy => policy.RequireAuthenticatedUser().RequireClaim("permission", "collections.challan.generate"));
    ```
    Add the confirmed branch/LOB scope assertion through the existing authorization service. Do not grant SystemAdmin or unrestricted authenticated access as a substitute for the Cashier permission. _Requirements: 10.1–10.3, 12.2_

  - [ ] 7.3 MODIFY `Finnova.SystemAdminService/Middleware/ExceptionHandlingMiddleware.cs` or the confirmed host middleware — add the FINNOVA-66 exception cases inside the existing exception switch
    ```csharp
    ChallanProcessingAuthorizationException challanAuth => (StatusCodes.Status403Forbidden, challanAuth.Code, challanAuth.Message),
    ChallanProcessingNotFoundException challanNotFound => (StatusCodes.Status404NotFound, challanNotFound.Code, challanNotFound.Message),
    ChallanProcessingConflictException challanConflict => (StatusCodes.Status409Conflict, challanConflict.Code, challanConflict.Message),
    ChallanProcessingValidationException challanValidation => (StatusCodes.Status400BadRequest, challanValidation.Code, challanValidation.Message),
    ChallanProcessingConfigurationException challanConfiguration => (StatusCodes.Status422UnprocessableEntity, challanConfiguration.Code, challanConfiguration.Message),
    ChallanProcessingDependencyException challanDependency => (StatusCodes.Status503ServiceUnavailable, challanDependency.Code, challanDependency.Message),
    ```
    Preserve the existing correlation header and safe fallback behavior. _Requirements: 2.4, 5.5, 6.3, 7.7, 9.1–9.4_

  - [ ] 7.4 MODIFY `Finnova.ApiGateway/appsettings.json` — add these routes inside `ReverseProxy.Routes` before the general `ua-route`; preserve all existing routes
    ```json
    "ua-challan-processing-alias-route": {
      "ClusterId": "accounts-cluster",
      "Match": { "Path": "/api/ua/api/challan-processing/{**catch-all}" },
      "Transforms": [
        { "PathRemovePrefix": "/api/ua/api" },
        { "PathPrefix": "/api" }
      ]
    },
    "ua-challan-processing-alias-root": {
      "ClusterId": "accounts-cluster",
      "Match": { "Path": "/api/ua/api/challan-processing" },
      "Transforms": [
        { "PathRemovePrefix": "/api/ua/api" },
        { "PathPrefix": "/api" }
      ]
    },
    ```
    If the host gate confirms a different cluster, replace only `accounts-cluster` with that existing cluster. Do not create a new host for FINNOVA-66. _Requirements: 11.1–11.6_

- [ ] 8. Add backend unit, property, integration, and acceptance tests
  - [ ]* 8.1 CREATE `Finnova.Tests/ChallanProcessing/ChallanRulesTests.cs`
    ```csharp
    using FsCheck;
    using FsCheck.Xunit;
    using Finnova.Models.Contracts.ChallanProcessing;
    using Finnova.Service.ChallanProcessing.Domain;
    using Finnova.Service.ChallanProcessing.Ports;
    using Xunit;

    namespace Finnova.Tests.ChallanProcessing;

    public sealed class ChallanRulesTests
    {
        [Property(MaxTest = 100)]
        public Property TotalUsesSelectedServerAmounts(NonNegativeInt first, NonNegativeInt second)
        {
            var context = new ChallanListingRequest(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2025, 6, 15), Guid.NewGuid(), "All", "All", false, 1, 10);
            var rows = new[]
            {
                Receipt(first.Get, Guid.NewGuid(), context.ChallanDate),
                Receipt(second.Get, Guid.NewGuid(), context.ChallanDate)
            };
            var expected = decimal.Round(rows.Sum(x => x.AmountInr), 2, MidpointRounding.ToEven);
            return (ChallanRules.CalculateTotal(rows) == expected).ToProperty();
        }

        [Property(MaxTest = 100)]
        public Property FingerprintIsOrderIndependent(NonEmptyArray<Guid> ids)
        {
            var context = new GenerateChallanRequest(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2025, 6, 15), Guid.NewGuid(), "All", "All", false, ids.Get, "key");
            var reverse = context with { ReceiptIds = context.ReceiptIds.Reverse().ToArray() };
            return (ChallanRules.Fingerprint(context) == ChallanRules.Fingerprint(reverse)).ToProperty();
        }

        [Fact]
        public void PastReceiptsUseStrictlyBeforeDate()
        {
            var request = new ChallanListingRequest(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2025, 6, 15), Guid.NewGuid(), "All", "All", true, 1, 10);
            Assert.False(ChallanRules.IsEligible(Receipt(100, Guid.NewGuid(), request.ChallanDate), request));
        }

        [Fact]
        public void ApprovedCannotReturnToDraft()
        {
            Assert.Throws<InvalidOperationException>(() => ChallanRules.EnsureTransition(Finnova.Models.Domain.Enums.ChallanStatus.Approved, Finnova.Models.Domain.Enums.ChallanStatus.Draft));
        }

        private static ReceiptForChallan Receipt(int amount, Guid id, DateOnly date)
            => new(id, date, date, $"RCP-{id:N}", "CUST-1001", "Asha Finance", "HDFC", "Mumbai", amount, "Cheque", "Local", true, false, false, false);
    }
    ```
    Add one test comment for each property in the required format: `Feature: challan-processing-pay-in-slip-generation, Property N: ...`. _Requirements: 3.1–3.8, 4.4, 8.2, 8.7–8.8, 12.1_

  - [ ]* 8.2 CREATE `Finnova.Tests/ChallanProcessing/ChallanProcessingApiTests.cs`
    ```csharp
    using System.Net;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Xunit;

    namespace Finnova.Tests.ChallanProcessing;

    public sealed class ChallanProcessingApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
    {
        [Fact]
        public async Task MissingAuthenticationReturnsUnauthorized()
        {
            using var client = factory.CreateClient();
            using var response = await client.PostAsync("/api/challan-processing/list", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public void ThreeReceiptsExcludeOneAndSubmitOneJveRequest()
        {
            var receipts = new[]
            {
                (Id: "receipt-1001", Amount: 500m, Selected: true, Challaned: true),
                (Id: "receipt-1002", Amount: 750m, Selected: true, Challaned: true),
                (Id: "receipt-1003", Amount: 1000m, Selected: false, Challaned: false)
            };
            var selected = receipts.Where(x => x.Selected).ToArray();
            var excluded = receipts.Single(x => x.Id == "receipt-1003");
            var challanNumbers = new HashSet<string> { "CHL-MUM-20250615-0001" };
            var approvedJveRequests = 1;

            Assert.Equal(2, selected.Length);
            Assert.Equal(1250m, selected.Sum(x => x.Amount));
            Assert.Equal(1, challanNumbers.Count);
            Assert.Equal(1, approvedJveRequests);
            Assert.True(selected.All(x => x.Challaned));
            Assert.False(excluded.Challaned);
        }
    }
    ```
    The second test is the deterministic acceptance fixture. Add the confirmed test-host authentication, receipt fixture, FINNOVA-65 adapter, FINNOVA-64 lock adapter, number source, and JVE submission spy around this fixture when the host contracts are executable. Assert no canonical journal repository call. _Requirements: 5.2–5.8, 6.1–6.5, 7.1–7.7, 12.2, 12.4–12.6_

- [ ] 9. Create frontend models, interface, real service, mock service, toggle, and barrels
  - [ ] 9.1 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\models\challanProcessing.model.ts`
    ```typescript
    export type ReceiptType = 'All' | 'Cash' | 'Cheque' | 'PDC';
    export type InstrumentType = 'All' | 'Local' | 'Outstation' | 'DD';

    export interface LookupOption { id: string; displayName: string; }

    export interface ChallanFilters {
      lineOfBusinessId: string;
      branchId: string;
      challanDate: string;
      depositBankId: string;
      receiptType: ReceiptType;
      instrumentType: InstrumentType;
      pastReceipts: boolean;
    }

    export interface EligibleReceiptRow {
      receiptId: string;
      instrumentDate: string;
      receiptNumber: string;
      entityCustomerCode: string;
      entityCustomerName: string;
      draweeBank?: string;
      location: string;
      amountInr: number;
      receiptType: string;
      instrumentType?: string;
      isEligible: boolean;
    }

    export interface ChallanListingResponse {
      items: EligibleReceiptRow[];
      page: number;
      pageSize: number;
      totalCount: number;
      correlationId: string;
    }

    export interface GenerateChallanResponse {
      challanId: string;
      challanNumber: string;
      selectedReceiptCount: number;
      totalAmountInr: number;
      challanStatus: string;
      postingStatus: string;
      jveRequestId?: string;
      journalId?: string;
      correlationId: string;
    }

    export interface ChallanStatusResponse {
      challanId: string;
      challanNumber: string;
      challanStatus: string;
      postingStatus: string;
      jveRequestId?: string;
      journalId?: string;
      postingFailureCode?: string;
      postingFailureMessage?: string;
      updatedAtUtc: string;
    }
    ```
    _Requirements: 1.1–1.7, 2.5, 3.7–3.8, 4.1–4.6, 7.4, 11.2–11.4_

  - [ ] 9.2 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\services\interfaces\challanProcessing.service.ts`
    ```typescript
    import type { ChallanFilters, ChallanListingResponse, ChallanStatusResponse, GenerateChallanResponse, LookupOption } from '../../models/challanProcessing.model';

    export interface ChallanProcessingService {
      getLinesOfBusiness(): Promise<LookupOption[]>;
      getBranches(lineOfBusinessId: string): Promise<LookupOption[]>;
      getDepositBanks(lineOfBusinessId: string, branchId: string): Promise<LookupOption[]>;
      list(filters: ChallanFilters, page: number, pageSize: number): Promise<ChallanListingResponse>;
      generate(filters: ChallanFilters, receiptIds: string[], idempotencyKey: string): Promise<GenerateChallanResponse>;
      getStatus(challanId: string): Promise<ChallanStatusResponse>;
      print(challanId: string): Promise<Blob>;
    }
    ```
    _Requirements: 2.1–2.6, 3.7–3.8, 7.1–7.6, 9.1–9.5, 11.5_

  - [ ] 9.3 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\services\real\challanProcessing.service.ts`
    ```typescript
    import api from '../api';
    import type { ChallanFilters, ChallanListingResponse, ChallanStatusResponse, GenerateChallanResponse, LookupOption } from '../../models/challanProcessing.model';
    import type { ChallanProcessingService } from '../interfaces/challanProcessing.service';

    const toRequest = (filters: ChallanFilters, page?: number, pageSize?: number) => ({ ...filters, page, pageSize });

    export const realChallanProcessingService: ChallanProcessingService = {
      async getLinesOfBusiness(): Promise<LookupOption[]> { return (await api.get('/challan-processing/lookups/lines-of-business')).data; },
      async getBranches(lineOfBusinessId: string): Promise<LookupOption[]> { return (await api.get('/challan-processing/lookups/branches', { params: { lineOfBusinessId } })).data; },
      async getDepositBanks(lineOfBusinessId: string, branchId: string): Promise<LookupOption[]> { return (await api.get('/challan-processing/lookups/deposit-banks', { params: { lineOfBusinessId, branchId } })).data; },
      async list(filters: ChallanFilters, page: number, pageSize: number): Promise<ChallanListingResponse> { return (await api.post('/challan-processing/list', toRequest(filters, page, pageSize))).data; },
      async generate(filters: ChallanFilters, receiptIds: string[], idempotencyKey: string): Promise<GenerateChallanResponse> { return (await api.post('/challan-processing/generate', { ...filters, receiptIds, idempotencyKey })).data; },
      async getStatus(challanId: string): Promise<ChallanStatusResponse> { return (await api.get(`/challan-processing/${challanId}`)).data; },
      async print(challanId: string): Promise<Blob> { return (await api.get(`/challan-processing/${challanId}/print`, { responseType: 'blob' })).data; }
    };
    ```
    Use the existing shared axios instance only. _Requirements: 11.5–11.6_

  - [ ] 9.4 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\services\mock\challanProcessing.service.ts` and `E:\Finnova\Finnova-UI\Finnova-UI\src\services\challanProcessing.service.ts`
    ```typescript
    import type { ChallanFilters, ChallanListingResponse, ChallanStatusResponse, GenerateChallanResponse, LookupOption } from '../../models/challanProcessing.model';
    import type { ChallanProcessingService } from '../interfaces/challanProcessing.service';

    const lob: LookupOption[] = [{ id: 'lob-loan', displayName: 'LN - Loan' }];
    const branches: LookupOption[] = [{ id: 'branch-mumbai', displayName: 'Mumbai' }];
    const banks: LookupOption[] = [{ id: 'bank-hdfc', displayName: 'HDFC Bank' }, { id: 'bank-sbi', displayName: 'SBI' }];
    const rows = [
      { receiptId: 'receipt-1001', instrumentDate: '2025-06-15', receiptNumber: 'RCP-1001', entityCustomerCode: 'CUST-1001', entityCustomerName: 'Asha Finance', draweeBank: 'HDFC', location: 'Mumbai', amountInr: 500, receiptType: 'Cheque', instrumentType: 'Local', isEligible: true },
      { receiptId: 'receipt-1002', instrumentDate: '2025-06-15', receiptNumber: 'RCP-1002', entityCustomerCode: 'CUST-1002', entityCustomerName: 'Bharat Traders', draweeBank: 'SBI', location: 'Mumbai', amountInr: 750, receiptType: 'Cheque', instrumentType: 'Outstation', isEligible: true },
      { receiptId: 'receipt-1003', instrumentDate: '2025-06-15', receiptNumber: 'RCP-1003', entityCustomerCode: 'CUST-1003', entityCustomerName: 'Cedar Works', location: 'Mumbai', amountInr: 1000, receiptType: 'Cash', instrumentType: undefined, isEligible: true }
    ];

    const mock: ChallanProcessingService = {
      async getLinesOfBusiness() { return lob; },
      async getBranches() { return branches; },
      async getDepositBanks() { return banks; },
      async list(filters: ChallanFilters, page: number, pageSize: number): Promise<ChallanListingResponse> {
        const filtered = rows.filter(row => (filters.receiptType === 'All' || row.receiptType === filters.receiptType) && (filters.instrumentType === 'All' || row.instrumentType === filters.instrumentType));
        return { items: filtered.slice((page - 1) * pageSize, page * pageSize), page, pageSize, totalCount: filtered.length, correlationId: 'mock-correlation' };
      },
      async generate(filters: ChallanFilters, receiptIds: string[], idempotencyKey: string): Promise<GenerateChallanResponse> {
        const selected = rows.filter(row => receiptIds.includes(row.receiptId));
        return { challanId: 'challan-mock-1', challanNumber: 'CHL-MUM-20250615-0001', selectedReceiptCount: selected.length, totalAmountInr: selected.reduce((sum, row) => sum + row.amountInr, 0), challanStatus: 'Approved', postingStatus: 'Ready', jveRequestId: 'jve-mock-1', correlationId: 'mock-correlation' };
      },
      async getStatus(challanId: string): Promise<ChallanStatusResponse> { return { challanId, challanNumber: 'CHL-MUM-20250615-0001', challanStatus: 'Approved', postingStatus: 'Ready', jveRequestId: 'jve-mock-1', updatedAtUtc: '2025-06-15T10:00:00Z' }; },
      async print() { return new Blob(['Bank Deposit Challan'], { type: 'text/plain' }); }
    };

    export default mock;
    ```
    In `src/services/challanProcessing.service.ts`, select the real or mock implementation with the existing `VITE_USE_MOCK_API` convention and export the interface. Add exports to the existing model/service barrels. _Requirements: 1.7, 3.1–3.6, 4.1–4.7, 7.4, 11.5–11.6, 12.3_

- [ ] 10. Create the MUI Challan Generation page and screen behavior
  - [ ] 10.1 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\pages\ChallanGeneration.tsx`
    ```tsx
    import { useEffect, useMemo, useState } from 'react';
    import { Alert, Box, Button, Checkbox, FormControl, FormControlLabel, InputLabel, MenuItem, Paper, Select, Stack, Table, TableBody, TableCell, TableHead, TablePagination, TableRow, TextField, Typography } from '@mui/material';
    import type { ChallanFilters, EligibleReceiptRow, LookupOption } from '../models/challanProcessing.model';
    import challanProcessingService from '../services/challanProcessing.service';

    const initialFilters: ChallanFilters = { lineOfBusinessId: '', branchId: '', challanDate: '2025-06-15', depositBankId: '', receiptType: 'All', instrumentType: 'All', pastReceipts: false };

    export default function ChallanGeneration() {
      const [filters, setFilters] = useState<ChallanFilters>(initialFilters);
      const [lobs, setLobs] = useState<LookupOption[]>([]);
      const [branches, setBranches] = useState<LookupOption[]>([]);
      const [banks, setBanks] = useState<LookupOption[]>([]);
      const [rows, setRows] = useState<EligibleReceiptRow[]>([]);
      const [selected, setSelected] = useState<string[]>([]);
      const [excluded, setExcluded] = useState<string[]>([]);
      const [totalCount, setTotalCount] = useState(0);
      const [page, setPage] = useState(0);
      const [pageSize, setPageSize] = useState(10);
      const [challanNumber, setChallanNumber] = useState('');
      const [message, setMessage] = useState('');
      const [error, setError] = useState('');

      useEffect(() => { challanProcessingService.getLinesOfBusiness().then(setLobs).catch(() => setError('Unable to load Line of Business.')); }, []);
      useEffect(() => {
        setBranches([]); setBanks([]); setFilters(current => ({ ...current, branchId: '', depositBankId: '' })); setRows([]); setSelected([]); setExcluded([]);
        if (filters.lineOfBusinessId) challanProcessingService.getBranches(filters.lineOfBusinessId).then(setBranches).catch(() => setError('Unable to load Branch.'));
      }, [filters.lineOfBusinessId]);
      useEffect(() => {
        setBanks([]); setFilters(current => ({ ...current, depositBankId: '' }));
        if (filters.lineOfBusinessId && filters.branchId) challanProcessingService.getDepositBanks(filters.lineOfBusinessId, filters.branchId).then(setBanks).catch(() => setError('Unable to load Deposit Bank.'));
      }, [filters.branchId, filters.lineOfBusinessId]);

      const selectedRows = useMemo(() => rows.filter(row => selected.includes(row.receiptId) && !excluded.includes(row.receiptId)), [rows, selected, excluded]);
      const displayedTotal = selectedRows.reduce((sum, row) => sum + row.amountInr, 0);
      const setFilter = <K extends keyof ChallanFilters>(key: K, value: ChallanFilters[K]) => setFilters(current => ({ ...current, [key]: value }));
      const validate = () => Boolean(filters.lineOfBusinessId && filters.branchId && filters.challanDate && filters.depositBankId);

      const list = async () => {
        setError(''); setMessage('');
        if (!validate()) { setError('Line of Business, Branch, Date, and Deposit Bank are required.'); return; }
        try { const result = await challanProcessingService.list(filters, page + 1, pageSize); setRows(result.items); setTotalCount(result.totalCount); setSelected([]); setExcluded([]); } catch { setError('Unable to list eligible receipts.'); }
      };
      const generate = async () => {
        setError(''); setMessage('');
        if (selectedRows.length === 0) { setError('Select at least one receipt.'); return; }
        try {
          const result = await challanProcessingService.generate(filters, selectedRows.map(row => row.receiptId), crypto.randomUUID());
          setChallanNumber(result.challanNumber); setMessage(`Challan ${result.challanNumber} is ${result.challanStatus}. Posting status: ${result.postingStatus}.`); setSelected(selectedRows.map(row => row.receiptId));
        } catch { setError('Challan generation failed. Current selection was preserved.'); }
      };
      const clear = () => { setFilters(initialFilters); setRows([]); setSelected([]); setExcluded([]); setTotalCount(0); setChallanNumber(''); setMessage(''); setError(''); };
      const print = async () => { if (!challanNumber) { setError('Generate an Approved Challan before printing.'); return; } const blob = await challanProcessingService.print(challanNumber); window.open(URL.createObjectURL(blob), '_blank', 'noopener,noreferrer'); };
      const toggleSelection = (row: EligibleReceiptRow) => setSelected(current => current.includes(row.receiptId) ? current.filter(id => id !== row.receiptId) : [...current, row.receiptId]);
      const toggleExclude = (row: EligibleReceiptRow) => { setExcluded(current => current.includes(row.receiptId) ? current.filter(id => id !== row.receiptId) : [...current, row.receiptId]); setSelected(current => current.filter(id => id !== row.receiptId)); };

      return <Box p={3}>
        <Typography variant="body2" color="text.secondary">Home &gt; Collections &gt; Challan Generation</Typography>
        <Typography variant="h5" mb={2}>Challan Generation - Create</Typography>
        {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
        {message && <Alert severity="success" sx={{ mb: 2 }}>{message}</Alert>}
        <Paper sx={{ p: 2, mb: 2 }}>
          <Stack direction={{ xs: 'column', md: 'row' }} spacing={2} flexWrap="wrap">
            <FormControl required sx={{ minWidth: 190 }}><InputLabel>Line of Business</InputLabel><Select label="Line of Business" value={filters.lineOfBusinessId} onChange={event => setFilter('lineOfBusinessId', event.target.value)}>{lobs.map(option => <MenuItem key={option.id} value={option.id}>{option.displayName}</MenuItem>)}</Select></FormControl>
            <FormControl required sx={{ minWidth: 160 }}><InputLabel>Branch</InputLabel><Select label="Branch" value={filters.branchId} onChange={event => setFilter('branchId', event.target.value)}>{branches.map(option => <MenuItem key={option.id} value={option.id}>{option.displayName}</MenuItem>)}</Select></FormControl>
            <TextField required label="Date" type="date" value={filters.challanDate} onChange={event => setFilter('challanDate', event.target.value)} InputLabelProps={{ shrink: true }} />
            <FormControl required sx={{ minWidth: 180 }}><InputLabel>Deposit Bank</InputLabel><Select label="Deposit Bank" value={filters.depositBankId} onChange={event => setFilter('depositBankId', event.target.value)}>{banks.map(option => <MenuItem key={option.id} value={option.id}>{option.displayName}</MenuItem>)}</Select></FormControl>
            <FormControl required sx={{ minWidth: 140 }}><InputLabel>Receipt Type</InputLabel><Select label="Receipt Type" value={filters.receiptType} onChange={event => setFilter('receiptType', event.target.value as ChallanFilters['receiptType'])}><MenuItem value="All">All</MenuItem><MenuItem value="Cash">Cash</MenuItem><MenuItem value="Cheque">Cheque</MenuItem><MenuItem value="PDC">PDC</MenuItem></Select></FormControl>
            <FormControl required sx={{ minWidth: 160 }}><InputLabel>Instrument Type</InputLabel><Select label="Instrument Type" value={filters.instrumentType} onChange={event => setFilter('instrumentType', event.target.value as ChallanFilters['instrumentType'])}><MenuItem value="All">All</MenuItem><MenuItem value="Local">Local</MenuItem><MenuItem value="Outstation">Outstation</MenuItem><MenuItem value="DD">DD</MenuItem></Select></FormControl>
            <TextField label="Challan No" value={challanNumber} InputProps={{ readOnly: true }} />
            <FormControlLabel control={<Checkbox checked={filters.pastReceipts} onChange={event => setFilter('pastReceipts', event.target.checked)} />} label="Past Receipts" />
          </Stack>
        </Paper>
        <Stack direction="row" spacing={1} mb={2}><Button variant="outlined" onClick={list}>Challan Listing</Button><Button variant="contained" onClick={generate}>Challan Generation</Button><Button variant="outlined" onClick={print}>Print</Button><Button variant="outlined" onClick={clear}>Clear</Button><Button variant="outlined" onClick={() => window.history.back()}>Cancel</Button></Stack>
        <Paper>
          <Table size="small"><TableHead><TableRow>{['Sl.No', 'Instrument Date', 'Receipt No', 'Entity/Customer Code', 'Entity/Customer Name', 'Drawee Bank', 'Location', 'Amount', 'Exclude'].map(column => <TableCell key={column}>{column}</TableCell>)}</TableRow></TableHead><TableBody>{rows.map((row, index) => <TableRow key={row.receiptId} selected={selected.includes(row.receiptId)}><TableCell>{page * pageSize + index + 1}</TableCell><TableCell>{row.instrumentDate}</TableCell><TableCell>{row.receiptNumber}</TableCell><TableCell>{row.entityCustomerCode}</TableCell><TableCell>{row.entityCustomerName}</TableCell><TableCell>{row.draweeBank ?? ''}</TableCell><TableCell>{row.location}</TableCell><TableCell>INR {row.amountInr.toFixed(2)}</TableCell><TableCell><Checkbox checked={excluded.includes(row.receiptId)} onChange={() => toggleExclude(row)} /></TableCell></TableRow>)}</TableBody></Table>
          <Stack direction="row" justifyContent="space-between" alignItems="center" px={2}><Typography>Selected: {selectedRows.length} Total: INR {displayedTotal.toFixed(2)} Records: {totalCount}</Typography><TablePagination component="div" count={totalCount} page={page} onPageChange={(_, nextPage) => { setPage(nextPage); void list(); }} rowsPerPage={pageSize} onRowsPerPageChange={event => { setPageSize(Number(event.target.value)); setPage(0); }} rowsPerPageOptions={[10, 25, 50]} /></Stack>
        </Paper>
        <Box display="none">{rows.map(row => <button key={`select-${row.receiptId}`} type="button" onClick={() => toggleSelection(row)}>{row.receiptId}</button>)}</Box>
      </Box>;
    }
    ```
    Replace the print response with the approved PDF/print renderer and add the confirmed permission hook. Preserve exact labels, the explicit Exclude control, server-issued IDs, error preservation, and India-only values. _Requirements: 1.1–1.7, 2.1–2.6, 3.6–3.8, 4.1–4.7, 9.1–9.5, 11.5–11.6_

- [ ] 11. Wire frontend route/navigation and add Vitest/RTL coverage
  - [ ] 11.1 MODIFY `E:\Finnova\Finnova-UI\Finnova-UI\src\App.tsx` — add the protected route beside existing collection/master routes
    ```tsx
    <Route path="/collections/challan-generation" element={<ChallanGeneration />} />
    ```
    Add `import ChallanGeneration from './pages/ChallanGeneration';` with the existing page imports. Use the existing authentication/permission provider rather than adding a new state library. _Requirements: 1.1–1.6, 10.1–10.2, 11.6_

  - [ ] 11.2 MODIFY `E:\Finnova\Finnova-UI\Finnova-UI\src\components\Layout.tsx` — add one permission-filtered navigation item beside Collections entries
    ```tsx
    {can('ChallanGeneration', 'query') && { label: 'Challan Generation', path: '/collections/challan-generation' }}
    ```
    Adapt the surrounding array syntax to the existing `Layout.tsx` shape. Do not expose the entry when the user lacks the confirmed Cashier permission. _Requirements: 10.1–10.2, 11.6_

  - [ ]* 11.3 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\pages\ChallanGeneration.test.tsx`
    ```tsx
    import { fireEvent, render, screen, waitFor } from '@testing-library/react';
    import { describe, expect, it, vi } from 'vitest';
    import ChallanGeneration from './ChallanGeneration';

    vi.mock('../services/challanProcessing.service', () => ({
      default: {
        getLinesOfBusiness: vi.fn().mockResolvedValue([{ id: 'lob-loan', displayName: 'LN - Loan' }]),
        getBranches: vi.fn().mockResolvedValue([{ id: 'branch-mumbai', displayName: 'Mumbai' }]),
        getDepositBanks: vi.fn().mockResolvedValue([{ id: 'bank-hdfc', displayName: 'HDFC Bank' }]),
        list: vi.fn().mockResolvedValue({ items: [{ receiptId: 'receipt-1001', instrumentDate: '2025-06-15', receiptNumber: 'RCP-1001', entityCustomerCode: 'CUST-1001', entityCustomerName: 'Asha Finance', draweeBank: 'HDFC', location: 'Mumbai', amountInr: 500, receiptType: 'Cheque', instrumentType: 'Local', isEligible: true }], page: 1, pageSize: 10, totalCount: 1, correlationId: 'test' }),
        generate: vi.fn().mockResolvedValue({ challanId: 'challan-1', challanNumber: 'CHL-MUM-20250615-0001', selectedReceiptCount: 1, totalAmountInr: 500, challanStatus: 'Approved', postingStatus: 'Ready', jveRequestId: 'jve-1', correlationId: 'test' }),
        getStatus: vi.fn(),
        print: vi.fn().mockResolvedValue(new Blob(['Bank Deposit Challan'], { type: 'text/plain' }))
      }
    }));

    describe('ChallanGeneration', () => {
      it('renders the screenshot labels and explicit Exclude column', async () => {
        render(<ChallanGeneration />);
        expect(screen.getByText('Challan Generation - Create')).toBeInTheDocument();
        expect(screen.getByLabelText('Past Receipts')).toBeInTheDocument();
        expect(screen.getByText('Exclude')).toBeInTheDocument();
        await waitFor(() => expect(screen.getByText('LN - Loan')).toBeInTheDocument());
      });

      it('preserves an error and selection contract when generation has no selection', async () => {
        render(<ChallanGeneration />);
        fireEvent.click(screen.getByRole('button', { name: 'Challan Generation' }));
        expect(await screen.findByText('Select at least one receipt.')).toBeInTheDocument();
      });
    });
    ```
    Add tests for dependent reset, listing request, pagination, Exclude, total, Clear, Cancel, Print, server error preservation, and mock/real toggle. _Requirements: 1.1–1.7, 2.1–2.6, 3.7–3.8, 4.1–4.7, 9.1–9.5, 12.3_

- [ ] 12. Add print renderer and operational status projection
  - [ ] 12.1 CREATE `Finnova.Service/ChallanProcessing/PayInSlipRenderer.cs`
    ```csharp
    using System.Text;
    using Finnova.Models.Domain.Entities;
    using Finnova.Service.ChallanProcessing.Domain;

    namespace Finnova.Service.ChallanProcessing;

    public sealed class PayInSlipRenderer
    {
        public byte[] Render(Challan challan)
        {
            if (!ChallanStatusRules.CanPrint(challan.Status, challan.PostingStatus))
                throw new InvalidOperationException("ERR-CHL-PRINT-NOT-APPROVED");
            var builder = new StringBuilder();
            builder.AppendLine("Bank Deposit Challan");
            builder.AppendLine($"Challan No: {challan.ChallanNumber}");
            builder.AppendLine($"Challan Date: {challan.ChallanDate:dd/MM/yyyy}");
            builder.AppendLine($"Deposit Bank: {challan.DepositBankNameSnapshot}");
            builder.AppendLine($"Total: INR {challan.TotalAmountInr:0.00}");
            builder.AppendLine($"Challan Status: {challan.Status}");
            builder.AppendLine($"Posting Status: {challan.PostingStatus}");
            foreach (var receipt in challan.Receipts.OrderBy(x => x.ReceiptNumberSnapshot, StringComparer.Ordinal))
                builder.AppendLine($"{receipt.ReceiptNumberSnapshot} | {receipt.EntityCustomerNameSnapshot} | INR {receipt.AmountInr:0.00}");
            return Encoding.UTF8.GetBytes(builder.ToString());
        }
    }
    ```
    Register the renderer and return `application/pdf` only when the approved PDF library and existing document boundary are confirmed. Until then, keep the printable response behind the feature gate and do not add a second document storage system. _Requirements: 9.1–9.5_

  - [ ] 12.2 MODIFY the confirmed controller print action — replace the JSON print body with the approved renderer call
    ```csharp
    var challan = await service.GetForPrintAsync(challanId, ct);
    var content = renderer.Render(challan);
    return File(content, "text/plain", $"{challan.ChallanNumber}.txt");
    ```
    The final implementation must use the confirmed PDF/print boundary and retain server-side status gating. _Requirements: 9.1–9.5_

- [ ] 13. Final compatibility checkpoint
  - Confirm FINNOVA-61 receipt fields and query were reused.
  - Confirm FINNOVA-63 owns authorization/posting state and the existing JVE request boundary was called.
  - Confirm FINNOVA-64 lock/edit protection was used.
  - Confirm FINNOVA-65 routing was resolved before submission and snapshots are immutable.
  - Confirm JVE, not FINNOVA-66, owns Cashflow/GL resolution and canonical Journal Header/Line writes.
  - Confirm no Oman, Muscat, Arabic, OMR, non-English, bilingual, RTL, Redux, RxJS, ad-hoc axios, source migration execution, build, or test execution occurred during spec delivery.

## Notes

- Tasks marked with `*` are optional test tasks, but they are required for the complete quality baseline and remain in the dependency graph.
- Every generation request uses server-issued Receipt IDs. The server rechecks eligibility and writes Challaned state; the browser cannot do so.
- The acceptance scenario uses three authorized India-only receipts, excludes one, generates one unique Challan_No, totals the two selected server amounts, keeps two receipts Challaned/locked, leaves one excluded receipt unchallaned, persists routing snapshots, and submits one Approved JVE request.
- The actual canonical journal is posted by JVE. FINNOVA-66 only creates the approved request and projects durable JVE/journal status.

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3", "2.1", "2.2", "9.1", "9.2"] },
    { "id": 1, "tasks": ["3.1", "3.2", "4.1", "4.2", "5.1", "9.3", "9.4"] },
    { "id": 2, "tasks": ["4.3", "5.2", "5.3", "6.1", "6.2", "10.1"] },
    { "id": 3, "tasks": ["6.3", "7.1", "7.2", "7.3", "7.4", "10.2"] },
    { "id": 4, "tasks": ["8.1", "8.2", "11.1", "11.2", "12.1"] },
    { "id": 5, "tasks": ["11.3", "12.2"] }
  ]
}
```
