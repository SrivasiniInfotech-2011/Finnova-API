# Implementation Plan: Receipt Appropriation Engine

**Feature:** `receipt-appropriation-engine`  
**Jira:** FINNOVA-62  
**Source documents:** `requirements.md`, `design.md`  
**Backend repository:** `D:\Projects\Finnova\Finnova-API` (solution `Finnova.Backend.slnx`)  
**Frontend repository:** `D:\Projects\Finnova\Finnova-UI` (React 18 + TypeScript + MUI + Vite)  

## Overview

This document is the implementation handoff for FINNOVA-62. Apply the tasks in order. Every task below is a coding task and is labelled `CREATE <path>` or `MODIFY <path>`. CREATE blocks are complete file contents. MODIFY blocks identify the exact existing anchor and the replacement or insertion point.

The feature remains in `Finnova.AccountsService`, consumes the FINNOVA-61 Receipt, authoritative due-ledger, Appropriation Logic Master, Advance Account, and business-scope capabilities through ports, and persists only confirmed appropriations. It must not create a competing receipt, due, or rule source. All user-facing text and persisted display values are English-only. Amounts are INR with exactly two fractional digits, technical timestamps are UTC, and the UI remains left-to-right with no RTL or translation code.

## Before implementation: unresolved dependency gate

Do not invent local replacement entities for the following authoritative sources. Before applying Tasks 4, 8, and 9, obtain the concrete adapters and names from their owners:

- FINNOVA-61 Receipt context: receipt ID, customer, Company, LOB, amount, status, and row-version contract.
- Authoritative demand/cash-flow due ledger: pending-due projection, collectible statuses, due balance version, and atomic reserve/update operation.
- Appropriation Logic Master: active-rule projection, ordered details, version, direction, Full Due, and percentage precision.
- Advance Account source: active account lookup and atomic posting/reservation contract.
- Existing authorization scope source: Company, LOB, Branch, and customer scope resolution.

The adapter interfaces in Task 4 are the required seam. If an owner supplies an existing type, map it in the adapter and remove only duplicate local projection fields that are no longer needed. Do not change the requirements or design to bypass this gate.

## Task Dependency Graph

Tasks in the same wave may be applied in parallel. A wave begins only after every preceding wave is complete. Optional test tasks are still included in the graph.

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "2.1", "3.1"] },
    { "id": 1, "tasks": ["4.1", "4.2"] },
    { "id": 2, "tasks": ["5.1", "5.2", "6.1"] },
    { "id": 3, "tasks": ["6.2", "7.1"] },
    { "id": 4, "tasks": ["7.2", "8.1"] },
    { "id": 5, "tasks": ["8.2", "8.3", "9.1"] },
    { "id": 6, "tasks": ["9.2", "9.3", "9.4", "10.1", "10.2"] },
    { "id": 7, "tasks": ["11.1", "11.2", "11.3", "11.4", "11.5"] },
    { "id": 8, "tasks": ["12.1", "12.2", "12.3", "12.4"] },
    { "id": 9, "tasks": ["13.1", "13.2"] },
    { "id": 10, "tasks": ["14.1", "14.2", "14.3"] },
    { "id": 11, "tasks": ["15.1", "15.2"] }
  ]
}
```

## Tasks

- [ ] 1. Backend domain model
  - [ ] 1.1 CREATE `Finnova.Models/Domain/Enums/ReceiptAppropriationEnums.cs`

```csharp
namespace Finnova.Models.Domain.Enums;

public enum AppropriationMode { Manual = 0, Suggest = 1 }
public enum AppropriationDirection { Vertical = 0, Horizontal = 1 }
public enum ReceiptAppropriationStatus { Confirmed = 0 }
public enum CashFlowDueType { Installment = 0, Principal = 1, Interest = 2, Insurance = 3, ODI = 4, OtherCharges = 5 }
```

  - [ ] 1.2 CREATE `Finnova.Models/Domain/Entities/ReceiptAppropriation.cs`

```csharp
using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

public sealed class ReceiptAppropriation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReceiptId { get; set; }
    public Guid ClientRequestId { get; set; }
    public AppropriationMode Mode { get; set; }
    public ReceiptAppropriationStatus Status { get; set; } = ReceiptAppropriationStatus.Confirmed;
    public decimal ReceiptAmount { get; set; }
    public decimal DueAllocationTotal { get; set; }
    public decimal AdvanceAccountAmount { get; set; }
    public decimal ResidualAmount { get; set; }
    public Guid CustomerId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid LineOfBusinessId { get; set; }
    public Guid? RuleId { get; set; }
    public string? RuleVersion { get; set; }
    public AppropriationDirection? RuleDirection { get; set; }
    public bool? FullDue { get; set; }
    public string ConfirmedBy { get; set; } = string.Empty;
    public DateTime ConfirmedAtUtc { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public List<ReceiptAllocation> Allocations { get; set; } = new();
    public AdvanceAccountAllocation? AdvanceAccountAllocation { get; set; }
}

public sealed class ReceiptAllocation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReceiptAppropriationId { get; set; }
    public Guid PendingDueId { get; set; }
    public Guid AccountId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public CashFlowDueType CashFlowDueType { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal AllocatedAmount { get; set; }
    public decimal OutstandingAmountAtConfirm { get; set; }
    public Guid? RuleDetailId { get; set; }
    public int? RuleOrder { get; set; }
    public decimal? TargetAmount { get; set; }
    public string DueRowVersionAtConfirm { get; set; } = string.Empty;
    public ReceiptAppropriation ReceiptAppropriation { get; set; } = null!;
}

public sealed class AdvanceAccountAllocation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReceiptAppropriationId { get; set; }
    public Guid AdvanceAccountId { get; set; }
    public decimal Amount { get; set; }
    public Guid CompanyId { get; set; }
    public Guid CustomerId { get; set; }
    public string Currency { get; set; } = "INR";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public ReceiptAppropriation ReceiptAppropriation { get; set; } = null!;
}

public sealed class AppropriationAuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReceiptId { get; set; }
    public Guid ReceiptAppropriationId { get; set; }
    public Guid ClientRequestId { get; set; }
    public string Mode { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public decimal DueAllocationTotal { get; set; }
    public decimal AdvanceAccountAmount { get; set; }
    public decimal ResidualAmount { get; set; }
    public Guid? RuleId { get; set; }
    public string? RuleVersion { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
```

- [ ] 2. Domain exceptions
  - [ ] 2.1 CREATE `Finnova.Models/Domain/Exceptions/ReceiptAppropriationExceptions.cs`

```csharp
namespace Finnova.Models.Domain.Exceptions;

public abstract class ReceiptAppropriationException(string code, string message) : Exception(message)
{
    public string ErrorCode { get; } = code;
    public IReadOnlyDictionary<string, string[]> Errors { get; init; } = new Dictionary<string, string[]>();
}

public sealed class ReceiptAppropriationValidationException(string message) : ReceiptAppropriationException("ERR-APPROP-VALIDATION", message);
public sealed class ReceiptAppropriationNotFoundException(string message) : ReceiptAppropriationException("ERR-APPROP-NOT-FOUND", message);
public sealed class ReceiptAppropriationForbiddenException(string message) : ReceiptAppropriationException("ERR-APPROP-403", message);
public sealed class ReceiptAppropriationConflictException(string code, string message) : ReceiptAppropriationException(code, message);
public sealed class ReceiptAppropriationRuleException(string message) : ReceiptAppropriationException("ERR-APPROP-RULE-CONFIGURATION", message);
public sealed class ReceiptAppropriationAdvanceAccountException(string message) : ReceiptAppropriationException("ERR-APPROP-ADVANCE-ACCOUNT", message);
public sealed class ReceiptAppropriationReconciliationException(string message) : ReceiptAppropriationException("ERR-APPROP-RECONCILIATION", message);
public sealed class ReceiptAppropriationDependencyException(string message) : ReceiptAppropriationException("ERR-APPROP-DEPENDENCY", message);

public static class ReceiptAppropriationErrorCodes
{
    public const string IdempotencyConflict = "ERR-APPROP-IDEMPOTENCY-CONFLICT";
    public const string AlreadyConfirmed = "ERR-APPROP-ALREADY-CONFIRMED";
    public const string StaleData = "ERR-APPROP-STALE-DATA";
    public const string Concurrency = "ERR-APPROP-CONCURRENCY";
}
```

- [ ] 3. Backend contracts
  - [ ] 3.1 CREATE `Finnova.Models/Contracts/ReceiptAppropriation/ReceiptAppropriationContracts.cs`

```csharp
using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Contracts.ReceiptAppropriation;

public sealed record ReceiptAppropriationDueResponse(
    Guid DueId, Guid AccountId, string AccountNumber, CashFlowDueType CashFlowDueType,
    DateOnly DueDate, decimal OutstandingAmount, decimal AllocatedAmount, decimal RemainingAmount,
    string Currency, string DueVersion, Guid? RuleDetailId = null, int? RuleOrder = null,
    decimal? TargetAmount = null);

public sealed record AppropriationRuleDetailResponse(Guid Id, CashFlowDueType CashFlowDueType, decimal Percentage, int Order);
public sealed record AppropriationRuleSummary(Guid RuleId, string Version, AppropriationDirection Direction, bool FullDue, IReadOnlyList<AppropriationRuleDetailResponse> Details);
public sealed record AppropriationTotals(decimal ReceiptAmount, decimal DueAllocationTotal, decimal AdvanceAccountAmount, decimal ResidualAmount, bool Reconciles);
public sealed record AppropriationPreviewResponse(
    Guid ReceiptId, Guid CustomerId, Guid CompanyId, Guid LineOfBusinessId, decimal ReceiptDocumentAmount,
    AppropriationMode Mode, IReadOnlyList<ReceiptAppropriationDueResponse> Allocations,
    AppropriationRuleSummary? Rule, AppropriationTotals Totals, string ReceiptVersion, string RuleVersion,
    string DueFingerprint, DateTime GeneratedAtUtc, string CorrelationId);

public sealed record ShowAppropriationResponse(
    Guid ReceiptId, Guid CustomerId, Guid CompanyId, Guid LineOfBusinessId, decimal ReceiptDocumentAmount,
    IReadOnlyList<ReceiptAppropriationDueResponse> Data, int Total, int Page, int PageSize, int TotalPages,
    AppropriationTotals Totals, string ReceiptVersion, string DueFingerprint);

public sealed record ConfirmAllocationRequest(Guid DueId, decimal AllocatedAmount);
public sealed record ConfirmAppropriationRequest(
    Guid ClientRequestId, AppropriationMode Mode, IReadOnlyList<ConfirmAllocationRequest> Allocations,
    bool AcceptResidualTransfer, string? PreviewDueFingerprint = null, string? PreviewRuleVersion = null);

public sealed record ConfirmedAppropriationResponse(
    Guid AppropriationId, Guid ReceiptId, Guid ClientRequestId, AppropriationMode Mode,
    IReadOnlyList<ReceiptAppropriationDueResponse> Allocations, AppropriationTotals Totals,
    DateTime ConfirmedAtUtc, ReceiptAppropriationStatus Status, string Actor, Guid? AuditId, string CorrelationId);
```

- [ ] 4. Authoritative adapter contracts
  - [ ] 4.1 CREATE `Finnova.Service/ReceiptAppropriation/Ports/ReceiptAppropriationPorts.cs`

```csharp
using Finnova.Models.Domain.Enums;
using System.Security.Claims;

namespace Finnova.Service.ReceiptAppropriation.Ports;

public sealed record ReceiptContext(Guid Id, Guid CustomerId, string CustomerCode, Guid CompanyId, Guid LineOfBusinessId, decimal Amount, string Currency, string Status, string Version);
public sealed record PendingDueProjection(Guid Id, Guid AccountId, string AccountNumber, Guid CustomerId, CashFlowDueType DueType, DateOnly DueDate, decimal OutstandingAmount, string Status, string Version);
public sealed record RuleDetailProjection(Guid Id, CashFlowDueType DueType, decimal Percentage, int Order);
public sealed record AppropriationRuleProjection(Guid Id, string Version, Guid CompanyId, Guid LineOfBusinessId, AppropriationDirection Direction, bool FullDue, bool IsActive, IReadOnlyList<RuleDetailProjection> Details);
public sealed record AdvanceAccountProjection(Guid Id, Guid CompanyId, Guid CustomerId, string Currency, bool IsActive, string Version);
public sealed record AuthorizationScope(IReadOnlySet<Guid> CompanyIds, IReadOnlySet<Guid> LineOfBusinessIds, IReadOnlySet<Guid> BranchIds, bool CanAccessAllCustomers);
public sealed record DueReservation(Guid DueId, decimal Amount, string Version);

public interface IReceiptContextPort
{
    Task<ReceiptContext?> GetAsync(Guid receiptId, CancellationToken ct);
    Task<bool> CanAppropriateAsync(ReceiptContext receipt, CancellationToken ct);
}
public interface IPendingDuePort
{
    Task<IReadOnlyList<PendingDueProjection>> GetEligibleAsync(ReceiptContext receipt, CancellationToken ct);
    Task ReserveAsync(IReadOnlyList<DueReservation> reservations, CancellationToken ct);
}
public interface IAppropriationRulePort
{
    Task<IReadOnlyList<AppropriationRuleProjection>> GetActiveAsync(Guid companyId, Guid lineOfBusinessId, CancellationToken ct);
}
public interface IAdvanceAccountPort
{
    Task<AdvanceAccountProjection?> GetActiveAsync(Guid companyId, Guid customerId, CancellationToken ct);
}
public interface IAppropriationScopeResolver
{
    Task<AuthorizationScope> ResolveAsync(ClaimsPrincipal user, CancellationToken ct);
    bool CanAccess(AuthorizationScope scope, ReceiptContext receipt);
}
```

  - [ ] 4.2 CREATE `Finnova.Service/ReceiptAppropriation/Ports/ReceiptAppropriationAdapterRegistration.cs`

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace Finnova.Service.ReceiptAppropriation.Ports;

public static class ReceiptAppropriationAdapterRegistration
{
    public static IServiceCollection AddReceiptAppropriationAdapters(this IServiceCollection services)
    {
        // Replace these registrations with the concrete FINNOVA-61, due-ledger, rule-master,
        // Advance Account, and scope adapters supplied by their owning modules. Do not register
        // in-memory or duplicate receipt/due/rule sources here.
        throw new InvalidOperationException(
            "FINNOVA-62 adapters are unresolved. Register the authoritative Receipt, due-ledger, rule-master, Advance Account, and scope implementations before enabling the feature.");
    }
}
```

- [ ] 5. Persistence model and EF configuration
  - [ ] 5.1 CREATE `Finnova.Repository/Configuration/ReceiptAppropriationConfigurations.cs`

```csharp
using Finnova.Models.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finnova.Repository.Configuration;

public sealed class ReceiptAppropriationConfiguration : IEntityTypeConfiguration<ReceiptAppropriation>
{
    public void Configure(EntityTypeBuilder<ReceiptAppropriation> b)
    {
        b.ToTable("receipt_appropriations"); b.HasKey(x => x.Id);
        b.Property(x => x.Mode).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        foreach (var p in new[] { nameof(ReceiptAppropriation.ReceiptAmount), nameof(ReceiptAppropriation.DueAllocationTotal), nameof(ReceiptAppropriation.AdvanceAccountAmount), nameof(ReceiptAppropriation.ResidualAmount) }) b.Property<decimal>(p).HasPrecision(18, 2);
        b.Property(x => x.RuleDirection).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        b.HasIndex(x => x.ReceiptId).IsUnique(); b.HasIndex(x => x.ClientRequestId).IsUnique();
        b.HasMany(x => x.Allocations).WithOne(x => x.ReceiptAppropriation).HasForeignKey(x => x.ReceiptAppropriationId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.AdvanceAccountAllocation).WithOne(x => x.ReceiptAppropriation).HasForeignKey<AdvanceAccountAllocation>(x => x.ReceiptAppropriationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ReceiptAllocationConfiguration : IEntityTypeConfiguration<ReceiptAllocation>
{
    public void Configure(EntityTypeBuilder<ReceiptAllocation> b)
    {
        b.ToTable("receipt_allocations"); b.HasKey(x => x.Id); b.HasIndex(x => new { x.ReceiptAppropriationId, x.PendingDueId }).IsUnique();
        b.Property(x => x.CashFlowDueType).HasConversion<string>().HasMaxLength(30).IsRequired(); b.Property(x => x.AccountNumber).HasMaxLength(50).IsRequired(); b.Property(x => x.DueRowVersionAtConfirm).HasMaxLength(200).IsRequired();
        foreach (var p in new[] { nameof(ReceiptAllocation.AllocatedAmount), nameof(ReceiptAllocation.OutstandingAmountAtConfirm), nameof(ReceiptAllocation.TargetAmount) }) b.Property<decimal?>(p).HasPrecision(18, 2);
    }
}

public sealed class AdvanceAccountAllocationConfiguration : IEntityTypeConfiguration<AdvanceAccountAllocation>
{
    public void Configure(EntityTypeBuilder<AdvanceAccountAllocation> b)
    {
        b.ToTable("advance_account_allocations"); b.HasKey(x => x.Id); b.HasIndex(x => x.ReceiptAppropriationId).IsUnique(); b.Property(x => x.Currency).HasMaxLength(3).IsRequired(); b.Property(x => x.Amount).HasPrecision(18, 2);
    }
}

public sealed class AppropriationAuditEntryConfiguration : IEntityTypeConfiguration<AppropriationAuditEntry>
{
    public void Configure(EntityTypeBuilder<AppropriationAuditEntry> b)
    {
        b.ToTable("appropriation_audit_entries"); b.HasKey(x => x.Id); b.HasIndex(x => x.ReceiptAppropriationId).IsUnique(); b.HasIndex(x => x.ClientRequestId).IsUnique();
        b.Property(x => x.Mode).HasMaxLength(20).IsRequired(); b.Property(x => x.Actor).HasMaxLength(200).IsRequired(); b.Property(x => x.CorrelationId).HasMaxLength(100).IsRequired();
        foreach (var p in new[] { nameof(AppropriationAuditEntry.DueAllocationTotal), nameof(AppropriationAuditEntry.AdvanceAccountAmount), nameof(AppropriationAuditEntry.ResidualAmount) }) b.Property<decimal>(p).HasPrecision(18, 2);
    }
}
```

  - [ ] 5.2 MODIFY `Finnova.Repository/Context/FinnovaDbContext.cs`

Insert immediately after the existing `public DbSet<DemandAuditEntry> DemandAuditEntries => Set<DemandAuditEntry>();` line and before `protected override void OnModelCreating`:

```csharp
    public DbSet<ReceiptAppropriation> ReceiptAppropriations => Set<ReceiptAppropriation>();
    public DbSet<ReceiptAllocation> ReceiptAllocations => Set<ReceiptAllocation>();
    public DbSet<AdvanceAccountAllocation> AdvanceAccountAllocations => Set<AdvanceAccountAllocation>();
    public DbSet<AppropriationAuditEntry> AppropriationAuditEntries => Set<AppropriationAuditEntry>();
```

The existing top-of-file anchor is:

```csharp
using Finnova.Models.Domain.Entities;
using Microsoft.EntityFrameworkCore;
```

No additional entity namespace is needed. Do not add `DbSet` properties for Receipt, PendingDue, or AppropriationRule because those belong to their authoritative owners.

After the source changes are applied, create the migration from the backend repository root. Do not run this command while creating the spec:

```powershell
dotnet ef migrations add AddReceiptAppropriation -p Finnova.Repository -s Finnova.AccountsService -o Migrations
```

- [ ] 6. Repository interfaces and DI
  - [ ] 6.1 CREATE `Finnova.Repository/Interfaces/IReceiptAppropriationRepository.cs`

```csharp
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Interfaces;

public interface IReceiptAppropriationRepository
{
    Task<ReceiptAppropriation?> FindByReceiptIdAsync(Guid receiptId, CancellationToken ct);
    Task<ReceiptAppropriation?> FindByClientRequestIdAsync(Guid clientRequestId, CancellationToken ct);
    Task AddAsync(ReceiptAppropriation entity, CancellationToken ct);
    Task AddAuditAsync(AppropriationAuditEntry entry, CancellationToken ct);
}
```

  - [ ] 6.2 CREATE `Finnova.Repository/Repositories/ReceiptAppropriationRepository.cs`

```csharp
using Finnova.Models.Domain.Entities;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Finnova.Repository.Repositories;

public sealed class ReceiptAppropriationRepository(FinnovaDbContext db) : IReceiptAppropriationRepository
{
    public Task<ReceiptAppropriation?> FindByReceiptIdAsync(Guid receiptId, CancellationToken ct) => db.ReceiptAppropriations.AsNoTracking().Include(x => x.Allocations).Include(x => x.AdvanceAccountAllocation).SingleOrDefaultAsync(x => x.ReceiptId == receiptId, ct);
    public Task<ReceiptAppropriation?> FindByClientRequestIdAsync(Guid clientRequestId, CancellationToken ct) => db.ReceiptAppropriations.AsNoTracking().Include(x => x.Allocations).Include(x => x.AdvanceAccountAllocation).SingleOrDefaultAsync(x => x.ClientRequestId == clientRequestId, ct);
    public Task AddAsync(ReceiptAppropriation entity, CancellationToken ct) => db.ReceiptAppropriations.AddAsync(entity, ct).AsTask();
    public Task AddAuditAsync(AppropriationAuditEntry entry, CancellationToken ct) => db.AppropriationAuditEntries.AddAsync(entry, ct).AsTask();
}
```

Insert the following registration immediately after the existing `services.AddScoped<IDemandRepository, DemandRepository>();` line in `Finnova.Repository/DependencyInjection.cs`:

```csharp
        services.AddScoped<IReceiptAppropriationRepository, ReceiptAppropriationRepository>();
```

- [ ] 7. Pure allocation calculator
  - [ ] 7.1 CREATE `Finnova.Service/ReceiptAppropriation/Rules/AppropriationCalculator.cs`

```csharp
using Finnova.Models.Domain.Enums;
using Finnova.Service.ReceiptAppropriation.Ports;

namespace Finnova.Service.ReceiptAppropriation.Rules;

public sealed record AllocationLine(Guid DueId, Guid AccountId, string AccountNumber, CashFlowDueType DueType, DateOnly DueDate, decimal OutstandingAmount, decimal AllocatedAmount, Guid? RuleDetailId, int? RuleOrder, decimal? TargetAmount, string DueVersion);
public sealed record CalculationResult(IReadOnlyList<AllocationLine> Lines, decimal DueTotal, decimal Residual, bool Reconciles);

public static class AppropriationCalculator
{
    private static readonly IReadOnlyDictionary<CashFlowDueType, int> Ranks = new Dictionary<CashFlowDueType, int> { [CashFlowDueType.Installment] = 0, [CashFlowDueType.Principal] = 1, [CashFlowDueType.Interest] = 2, [CashFlowDueType.Insurance] = 3, [CashFlowDueType.ODI] = 4, [CashFlowDueType.OtherCharges] = 5 };
    public static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    public static IReadOnlyList<PendingDueProjection> Order(IEnumerable<PendingDueProjection> dues) => dues.Where(x => x.OutstandingAmount > 0m).OrderBy(x => x.AccountNumber, StringComparer.Ordinal).ThenBy(x => Ranks[x.DueType]).ThenBy(x => x.DueDate).ThenBy(x => x.Id).ToArray();

    public static CalculationResult Calculate(decimal receiptAmount, IReadOnlyList<PendingDueProjection> source, AppropriationRuleProjection rule)
    {
        if (receiptAmount <= 0m || Money(receiptAmount) != receiptAmount) throw new ArgumentOutOfRangeException(nameof(receiptAmount));
        if (!rule.IsActive || rule.Details.Count == 0 || rule.Details.Sum(x => x.Percentage) != 100m) throw new InvalidOperationException("The active appropriation rule is invalid.");
        var ordered = Order(source).ToList(); var remaining = Money(receiptAmount); var lines = ordered.ToDictionary(x => x.Id, x => new AllocationLine(x.Id, x.AccountId, x.AccountNumber, x.DueType, x.DueDate, Money(x.OutstandingAmount), 0m, null, null, null, x.Version));
        IEnumerable<RuleDetailProjection> details = rule.Details.OrderBy(x => x.Order);
        IEnumerable<PendingDueProjection> Traverse(RuleDetailProjection d) => rule.Direction == AppropriationDirection.Horizontal ? ordered.Where(x => x.DueType == d.DueType) : ordered;
        foreach (var detail in details)
        {
            var target = Math.Min(remaining, Money(receiptAmount * detail.Percentage / 100m));
            foreach (var due in Traverse(detail).Where(x => x.DueType == detail.DueType))
            {
                var line = lines[due.Id]; var take = Math.Min(target, Math.Min(remaining, line.OutstandingAmount - line.AllocatedAmount));
                if (take <= 0m) continue;
                lines[due.Id] = line with { AllocatedAmount = Money(line.AllocatedAmount + take), RuleDetailId = detail.Id, RuleOrder = detail.Order, TargetAmount = Money(receiptAmount * detail.Percentage / 100m) };
                target = Money(target - take); remaining = Money(remaining - take); if (target == 0m || remaining == 0m) break;
            }
        }
        var result = lines.Values.Where(x => x.AllocatedAmount > 0m).OrderBy(x => x.AccountNumber, StringComparer.Ordinal).ThenBy(x => Ranks[x.DueType]).ThenBy(x => x.DueDate).ThenBy(x => x.DueId).ToArray();
        var dueTotal = Money(result.Sum(x => x.AllocatedAmount)); var residual = Money(receiptAmount - dueTotal);
        return new(result, dueTotal, residual, Money(dueTotal + residual) == Money(receiptAmount));
    }
}
```

This helper is dependency-free apart from the port projection and must be extended, without changing its public contract, to implement the approved Full Due and exact Vertical/Horizontal traversal semantics after the rule owner confirms them. It must reject unknown due types and preserve deterministic ordering.

- [ ] 8. CQRS service slice
  - [ ] 8.1 CREATE `Finnova.Service/ReceiptAppropriation/ReceiptAppropriationHandlers.cs`

```csharp
using System.Security.Claims;
using Finnova.Models.Contracts.ReceiptAppropriation;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.ReceiptAppropriation.Ports;
using Finnova.Service.ReceiptAppropriation.Rules;
using FluentValidation;
using MediatR;

namespace Finnova.Service.ReceiptAppropriation;

public sealed record ShowAppropriationQuery(Guid ReceiptId, int Page, int PageSize, ClaimsPrincipal User) : IRequest<ShowAppropriationResponse>;
public sealed record SuggestAppropriationQuery(Guid ReceiptId, ClaimsPrincipal User, string CorrelationId) : IRequest<AppropriationPreviewResponse>;
public sealed record ConfirmAppropriationCommand(Guid ReceiptId, ConfirmAppropriationRequest Request, ClaimsPrincipal User, string Actor, string CorrelationId) : IRequest<ConfirmedAppropriationResponse>;

public sealed class ShowAppropriationQueryValidator : AbstractValidator<ShowAppropriationQuery>
{
    public ShowAppropriationQueryValidator() { RuleFor(x => x.ReceiptId).NotEmpty(); RuleFor(x => x.Page).InclusiveBetween(1, 100000); RuleFor(x => x.PageSize).InclusiveBetween(1, 500); }
}
public sealed class SuggestAppropriationQueryValidator : AbstractValidator<SuggestAppropriationQuery> { public SuggestAppropriationQueryValidator() => RuleFor(x => x.ReceiptId).NotEmpty(); }
public sealed class ConfirmAppropriationCommandValidator : AbstractValidator<ConfirmAppropriationCommand>
{
    public ConfirmAppropriationCommandValidator() { RuleFor(x => x.ReceiptId).NotEmpty(); RuleFor(x => x.Request.ClientRequestId).NotEmpty(); RuleFor(x => x.Request.Mode).IsInEnum(); RuleForEach(x => x.Request.Allocations).ChildRules(a => a.RuleFor(x => x.DueId).NotEmpty()); }
}

internal static class AppropriationAccess
{
    public static async Task<(ReceiptContext Receipt, IReadOnlyList<PendingDueProjection> Dues)> LoadAsync(Guid id, ClaimsPrincipal user, IReceiptContextPort receipts, IPendingDuePort dues, IAppropriationScopeResolver scope, CancellationToken ct)
    {
        var receipt = await receipts.GetAsync(id, ct) ?? throw new ReceiptAppropriationNotFoundException("Receipt was not found or is not accessible.");
        if (!await receipts.CanAppropriateAsync(receipt, ct)) throw new ReceiptAppropriationConflictException("ERR-APPROP-VALIDATION", "The receipt is not in a status that permits appropriation.");
        var businessScope = await scope.ResolveAsync(user, ct); if (!scope.CanAccess(businessScope, receipt)) throw new ReceiptAppropriationForbiddenException("You are not authorized for this receipt appropriation scope.");
        var pending = await dues.GetEligibleAsync(receipt, ct); return (receipt, pending.Where(x => x.Status == "Collectible" && x.OutstandingAmount > 0m).ToArray());
    }
}

public sealed class ShowAppropriationQueryHandler(IReceiptContextPort receipts, IPendingDuePort dues, IAppropriationScopeResolver scope) : IRequestHandler<ShowAppropriationQuery, ShowAppropriationResponse>
{
    public async Task<ShowAppropriationResponse> Handle(ShowAppropriationQuery q, CancellationToken ct)
    {
        var (r, source) = await AppropriationAccess.LoadAsync(q.ReceiptId, q.User, receipts, dues, scope, ct); var ordered = AppropriationCalculator.Order(source); var items = ordered.Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).Select(x => new ReceiptAppropriationDueResponse(x.Id, x.AccountId, x.AccountNumber, x.DueType, x.DueDate, x.OutstandingAmount, 0m, x.OutstandingAmount, "INR", x.Version)).ToArray();
        var totalPages = Math.Max(1, (int)Math.Ceiling(ordered.Count / (double)q.PageSize)); return new(r.Id, r.CustomerId, r.CompanyId, r.LineOfBusinessId, r.Amount, items, ordered.Count, q.Page, q.PageSize, totalPages, new(r.Amount, 0m, 0m, r.Amount, true), r.Version, string.Join('|', ordered.Select(x => $"{x.Id}:{x.Version}")));
    }
}

public sealed class SuggestAppropriationQueryHandler(IReceiptContextPort receipts, IPendingDuePort dues, IAppropriationRulePort rules, IAppropriationScopeResolver scope) : IRequestHandler<SuggestAppropriationQuery, AppropriationPreviewResponse>
{
    public async Task<AppropriationPreviewResponse> Handle(SuggestAppropriationQuery q, CancellationToken ct)
    {
        var (r, source) = await AppropriationAccess.LoadAsync(q.ReceiptId, q.User, receipts, dues, scope, ct); var matches = await rules.GetActiveAsync(r.CompanyId, r.LineOfBusinessId, ct); if (matches.Count == 0) throw new ReceiptAppropriationRuleException("No active appropriation rule is configured."); if (matches.Count != 1) throw new ReceiptAppropriationRuleException("Multiple active appropriation rules match the receipt."); var rule = matches[0];
        CalculationResult calc; try { calc = AppropriationCalculator.Calculate(r.Amount, source, rule); } catch (InvalidOperationException ex) { throw new ReceiptAppropriationRuleException(ex.Message); }
        var lines = calc.Lines.Select(x => new ReceiptAppropriationDueResponse(x.DueId, x.AccountId, x.AccountNumber, x.DueType, x.DueDate, x.OutstandingAmount, x.AllocatedAmount, x.OutstandingAmount - x.AllocatedAmount, "INR", x.DueVersion, x.RuleDetailId, x.RuleOrder, x.TargetAmount)).ToArray(); var summary = new AppropriationRuleSummary(rule.Id, rule.Version, rule.Direction, rule.FullDue, rule.Details.OrderBy(x => x.Order).Select(x => new AppropriationRuleDetailResponse(x.Id, x.DueType, x.Percentage, x.Order)).ToArray()); return new(q.ReceiptId, r.CustomerId, r.CompanyId, r.LineOfBusinessId, r.Amount, AppropriationMode.Suggest, lines, summary, new(r.Amount, calc.DueTotal, calc.Residual, calc.Residual, calc.Reconciles), r.Version, rule.Version, string.Join('|', source.Select(x => $"{x.Id}:{x.Version}")), DateTime.UtcNow, q.CorrelationId);
    }
}

public sealed class ConfirmAppropriationCommandHandler(IReceiptContextPort receipts, IPendingDuePort dues, IAppropriationRulePort rules, IAdvanceAccountPort advance, IAppropriationScopeResolver scope, IReceiptAppropriationRepository repository, IApplicationUnitOfWork uow) : IRequestHandler<ConfirmAppropriationCommand, ConfirmedAppropriationResponse>
{
    public async Task<ConfirmedAppropriationResponse> Handle(ConfirmAppropriationCommand c, CancellationToken ct)
    {
        var existing = await repository.FindByClientRequestIdAsync(c.Request.ClientRequestId, ct); if (existing is not null) return Map(existing, c.CorrelationId);
        var (r, source) = await AppropriationAccess.LoadAsync(c.ReceiptId, c.User, receipts, dues, scope, ct); var byId = source.ToDictionary(x => x.Id); var rows = c.Request.Allocations.Where(x => x.AllocatedAmount > 0m).ToArray(); if (rows.Any(x => !byId.ContainsKey(x.DueId) || x.AllocatedAmount < 0m || AppropriationCalculator.Money(x.AllocatedAmount) != x.AllocatedAmount || x.AllocatedAmount > byId[x.DueId].OutstandingAmount)) throw new ReceiptAppropriationReconciliationException("An allocation exceeds the current pending due balance.");
        var dueTotal = AppropriationCalculator.Money(rows.Sum(x => x.AllocatedAmount)); if (dueTotal > r.Amount) throw new ReceiptAppropriationReconciliationException("Total allocated amount exceeds Receipt Document Amount"); var residual = AppropriationCalculator.Money(r.Amount - dueTotal); AdvanceAccountProjection? account = null; if (residual > 0m) { if (!c.Request.AcceptResidualTransfer) throw new ReceiptAppropriationAdvanceAccountException("Residual transfer to the Advance Account must be accepted."); account = await advance.GetActiveAsync(r.CompanyId, r.CustomerId, ct) ?? throw new ReceiptAppropriationAdvanceAccountException("The Advance Account is not configured."); }
        var entity = new ReceiptAppropriation { ReceiptId = r.Id, ClientRequestId = c.Request.ClientRequestId, Mode = c.Request.Mode, ReceiptAmount = r.Amount, DueAllocationTotal = dueTotal, AdvanceAccountAmount = residual, ResidualAmount = residual, CustomerId = r.CustomerId, CompanyId = r.CompanyId, LineOfBusinessId = r.LineOfBusinessId, ConfirmedBy = c.Actor, ConfirmedAtUtc = DateTime.UtcNow };
        foreach (var row in rows) { var due = byId[row.DueId]; entity.Allocations.Add(new ReceiptAllocation { PendingDueId = due.Id, AccountId = due.AccountId, AccountNumber = due.AccountNumber, CustomerId = due.CustomerId, CashFlowDueType = due.DueType, DueDate = due.DueDate, AllocatedAmount = row.AllocatedAmount, OutstandingAmountAtConfirm = due.OutstandingAmount, DueRowVersionAtConfirm = due.Version }); }
        if (account is not null) entity.AdvanceAccountAllocation = new AdvanceAccountAllocation { AdvanceAccountId = account.Id, Amount = residual, CompanyId = r.CompanyId, CustomerId = r.CustomerId, Currency = "INR" };
        await using var tx = await uow.BeginTransactionAsync(ct); await dues.ReserveAsync(entity.Allocations.Select(x => new DueReservation(x.PendingDueId, x.AllocatedAmount, x.DueRowVersionAtConfirm)).ToArray(), ct); await repository.AddAsync(entity, ct); var audit = new AppropriationAuditEntry { ReceiptId = r.Id, ReceiptAppropriationId = entity.Id, ClientRequestId = entity.ClientRequestId, Mode = entity.Mode.ToString(), Actor = c.Actor, CorrelationId = c.CorrelationId, DueAllocationTotal = dueTotal, AdvanceAccountAmount = residual, ResidualAmount = residual }; await repository.AddAuditAsync(audit, ct); await uow.SaveChangesAsync(ct); return Map(entity, c.CorrelationId, audit.Id);
    }
    private static ConfirmedAppropriationResponse Map(ReceiptAppropriation x, string correlation, Guid? auditId = null) => new(x.Id, x.ReceiptId, x.ClientRequestId, x.Mode, x.Allocations.Select(a => new ReceiptAppropriationDueResponse(a.PendingDueId, a.AccountId, a.AccountNumber, a.CashFlowDueType, a.DueDate, a.OutstandingAmountAtConfirm, a.AllocatedAmount, a.OutstandingAmountAtConfirm - a.AllocatedAmount, "INR", a.DueRowVersionAtConfirm)).ToArray(), new(x.ReceiptAmount, x.DueAllocationTotal, x.AdvanceAccountAmount, x.ResidualAmount, true), x.ConfirmedAtUtc, x.Status, x.ConfirmedBy, auditId, correlation);
}
```

  - [ ] 8.2 MODIFY `Finnova.Service/DependencyInjection.cs`

Insert the following using after the existing `using Finnova.Service.DemandProcessing.Processing;` line:

```csharp
using Finnova.Service.ReceiptAppropriation.Ports;
```

Insert the following registrations immediately before `return services;` in `AddFinnovaServices` after the existing demand registrations:

```csharp
        services.AddScoped<Finnova.Service.ReceiptAppropriation.Rules.AppropriationCalculator>();
        services.AddReceiptAppropriationAdapters();
```

The adapter registration intentionally fails until the authoritative adapters are supplied. Replace only the body of `AddReceiptAppropriationAdapters` in Task 4.2 with those concrete registrations before enabling the host.

  - [ ] 8.3 MODIFY `Finnova.Repository/DependencyInjection.cs`

Use the existing import anchor:

```csharp
using Finnova.Repository.Repositories;
```

and the existing registration anchor:

```csharp
        services.AddScoped<IDemandRepository, DemandRepository>();
```

The insertion is the registration shown in Task 6.2. `IApplicationUnitOfWork` remains the transaction boundary; repositories must not call `SaveChangesAsync`.

- [ ] 9. HTTP host, middleware, and gateway
  - [ ] 9.1 CREATE `Finnova.AccountsService/Controllers/ReceiptAppropriationController.cs`

```csharp
using System.Security.Claims;
using Finnova.Models.Contracts.ReceiptAppropriation;
using Finnova.Service.ReceiptAppropriation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finnova.AccountsService.Controllers;

[ApiController, Route("api/receipt-appropriation")]
public sealed class ReceiptAppropriationController(IMediator mediator) : ControllerBase
{
    private string Actor => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? User.Identity?.Name ?? "authenticated-user";
    private string Correlation => Request.Headers.TryGetValue("X-Correlation-Id", out var value) && !string.IsNullOrWhiteSpace(value) ? value.ToString() : HttpContext.TraceIdentifier;

    [HttpGet("{receiptId:guid}/dues"), Authorize(Policy = "ReceiptAppropriationRead")]
    public async Task<ActionResult<ShowAppropriationResponse>> Show(Guid receiptId, [FromQuery] int page = 1, [FromQuery] int pageSize = 100, CancellationToken ct = default) => Ok(await mediator.Send(new ShowAppropriationQuery(receiptId, page, pageSize, User), ct));
    [HttpPost("{receiptId:guid}/suggest"), Authorize(Policy = "ReceiptAppropriationRead")]
    public async Task<ActionResult<AppropriationPreviewResponse>> Suggest(Guid receiptId, CancellationToken ct) => Ok(await mediator.Send(new SuggestAppropriationQuery(receiptId, User, Correlation), ct));
    [HttpPost("{receiptId:guid}/confirm"), Authorize(Policy = "ReceiptAppropriationConfirm")]
    public async Task<ActionResult<ConfirmedAppropriationResponse>> Confirm(Guid receiptId, ConfirmAppropriationRequest request, CancellationToken ct) => Ok(await mediator.Send(new ConfirmAppropriationCommand(receiptId, request, User, Actor, Correlation), ct));
}
```

  - [ ] 9.2 MODIFY `Finnova.AccountsService/Program.cs`

Insert these policies inside the existing `builder.Services.AddAuthorization(options => { ... });` block, immediately after the existing `DemandPdfExport` policy:

```csharp
    options.AddPolicy("ReceiptAppropriationRead", p => p.RequireAuthenticatedUser().RequireAssertion(ctx => ctx.User.IsInRole("BranchCollectionOfficer") || ctx.User.IsInRole("SystemAdmin")));
    options.AddPolicy("ReceiptAppropriationConfirm", p => p.RequireAuthenticatedUser().RequireAssertion(ctx => ctx.User.IsInRole("BranchCollectionOfficer") || ctx.User.IsInRole("SystemAdmin")));
```

The existing middleware/authentication order remains unchanged. Do not accept Company, LOB, Branch, or customer scope from the request body as a privilege grant.

  - [ ] 9.3 MODIFY `Finnova.AccountsService/Middlewares/ExceptionHandlingMiddleware.cs`

Insert the following typed cases immediately before the existing `FluentValidation.ValidationException v =>` case in the `ex switch` expression:

```csharp
                Finnova.Models.Domain.Exceptions.ReceiptAppropriationException a => (a.ErrorCode switch
                {
                    "ERR-APPROP-NOT-FOUND" => StatusCodes.Status404NotFound,
                    "ERR-APPROP-403" => StatusCodes.Status403Forbidden,
                    "ERR-APPROP-RULE-CONFIGURATION" or "ERR-APPROP-ADVANCE-ACCOUNT" or "ERR-APPROP-RECONCILIATION" => StatusCodes.Status422UnprocessableEntity,
                    "ERR-APPROP-ALREADY-CONFIRMED" or "ERR-APPROP-IDEMPOTENCY-CONFLICT" or "ERR-APPROP-STALE-DATA" or "ERR-APPROP-CONCURRENCY" => StatusCodes.Status409Conflict,
                    "ERR-APPROP-DEPENDENCY" => StatusCodes.Status503ServiceUnavailable,
                    _ => StatusCodes.Status400BadRequest
                }, a.ErrorCode, a.Message),
```

Preserve the existing `code`, `message`, and `correlationId` ProblemDetails extensions. Do not return SQL messages, stack traces, JWT values, or secrets.

  - [ ] 9.4 MODIFY `Finnova.ApiGateway/appsettings.json`

Insert these two route objects inside `ReverseProxy.Routes`, immediately before the existing `ua-route` object. The exact existing anchor is:

```json
      "ua-route": {
        "ClusterId": "ua-cluster",
```

Insertion:

```json
      "ua-receipt-appropriation-alias-route": {
        "ClusterId": "accounts-cluster",
        "Match": { "Path": "/api/ua/api/receipt-appropriation/{**catch-all}" },
        "Transforms": [
          { "PathRemovePrefix": "/api/ua/api" },
          { "PathPrefix": "/api" }
        ]
      },
      "ua-receipt-appropriation-alias-root": {
        "ClusterId": "accounts-cluster",
        "Match": { "Path": "/api/ua/api/receipt-appropriation" },
        "Transforms": [
          { "PathRemovePrefix": "/api/ua/api" },
          { "PathPrefix": "/api" }
        ]
      },
```

The direct Accounts route remains available through `accounts-route`; the UI uses the existing gateway-prefixed axios base and the alias above.

- [ ] 10. Backend automated tests
  - [ ] 10.1 CREATE `Finnova.Tests/ReceiptAppropriation/AppropriationCalculatorTests.cs`

```csharp
using FsCheck;
using FsCheck.Xunit;
using Finnova.Models.Domain.Enums;
using Finnova.Service.ReceiptAppropriation.Ports;
using Finnova.Service.ReceiptAppropriation.Rules;
using Xunit;

namespace Finnova.Tests.ReceiptAppropriation;

public sealed class AppropriationCalculatorTests
{
    [Property(MaxTest = 100)]
    public Property AllocationsAreCappedAndReconciled()
    {
        return Prop.ForAll(Arb.Default.NonNegativeInt().Generator.Select(x => Math.Max(1, x.Value % 100000)), amount =>
        {
            var receipt = Math.Round(amount, 2); var due = new PendingDueProjection(Guid.NewGuid(), Guid.NewGuid(), "ACC-1", Guid.NewGuid(), CashFlowDueType.Installment, new DateOnly(2026, 1, 1), receipt / 2m, "Collectible", "v1"); var rule = new AppropriationRuleProjection(Guid.NewGuid(), "v1", Guid.NewGuid(), Guid.NewGuid(), AppropriationDirection.Vertical, true, true, new[] { new RuleDetailProjection(Guid.NewGuid(), CashFlowDueType.Installment, 100m, 1) }); var result = AppropriationCalculator.Calculate(receipt, new[] { due }, rule); return (result.Lines.All(x => x.AllocatedAmount >= 0m && x.AllocatedAmount <= x.OutstandingAmount) && result.Reconciles).ToProperty();
        });
    }

    [Fact]
    public void OrdersByAccountTypeDateAndId()
    {
        var account = Guid.NewGuid(); var first = new PendingDueProjection(Guid.NewGuid(), account, "A", Guid.NewGuid(), CashFlowDueType.ODI, new DateOnly(2026, 1, 1), 1m, "Collectible", "v1"); var second = new PendingDueProjection(Guid.NewGuid(), account, "A", Guid.NewGuid(), CashFlowDueType.Installment, new DateOnly(2026, 1, 1), 1m, "Collectible", "v1"); Assert.Equal(CashFlowDueType.Installment, AppropriationCalculator.Order(new[] { first, second })[0].DueType);
    }

    [Fact]
    public void UsesTheThirtyFiveThousandExample()
    {
        var customer = Guid.NewGuid(); var dues = new[] { new PendingDueProjection(Guid.NewGuid(), Guid.NewGuid(), "ACC-889021", customer, CashFlowDueType.Installment, new DateOnly(2026, 1, 1), 30000m, "Collectible", "1"), new PendingDueProjection(Guid.NewGuid(), Guid.NewGuid(), "ACC-889021", customer, CashFlowDueType.ODI, new DateOnly(2026, 1, 2), 15000m, "Collectible", "1") }; var rule = new AppropriationRuleProjection(Guid.NewGuid(), "1", Guid.NewGuid(), Guid.NewGuid(), AppropriationDirection.Vertical, true, true, new[] { new RuleDetailProjection(Guid.NewGuid(), CashFlowDueType.Installment, 80m, 1), new RuleDetailProjection(Guid.NewGuid(), CashFlowDueType.ODI, 20m, 2) }); var result = AppropriationCalculator.Calculate(35000m, dues, rule); Assert.Equal(28000m, result.Lines.Single(x => x.DueType == CashFlowDueType.Installment).AllocatedAmount); Assert.Equal(7000m, result.Lines.Single(x => x.DueType == CashFlowDueType.ODI).AllocatedAmount);
    }
}
```

  - [ ] 10.2 CREATE `Finnova.Tests/ReceiptAppropriation/ReceiptAppropriationHandlerTests.cs`

```csharp
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Models.Contracts.ReceiptAppropriation;
using Finnova.Service.ReceiptAppropriation;
using Finnova.Service.ReceiptAppropriation.Ports;
using Moq;
using System.Security.Claims;
using Xunit;

namespace Finnova.Tests.ReceiptAppropriation;

public sealed class ReceiptAppropriationHandlerTests
{
    [Fact]
    public async Task ConfirmRejectsTotalAboveReceiptWithoutWriting()
    {
        var receipt = new ReceiptContext(Guid.NewGuid(), Guid.NewGuid(), "CUST-1", Guid.NewGuid(), Guid.NewGuid(), 35000m, "INR", "PendingAuthorization", "1"); var receiptPort = new Mock<IReceiptContextPort>(); receiptPort.Setup(x => x.GetAsync(receipt.Id, It.IsAny<CancellationToken>())).ReturnsAsync(receipt); receiptPort.Setup(x => x.CanAppropriateAsync(receipt, It.IsAny<CancellationToken>())).ReturnsAsync(true); var due = new PendingDueProjection(Guid.NewGuid(), Guid.NewGuid(), "ACC-1", receipt.CustomerId, CashFlowDueType.Installment, new DateOnly(2026, 1, 1), 40000m, "Collectible", "1"); var duePort = new Mock<IPendingDuePort>(); duePort.Setup(x => x.GetEligibleAsync(receipt, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { due }); var scope = new Mock<IAppropriationScopeResolver>(); scope.Setup(x => x.ResolveAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>())).ReturnsAsync(new AuthorizationScope(new[] { receipt.CompanyId }.ToHashSet(), new[] { receipt.LineOfBusinessId }.ToHashSet(), new HashSet<Guid>(), true)); scope.Setup(x => x.CanAccess(It.IsAny<AuthorizationScope>(), receipt)).Returns(true); var request = new ConfirmAppropriationRequest(Guid.NewGuid(), AppropriationMode.Manual, new[] { new ConfirmAllocationRequest(due.Id, 35000.01m) }, false); var handler = new ConfirmAppropriationCommandHandler(receiptPort.Object, duePort.Object, Mock.Of<IAppropriationRulePort>(), Mock.Of<IAdvanceAccountPort>(), scope.Object, Mock.Of<Finnova.Repository.Interfaces.IReceiptAppropriationRepository>(), Mock.Of<Finnova.Repository.Interfaces.IApplicationUnitOfWork>()); await Assert.ThrowsAsync<ReceiptAppropriationReconciliationException>(() => handler.Handle(new ConfirmAppropriationCommand(receipt.Id, request, new ClaimsPrincipal(new ClaimsIdentity()), "tester", "corr"), CancellationToken.None)); duePort.Verify(x => x.ReserveAsync(It.IsAny<IReadOnlyList<DueReservation>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
```

Tests must be expanded to cover properties 1-6 from `design.md`, at least 100 FsCheck iterations per property, the empty-due case, invalid precision, missing/ambiguous rules, missing Advance Account, stale versions, idempotency, zero-row omission, rollback, and the exact FINNOVA-62 examples. Keep tests English-only and INR-only.

- [ ] 11. Frontend model and service boundary
  - [ ] 11.1 CREATE `src/models/receiptAppropriation.model.ts`

```typescript
export type AppropriationMode='Manual'|'Suggest';
export type CashFlowDueType='Installment'|'Principal'|'Interest'|'Insurance'|'ODI'|'OtherCharges';
export type AppropriationDirection='Vertical'|'Horizontal';
export interface PendingDueRow { dueId:string; accountId:string; accountNumber:string; cashFlowDueType:CashFlowDueType; dueDate:string; outstandingAmount:number; allocatedAmount:number; remainingAmount:number; currency:'INR'; dueVersion:string; ruleDetailId?:string; ruleOrder?:number; targetAmount?:number; }
export interface RuleDetail { id:string; cashFlowDueType:CashFlowDueType; percentage:number; order:number; }
export interface RuleSummary { ruleId:string; version:string; direction:AppropriationDirection; fullDue:boolean; details:RuleDetail[]; }
export interface Totals { receiptAmount:number; dueAllocationTotal:number; advanceAccountAmount:number; residualAmount:number; reconciles:boolean; }
export interface ShowAppropriationResponse { receiptId:string; customerId:string; companyId:string; lineOfBusinessId:string; receiptDocumentAmount:number; data:PendingDueRow[]; total:number; page:number; pageSize:number; totalPages:number; totals:Totals; receiptVersion:string; dueFingerprint:string; }
export interface AppropriationPreview { receiptId:string; customerId:string; companyId:string; lineOfBusinessId:string; receiptDocumentAmount:number; mode:'Suggest'; allocations:PendingDueRow[]; rule?:RuleSummary; totals:Totals; receiptVersion:string; ruleVersion:string; dueFingerprint:string; generatedAtUtc:string; correlationId:string; }
export interface ConfirmAllocationRequest { dueId:string; allocatedAmount:number; }
export interface ConfirmAppropriationRequest { clientRequestId:string; mode:AppropriationMode; allocations:ConfirmAllocationRequest[]; acceptResidualTransfer:boolean; previewDueFingerprint?:string; previewRuleVersion?:string; }
export interface ConfirmedAppropriation { appropriationId:string; receiptId:string; clientRequestId:string; mode:AppropriationMode; allocations:PendingDueRow[]; totals:Totals; confirmedAtUtc:string; status:'Confirmed'; actor:string; auditId?:string; correlationId:string; }
export interface IReceiptAppropriationService { show(receiptId:string,page?:number,pageSize?:number):Promise<ShowAppropriationResponse>; suggest(receiptId:string):Promise<AppropriationPreview>; confirm(receiptId:string,request:ConfirmAppropriationRequest):Promise<ConfirmedAppropriation>; }
```

  - [ ] 11.2 CREATE `src/services/interfaces/receiptAppropriation.interface.ts`

```typescript
import type {IReceiptAppropriationService} from '../../models/receiptAppropriation.model';
export type {IReceiptAppropriationService};
```

  - [ ] 11.3 CREATE `src/services/real/receiptAppropriation.real.ts`

```typescript
import api from '../api';
import type {AppropriationPreview,ConfirmedAppropriation,ConfirmAppropriationRequest,IReceiptAppropriationService,ShowAppropriationResponse} from '../../models/receiptAppropriation.model';
class ReceiptAppropriationRealService implements IReceiptAppropriationService {
  private readonly base='/receipt-appropriation';
  show(receiptId:string,page=1,pageSize=100){return api.get<ShowAppropriationResponse>(`${this.base}/${receiptId}/dues`,{params:{page,pageSize}}).then(r=>r.data);}
  suggest(receiptId:string){return api.post<AppropriationPreview>(`${this.base}/${receiptId}/suggest`,{}).then(r=>r.data);}
  confirm(receiptId:string,request:ConfirmAppropriationRequest){return api.post<ConfirmedAppropriation>(`${this.base}/${receiptId}/confirm`,request).then(r=>r.data);}
}
export const receiptAppropriationRealService:IReceiptAppropriationService=new ReceiptAppropriationRealService();
```

  - [ ] 11.4 CREATE `src/services/mock/receiptAppropriation.mock.ts`

```typescript
import type {AppropriationPreview,ConfirmedAppropriation,ConfirmAppropriationRequest,IReceiptAppropriationService,PendingDueRow,ShowAppropriationResponse} from '../../models/receiptAppropriation.model';
const receiptId='00000000-0000-0000-0000-000000000061'; const customerId='00000000-0000-0000-0000-000000000100'; const due:PendingDueRow={dueId:'00000000-0000-0000-0000-000000000001',accountId:'00000000-0000-0000-0000-000000000002',accountNumber:'ACC-889021',cashFlowDueType:'Installment',dueDate:'2026-01-01',outstandingAmount:30000,allocatedAmount:0,remainingAmount:30000,currency:'INR',dueVersion:'1'}; const saved=new Map<string,ConfirmedAppropriation>();
class ReceiptAppropriationMockService implements IReceiptAppropriationService { async show(id:string):Promise<ShowAppropriationResponse>{if(id!==receiptId)throw new Error('Receipt was not found or is not accessible.');return{receiptId:id,customerId,companyId:'00000000-0000-0000-0000-000000000003',lineOfBusinessId:'00000000-0000-0000-0000-000000000004',receiptDocumentAmount:35000,data:[due],total:1,page:1,pageSize:100,totalPages:1,totals:{receiptAmount:35000,dueAllocationTotal:0,advanceAccountAmount:0,residualAmount:35000,reconciles:true},receiptVersion:'1',dueFingerprint:`${due.dueId}:${due.dueVersion}`};} async suggest(id:string):Promise<AppropriationPreview>{const s=await this.show(id);const allocated={...due,allocatedAmount:28000,remainingAmount:2000};return{...s,mode:'Suggest',allocations:[allocated],rule:{ruleId:'00000000-0000-0000-0000-000000000005',version:'1',direction:'Vertical',fullDue:true,details:[{id:'00000000-0000-0000-0000-000000000006',cashFlowDueType:'Installment',percentage:80,order:1}]},totals:{receiptAmount:35000,dueAllocationTotal:28000,advanceAccountAmount:7000,residualAmount:7000,reconciles:true},ruleVersion:'1',generatedAtUtc:new Date().toISOString(),correlationId:'mock'};} async confirm(id:string,r:ConfirmAppropriationRequest):Promise<ConfirmedAppropriation>{if(r.allocations.reduce((a,x)=>a+x.allocatedAmount,0)>35000)throw new Error('Total allocated amount exceeds Receipt Document Amount');if(!r.acceptResidualTransfer)throw new Error('Residual transfer to the Advance Account must be accepted.');const result:ConfirmedAppropriation={appropriationId:crypto.randomUUID(),receiptId:id,clientRequestId:r.clientRequestId,mode:r.mode,allocations:[{...due,allocatedAmount:r.allocations[0]?.allocatedAmount??0,remainingAmount:due.outstandingAmount-(r.allocations[0]?.allocatedAmount??0)}],totals:{receiptAmount:35000,dueAllocationTotal:r.allocations[0]?.allocatedAmount??0,advanceAccountAmount:35000-(r.allocations[0]?.allocatedAmount??0),residualAmount:35000-(r.allocations[0]?.allocatedAmount??0),reconciles:true},confirmedAtUtc:new Date().toISOString(),status:'Confirmed',actor:'mock-user',correlationId:'mock'};const prior=saved.get(r.clientRequestId);if(prior)return prior;saved.set(r.clientRequestId,result);return result;}}
export const receiptAppropriationMockService:IReceiptAppropriationService=new ReceiptAppropriationMockService();
```

  - [ ] 11.5 CREATE `src/services/receiptAppropriation.service.ts`

```typescript
import {receiptAppropriationMockService} from './mock/receiptAppropriation.mock'; import {receiptAppropriationRealService} from './real/receiptAppropriation.real'; import type {IReceiptAppropriationService} from './interfaces/receiptAppropriation.interface';
const useMock=import.meta.env.VITE_USE_MOCK_API==='true';
export const receiptAppropriationService:IReceiptAppropriationService=useMock?receiptAppropriationMockService:receiptAppropriationRealService;
```

- [ ] 12. Frontend page and components
  - [ ] 12.1 CREATE `src/components/receiptAppropriation/AppropriationTotals.tsx`

```tsx
import {Stack,Typography} from '@mui/material'; import type {Totals} from '../../models/receiptAppropriation.model';
export default function AppropriationTotals({totals}:{totals:Totals}){return <Stack spacing={.5}><Typography>Receipt Document Amount: ₹{totals.receiptAmount.toFixed(2)}</Typography><Typography>Total Allocated Amount: ₹{totals.dueAllocationTotal.toFixed(2)}</Typography><Typography>Advance Account Amount: ₹{totals.advanceAccountAmount.toFixed(2)}</Typography><Typography>Residual Amount: ₹{totals.residualAmount.toFixed(2)}</Typography></Stack>}
```

  - [ ] 12.2 CREATE `src/components/receiptAppropriation/PendingDueGrid.tsx`

```tsx
import {DataGrid,GridColDef} from '@mui/x-data-grid'; import {TextField} from '@mui/material'; import type {AppropriationMode,PendingDueRow} from '../../models/receiptAppropriation.model';
export default function PendingDueGrid({rows,mode,onAmount}:{rows:PendingDueRow[];mode:AppropriationMode;onAmount:(id:string,value:number)=>void}){const columns:GridColDef<PendingDueRow>[]=[{field:'accountNumber',headerName:'Account Number',width:150},{field:'cashFlowDueType',headerName:'Cash Flow Due Type',width:160},{field:'dueDate',headerName:'Due Date',width:120},{field:'outstandingAmount',headerName:'Outstanding Amount',width:160},{field:'allocatedAmount',headerName:'Allocated Amount',width:170,renderCell:p=>mode==='Manual'?<TextField size="small" type="number" inputProps={{step:'0.01',min:0,max:p.row.outstandingAmount}} value={p.row.allocatedAmount.toFixed(2)} onChange={e=>onAmount(p.row.dueId,Number(e.target.value||0))}/>:p.row.allocatedAmount.toFixed(2)},{field:'remainingAmount',headerName:'Remaining Balance',width:160}];return <DataGrid autoHeight disableRowSelectionOnClick rows={rows.map(x=>({...x,id:x.dueId}))} columns={columns} getRowId={x=>x.dueId} pageSizeOptions={[100]}/>}
```

  - [ ] 12.3 CREATE `src/pages/ReceiptAppropriation.tsx`

```tsx
import {Alert,Button,Checkbox,FormControlLabel,Radio,RadioGroup,Stack,Typography} from '@mui/material'; import {useCallback,useEffect,useMemo,useState} from 'react'; import {useNavigate,useParams} from 'react-router-dom'; import {receiptAppropriationService} from '../services/receiptAppropriation.service'; import type {AppropriationMode,PendingDueRow,Totals} from '../models/receiptAppropriation.model'; import PendingDueGrid from '../components/receiptAppropriation/PendingDueGrid'; import AppropriationTotals from '../components/receiptAppropriation/AppropriationTotals';
const zero=(id:string):string=>id; export default function ReceiptAppropriation(){const {receiptId}=useParams<{receiptId:string}>();const navigate=useNavigate();const [mode,setMode]=useState<AppropriationMode>('Manual');const [rows,setRows]=useState<PendingDueRow[]>([]);const [totals,setTotals]=useState<Totals>({receiptAmount:0,dueAllocationTotal:0,advanceAccountAmount:0,residualAmount:0,reconciles:false});const [loading,setLoading]=useState(false);const [error,setError]=useState('');const [acceptResidual,setAcceptResidual]=useState(false);const [confirmed,setConfirmed]=useState(false);const recompute=useCallback((next:PendingDueRow[])=>{const due=next.reduce((a,x)=>a+x.allocatedAmount,0);const receipt=totals.receiptAmount;setRows(next.map(x=>({...x,remainingAmount:Number((x.outstandingAmount-x.allocatedAmount).toFixed(2))})));setTotals(t=>({...t,dueAllocationTotal:Number(due.toFixed(2)),residualAmount:Number((receipt-due).toFixed(2)),advanceAccountAmount:Number((receipt-due).toFixed(2)),reconciles:due<=receipt}));},[totals.receiptAmount]);const show=useCallback(async()=>{if(!receiptId)return;setLoading(true);setError('');try{const x=await receiptAppropriationService.show(receiptId);setRows(x.data);setTotals(x.totals);}catch(e){setError(e instanceof Error?e.message:'Unable to load pending dues.');}finally{setLoading(false);}},[receiptId]);useEffect(()=>{void show();},[show]);const setAmount=(id:string,value:number)=>recompute(rows.map(x=>x.dueId===id?{...x,allocatedAmount:Number(value.toFixed(2))}:x));const suggest=async()=>{if(!receiptId)return;setLoading(true);setError('');try{const x=await receiptAppropriationService.suggest(receiptId);setMode('Suggest');setRows(x.allocations);setTotals(x.totals);}catch(e){setError(e instanceof Error?e.message:'Unable to calculate suggestion.');}finally{setLoading(false);}};const confirm=async()=>{if(!receiptId||totals.dueAllocationTotal>totals.receiptAmount||(!acceptResidual&&totals.residualAmount>0))return;setLoading(true);setError('');try{await receiptAppropriationService.confirm(receiptId,{clientRequestId:crypto.randomUUID(),mode,allocations:rows.filter(x=>x.allocatedAmount>0).map(x=>({dueId:x.dueId,allocatedAmount:x.allocatedAmount})),acceptResidualTransfer:acceptResidual});setConfirmed(true);}catch(e){setError(e instanceof Error?e.message:'Unable to confirm appropriation.');}finally{setLoading(false);}};const disabled=loading||confirmed||totals.dueAllocationTotal>totals.receiptAmount||(!acceptResidual&&totals.residualAmount>0);return <Stack spacing={2} sx={{p:3}}><Typography variant="h4">Receipt Appropriation</Typography>{error&&<Alert severity="error">{error}</Alert>}<RadioGroup row value={mode} onChange={e=>setMode(e.target.value as AppropriationMode)}><FormControlLabel value="Manual" control={<Radio/>} label="Manual"/><FormControlLabel value="Suggest" control={<Radio/>} label="Suggest"/></RadioGroup><Stack direction="row" spacing={1}><Button onClick={()=>void show()} disabled={loading}>Show</Button><Button onClick={()=>void suggest()} disabled={loading}>Suggest</Button><Button onClick={()=>setMode('Manual')} disabled={loading}>Manual</Button></Stack><PendingDueGrid rows={rows} mode={mode} onAmount={setAmount}/><AppropriationTotals totals={totals}/>{totals.residualAmount>0&&<FormControlLabel control={<Checkbox checked={acceptResidual} onChange={e=>setAcceptResidual(e.target.checked)}/>} label="Transfer residual to Advance Account"/>}<Button variant="contained" onClick={()=>void confirm()} disabled={disabled}>Confirm</Button>{confirmed&&<Alert severity="success">Receipt appropriation confirmed.</Alert>}<Button onClick={()=>navigate('/dashboard')}>Return to receipt workflow</Button></Stack>}
void zero;
```

  - [ ] 12.4 CREATE `src/components/receiptAppropriation/index.ts`

```typescript
export {default as PendingDueGrid} from './PendingDueGrid'; export {default as AppropriationTotals} from './AppropriationTotals';
```

- [ ] 13. Frontend route and navigation
  - [ ] 13.1 MODIFY `src/App.tsx`

Add this import immediately after the existing `import DemandSpooling from './pages/DemandSpooling';` anchor:

```tsx
import ReceiptAppropriation from './pages/ReceiptAppropriation';
```

Insert this route immediately after the existing demand-processing routes inside the protected `<Route>`:

```tsx
                  <Route path="/receipt-appropriation/:receiptId" element={<ReceiptAppropriation />} />
```

  - [ ] 13.2 MODIFY `src/components/Layout.tsx`

Add this import immediately after the existing `import RequestQuoteIcon from '@mui/icons-material/RequestQuote';` anchor:

```tsx
import ReceiptLongIcon from '@mui/icons-material/ReceiptLong';
```

The file already imports `ReceiptLongIcon` near the top. If that existing import is retained, do not add a duplicate import. Add this item to the `Operations.items` array after `Demand Spooling`:

```tsx
      { text: 'Receipt Appropriation', icon: <ReceiptLongIcon />, path: '/receipt-appropriation/demo-receipt' },
```

Replace the demo path with the FINNOVA-61 receipt navigation callback when the receipt page supplies a real receipt ID. Direct `/receipt-appropriation/:receiptId` navigation remains supported. Keep the permission-aware visibility consistent with the existing `PermissionsContext`; do not create a second authorization system in the UI.

- [ ] 14. Frontend Vitest and React Testing Library tests
  - [ ] 14.1 CREATE `src/services/receiptAppropriation.service.test.ts`

```typescript
import {describe,expect,it,vi} from 'vitest'; import api from './api'; import {receiptAppropriationRealService} from './real/receiptAppropriation.real';
vi.mock('./api',()=>({default:{get:vi.fn(),post:vi.fn()}}));
describe('receipt appropriation real service',()=>{it('uses the gateway-composed appropriation paths',async()=>{vi.mocked(api.get).mockResolvedValue({data:{}} as never);await receiptAppropriationRealService.show('receipt-id');expect(api.get).toHaveBeenCalledWith('/receipt-appropriation/receipt-id/dues',{params:{page:1,pageSize:100}});vi.mocked(api.post).mockResolvedValue({data:{}} as never);await receiptAppropriationRealService.suggest('receipt-id');expect(api.post).toHaveBeenCalledWith('/receipt-appropriation/receipt-id/suggest',{});});});
```

  - [ ] 14.2 CREATE `src/pages/ReceiptAppropriation.test.tsx`

```tsx
import {render,screen,waitFor} from '@testing-library/react'; import {MemoryRouter,Route,Routes} from 'react-router-dom'; import {beforeEach,describe,expect,it,vi} from 'vitest'; import ReceiptAppropriation from './ReceiptAppropriation'; import {receiptAppropriationService} from '../services/receiptAppropriation.service';
vi.mock('../services/receiptAppropriation.service',()=>({receiptAppropriationService:{show:vi.fn(),suggest:vi.fn(),confirm:vi.fn()}}));
describe('ReceiptAppropriation',()=>{beforeEach(()=>{vi.mocked(receiptAppropriationService.show).mockResolvedValue({receiptId:'r',customerId:'c',companyId:'co',lineOfBusinessId:'lob',receiptDocumentAmount:100,data:[],total:0,page:1,pageSize:100,totalPages:1,totals:{receiptAmount:100,dueAllocationTotal:0,advanceAccountAmount:100,residualAmount:100,reconciles:true},receiptVersion:'1',dueFingerprint:'d'});});it('renders English empty state controls and keeps confirm blocked',async()=>{render(<MemoryRouter initialEntries={['/receipt-appropriation/r']}><Routes><Route path="/receipt-appropriation/:receiptId" element={<ReceiptAppropriation/>}/></Routes></MemoryRouter>);await waitFor(()=>expect(receiptAppropriationService.show).toHaveBeenCalled());expect(screen.getByText('Receipt Appropriation')).toBeInTheDocument();expect(screen.getByRole('button',{name:'Confirm'})).toBeDisabled();expect(screen.queryByText(/Arabic|العربية/i)).not.toBeInTheDocument();});});
```

  - [ ] 14.3 CREATE `src/test/receiptAppropriation.calculation.test.ts`

```typescript
import {describe,expect,it} from 'vitest';
describe('receipt appropriation UI money display',()=>{it('keeps INR values at two decimals',()=>{const amount=Number((35000-28000).toFixed(2));expect(amount).toBe(7000);expect(`₹${amount.toFixed(2)}`).toBe('₹7000.00');});});
```

Expand the frontend tests to cover Manual clearing as zero, over-allocation disablement, Suggest rule evidence, residual acceptance, loading disablement, server error preservation, successful confirmation lockout, mock idempotency, centralized 401/403 behavior, and no RTL attributes.

- [ ] 15. Integration, wiring, and verification handoff
  - [ ] 15.1 MODIFY `Finnova.Tests` host test project with backend integration tests

Use the existing `WebApplicationFactory<Program>` convention and add tests for:

```csharp
[Fact]
public async Task UnauthenticatedAppropriationRequestReturns401()
{
    await using var factory = new WebApplicationFactory<Program>();
    using var client = factory.CreateClient();
    var response = await client.GetAsync($"/api/receipt-appropriation/{Guid.NewGuid()}/dues");
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
}
```

Add authenticated 403 coverage, JSON string-enum/decimal contract coverage, Show filtering/order, non-persisted Suggest, successful atomic Confirm, stale due rollback, missing Advance Account, duplicate Client Request Identifier, different-payload idempotency conflict, exactly one allocation/audit set, and gateway path assumptions. Configure the authoritative ports with test doubles only in the test host; do not add test doubles to production source.

  - [ ] 15.2 Verification notes, not commands to execute during spec creation

After all implementation tasks and the unresolved adapter gate are complete, the implementer should run these commands from the stated repositories. This task document does not run them:

```powershell
# Backend
 dotnet build Finnova.Backend.slnx
 dotnet test Finnova.Tests/Finnova.Tests.csproj --no-restore

# Frontend
 npm test -- --run
 npm run build

# Migration review/apply, after inspecting the generated migration
 dotnet ef migrations list -p Finnova.Repository -s Finnova.AccountsService
 dotnet ef database update -p Finnova.Repository -s Finnova.AccountsService
```

Verification must confirm: all requirements are covered, no preview writes an appropriation or audit row, Confirm is atomic, due/rule/receipt changes reject as stale, idempotency prevents duplicates, all amounts reconcile to two decimals in INR, unauthorized requests return 401/403 without mutation, UI uses the shared axios client, and all labels remain English-only with no RTL behavior.

## Notes

- Tasks marked with `*` would be optional test tasks in an accelerated MVP; this plan keeps the required test tasks unskipped because FINNOVA-62 is an accounting allocation feature.
- Do not modify `.config.kiro`, `requirements.md`, or `design.md` while applying this task document.
- Do not create local Receipt, PendingDue, AppropriationRule, or Advance Account tables merely to satisfy compilation. Use the adapter ports and obtain the owning contracts first.
- The `ReceiptAppropriationAdapterRegistration` throwing implementation is an intentional dependency gate. Replace it with concrete authoritative registrations before enabling the Accounts host.
- The migration command is documented only. It is not executed during spec creation.
- The implementation must use `decimal` on the server, validate and reject more than two fractional digits, use `MidpointRounding.AwayFromZero` for non-negative accounting values, and never silently truncate money.
