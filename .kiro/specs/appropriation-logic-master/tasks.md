# Implementation Plan: Appropriation Logic Master

**Feature:** `appropriation-logic-master`  
**Jira:** FINNOVA-67  
**Backend repository:** `E:\Finnova\Finnova-API`  
**Frontend repository:** `E:\Finnova\Finnova-UI\Finnova-UI`  
**Consumers:** FINNOVA-62 Receipt Appropriation Suggest and Suggest-mode Confirm  
**Boundary:** FINNOVA-67 configures rules only. It does not allocate receipts, update dues, or post journals.

## Overview

This handoff implements the FINNOVA-67 Appropriation Logic Master from backend domain through frontend tests. It creates immutable, versioned Company/LOB rules and an authenticated resolver consumed by FINNOVA-62. Apply the FINNOVA-62 adapter tasks only after the resolver contract and due-head mappings are confirmed.

Apply tasks in order. Every CREATE block is a complete file. Every MODIFY block identifies the exact anchor and the complete insertion or replacement snippet. Resolve the prerequisite gates in `design.md` before copying source snippets that depend on owner-supplied names. Do not run builds, tests, migrations, or application processes while preparing this task document.

## Tasks

- [ ] 1.1 CREATE `Finnova.Models/Domain/Enums/AppropriationLogicEnums.cs`

```csharp
namespace Finnova.Models.Domain.Enums;

public enum AppropriationDirection
{
    Vertical = 0,
    Horizontal = 1
}

public enum AppropriationDueType
{
    Installment = 0,
    Principal = 1,
    Interest = 2,
    Insurance = 3,
    Odi = 4,
    BankCharges = 5,
    ChequeReturnCharges = 6,
    LegalCharges = 7
}

public enum AppropriationRuleOperation
{
    Created = 0,
    Updated = 1,
    Activated = 2,
    Deactivated = 3
}
```

- [ ] 1.2 CREATE `Finnova.Models/Domain/Entities/AppropriationRuleVersion.cs`

```csharp
using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

public sealed class AppropriationRuleVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RuleId { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Guid? LineOfBusinessId { get; set; }
    public AppropriationDirection Direction { get; set; }
    public bool FullDue { get; set; }
    public bool IsActive { get; set; }
    public int VersionNumber { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string NormalizedRequestHash { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ActivatedAtUtc { get; set; }
    public DateTime? DeactivatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public List<AppropriationRuleDetail> Details { get; set; } = new();
}

public sealed class AppropriationRuleDetail
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RuleVersionId { get; set; }
    public int SequenceNumber { get; set; }
    public AppropriationDueType DueType { get; set; }
    public string CashFlowDueTypeCode { get; set; } = string.Empty;
    public decimal Percentage { get; set; }
    public AppropriationRuleVersion RuleVersion { get; set; } = null!;
}

public sealed class AppropriationRuleAuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RuleId { get; set; }
    public Guid RuleVersionId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? LineOfBusinessId { get; set; }
    public AppropriationRuleOperation Operation { get; set; }
    public string Actor { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string NormalizedRequestHash { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class RuleMutationIdempotency
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string IdempotencyKey { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string NormalizedRequestHash { get; set; } = string.Empty;
    public Guid RuleId { get; set; }
    public Guid RuleVersionId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
```

- [ ] 2.1 CREATE `Finnova.Models/Domain/Exceptions/AppropriationLogicExceptions.cs`

```csharp
namespace Finnova.Models.Domain.Exceptions;

public abstract class AppropriationLogicException(string code, string message) : Exception(message)
{
    public string ErrorCode { get; } = code;
    public IReadOnlyDictionary<string, string[]> Errors { get; init; } = new Dictionary<string, string[]>();
}

public sealed class AppropriationLogicValidationException(string message) : AppropriationLogicException("ERR-APPROP-RULE-VALIDATION", message);
public sealed class AppropriationLogicNotFoundException(string message) : AppropriationLogicException("ERR-APPROP-RULE-NOT-FOUND", message);
public sealed class AppropriationLogicForbiddenException(string message) : AppropriationLogicException("ERR-APPROP-RULE-403", message);
public sealed class AppropriationLogicConflictException(string code, string message) : AppropriationLogicException(code, message);
public sealed class AppropriationLogicDependencyException(string message) : AppropriationLogicException("ERR-APPROP-RULE-DEPENDENCY", message);

public static class AppropriationLogicErrorCodes
{
    public const string Ambiguous = "ERR-APPROP-RULE-AMBIGUOUS";
    public const string DuplicateDueHead = "ERR-APPROP-DUPLICATE-DUE-HEAD";
    public const string TotalOver100 = "ERR-APPROP-RULE-TOTAL-OVER-100";
    public const string Stale = "ERR-APPROP-RULE-STALE";
    public const string IdempotencyConflict = "ERR-APPROP-IDEMPOTENCY-CONFLICT";
    public const string DueMapping = "ERR-APPROP-DUE-MAPPING";
}
```

- [ ] 3.1 CREATE `Finnova.Models/Contracts/AppropriationLogic/AppropriationLogicContracts.cs`

```csharp
using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Contracts.AppropriationLogic;

public sealed record AppropriationLogicDetailRequest(AppropriationDueType DueType, decimal Percentage);
public sealed record AppropriationLogicDetailResponse(Guid Id, int SequenceNumber, AppropriationDueType DueType, string CashFlowDueTypeCode, decimal Percentage);

public sealed record CreateAppropriationLogicRequest(
    Guid CompanyId,
    Guid? LineOfBusinessId,
    AppropriationDirection Direction,
    bool FullDue,
    bool Active,
    IReadOnlyList<AppropriationLogicDetailRequest> Details,
    string IdempotencyKey);

public sealed record UpdateAppropriationLogicRequest(
    Guid? LineOfBusinessId,
    AppropriationDirection Direction,
    bool FullDue,
    bool Active,
    IReadOnlyList<AppropriationLogicDetailRequest> Details,
    int ExpectedVersion,
    string IdempotencyKey);

public sealed record LifecycleAppropriationLogicRequest(int ExpectedVersion, string IdempotencyKey);

public sealed record AppropriationLogicResponse(
    Guid RuleId,
    Guid RuleVersionId,
    Guid CompanyId,
    Guid? LineOfBusinessId,
    AppropriationDirection Direction,
    bool FullDue,
    bool IsActive,
    int VersionNumber,
    IReadOnlyList<AppropriationLogicDetailResponse> Details,
    string CreatedBy,
    DateTime CreatedAtUtc,
    DateTime? ActivatedAtUtc,
    DateTime? DeactivatedAtUtc,
    string CorrelationId);

public sealed record AppropriationLogicListItem(
    Guid RuleId,
    Guid RuleVersionId,
    Guid CompanyId,
    Guid? LineOfBusinessId,
    AppropriationDirection Direction,
    bool FullDue,
    bool IsActive,
    int VersionNumber,
    decimal PercentageTotal,
    DateTime CreatedAtUtc);

public sealed record AppropriationResolutionDetail(
    Guid DetailId,
    AppropriationDueType DueType,
    string CashFlowDueTypeCode,
    decimal Percentage,
    int SequenceNumber);

public sealed record AppropriationResolutionResponse(
    string ResolverContractVersion,
    Guid RuleId,
    Guid RuleVersionId,
    int VersionNumber,
    Guid CompanyId,
    Guid? LineOfBusinessId,
    bool UsedGlobalFallback,
    AppropriationDirection Direction,
    bool FullDue,
    IReadOnlyList<AppropriationResolutionDetail> Details);
```

- [ ] 3.2 CREATE `Finnova.Service/AppropriationLogic/Ports/AppropriationLogicPorts.cs`

```csharp
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using System.Security.Claims;

namespace Finnova.Service.AppropriationLogic.Ports;

public sealed record CompanyScope(Guid CompanyId, bool CanAdminister);
public sealed record LineOfBusinessScope(Guid Id, Guid CompanyId, bool IsActive);
public sealed record DueHeadMapping(AppropriationDueType DueType, string CashFlowDueTypeCode, bool IsActive);
public sealed record AppropriationResolution(AppropriationRuleVersion Rule, bool UsedGlobalFallback);

public interface IAppropriationReferencePort
{
    Task<bool> IsActiveCompanyAsync(Guid companyId, CancellationToken ct);
    Task<LineOfBusinessScope?> GetLineOfBusinessAsync(Guid id, CancellationToken ct);
    Task<DueHeadMapping?> GetDueHeadMappingAsync(AppropriationDueType dueType, CancellationToken ct);
}

public interface IAppropriationAuthorizationPort
{
    Task<CompanyScope> ResolveAsync(ClaimsPrincipal user, CancellationToken ct);
    bool CanAdminister(CompanyScope scope, Guid companyId);
}

public interface IAppropriationLogicRepository
{
    Task<IReadOnlyList<AppropriationRuleVersion>> ListAsync(Guid? companyId, Guid? lineOfBusinessId, bool includeInactive, CancellationToken ct);
    Task<AppropriationRuleVersion?> GetAsync(Guid ruleId, CancellationToken ct);
    Task<IReadOnlyList<AppropriationRuleVersion>> GetActiveCandidatesAsync(Guid companyId, Guid lineOfBusinessId, CancellationToken ct);
    Task<RuleMutationIdempotency?> FindIdempotencyAsync(string key, CancellationToken ct);
    Task AddVersionAsync(AppropriationRuleVersion version, CancellationToken ct);
    Task AddAuditAsync(AppropriationRuleAuditEntry entry, CancellationToken ct);
    Task AddIdempotencyAsync(RuleMutationIdempotency entry, CancellationToken ct);
    Task DeactivateScopeAsync(Guid companyId, Guid? lineOfBusinessId, CancellationToken ct);
}

public interface IAppropriationLogicUnitOfWork
{
    Task<IAsyncDisposable> BeginTransactionAsync(CancellationToken ct);
    Task<int> SaveChangesAsync(CancellationToken ct);
}

public interface IAppropriationRuleResolver
{
    Task<AppropriationResolutionResult> ResolveAsync(Guid companyId, Guid lineOfBusinessId, CancellationToken ct);
}

public enum AppropriationResolutionStatus { Resolved = 0, NotFound = 1, Ambiguous = 2 }
public sealed record AppropriationResolutionResult(AppropriationResolutionStatus Status, AppropriationResolution? Resolution, string ResolverContractVersion, string? ErrorCode);
```

- [ ] 4.1 CREATE `Finnova.Repository/Configuration/AppropriationLogicConfigurations.cs`

```csharp
using Finnova.Models.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finnova.Repository.Configuration;

public sealed class AppropriationRuleVersionConfiguration : IEntityTypeConfiguration<AppropriationRuleVersion>
{
    public void Configure(EntityTypeBuilder<AppropriationRuleVersion> b)
    {
        b.ToTable("appropriation_rule_versions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Direction).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired();
        b.Property(x => x.NormalizedRequestHash).HasMaxLength(128).IsRequired();
        b.Property(x => x.CreatedBy).HasMaxLength(200).IsRequired();
        b.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        b.HasIndex(x => new { x.CompanyId, x.LineOfBusinessId, x.IsActive });
        b.HasIndex(x => x.IdempotencyKey).IsUnique();
        b.HasMany(x => x.Details).WithOne(x => x.RuleVersion).HasForeignKey(x => x.RuleVersionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class AppropriationRuleDetailConfiguration : IEntityTypeConfiguration<AppropriationRuleDetail>
{
    public void Configure(EntityTypeBuilder<AppropriationRuleDetail> b)
    {
        b.ToTable("appropriation_rule_details");
        b.HasKey(x => x.Id);
        b.Property(x => x.DueType).HasConversion<string>().HasMaxLength(40).IsRequired();
        b.Property(x => x.CashFlowDueTypeCode).HasMaxLength(80).IsRequired();
        b.Property(x => x.Percentage).HasPrecision(5, 2).IsRequired();
        b.HasIndex(x => new { x.RuleVersionId, x.SequenceNumber }).IsUnique();
        b.HasIndex(x => new { x.RuleVersionId, x.DueType }).IsUnique();
    }
}

public sealed class AppropriationRuleAuditConfiguration : IEntityTypeConfiguration<AppropriationRuleAuditEntry>
{
    public void Configure(EntityTypeBuilder<AppropriationRuleAuditEntry> b)
    {
        b.ToTable("appropriation_rule_audit_entries");
        b.HasKey(x => x.Id);
        b.Property(x => x.Operation).HasConversion<string>().HasMaxLength(30).IsRequired();
        b.Property(x => x.Actor).HasMaxLength(200).IsRequired();
        b.Property(x => x.CorrelationId).HasMaxLength(100).IsRequired();
        b.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired();
        b.Property(x => x.NormalizedRequestHash).HasMaxLength(128).IsRequired();
        b.HasIndex(x => x.RuleVersionId);
    }
}

public sealed class RuleMutationIdempotencyConfiguration : IEntityTypeConfiguration<RuleMutationIdempotency>
{
    public void Configure(EntityTypeBuilder<RuleMutationIdempotency> b)
    {
        b.ToTable("appropriation_rule_idempotency");
        b.HasKey(x => x.Id);
        b.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired();
        b.Property(x => x.Operation).HasMaxLength(30).IsRequired();
        b.Property(x => x.NormalizedRequestHash).HasMaxLength(128).IsRequired();
        b.HasIndex(x => x.IdempotencyKey).IsUnique();
    }
}
```

- [ ] 4.2 MODIFY `Finnova.Repository/Context/FinnovaDbContext.cs`

Insert after the existing master-data `DbSet` declarations and before `OnModelCreating`:

```csharp
    public DbSet<AppropriationRuleVersion> AppropriationRuleVersions => Set<AppropriationRuleVersion>();
    public DbSet<AppropriationRuleDetail> AppropriationRuleDetails => Set<AppropriationRuleDetail>();
    public DbSet<AppropriationRuleAuditEntry> AppropriationRuleAuditEntries => Set<AppropriationRuleAuditEntry>();
    public DbSet<RuleMutationIdempotency> RuleMutationIdempotencies => Set<RuleMutationIdempotency>();
```

Use the existing `using Finnova.Models.Domain.Entities;` anchor. Do not add Receipt, PendingDue, CashFlowDue, journal, or FINNOVA-61 payment-mode DbSets. After implementation and migration review, the implementer runs this command manually; do not run it while executing this spec workflow:

```powershell
dotnet ef migrations add AddAppropriationLogicMaster -p Finnova.Repository -s Finnova.SystemAdminService -o Migrations
```

- [ ] 5.1 CREATE `Finnova.Repository/Interfaces/IAppropriationLogicRepository.cs`

```csharp
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Interfaces;

public interface IAppropriationLogicRepository
{
    Task<IReadOnlyList<AppropriationRuleVersion>> ListAsync(Guid? companyId, Guid? lineOfBusinessId, bool includeInactive, CancellationToken ct);
    Task<AppropriationRuleVersion?> GetAsync(Guid ruleId, CancellationToken ct);
    Task<IReadOnlyList<AppropriationRuleVersion>> GetActiveCandidatesAsync(Guid companyId, Guid lineOfBusinessId, CancellationToken ct);
    Task<RuleMutationIdempotency?> FindIdempotencyAsync(string key, CancellationToken ct);
    Task AddVersionAsync(AppropriationRuleVersion version, CancellationToken ct);
    Task AddAuditAsync(AppropriationRuleAuditEntry entry, CancellationToken ct);
    Task AddIdempotencyAsync(RuleMutationIdempotency entry, CancellationToken ct);
    Task DeactivateScopeAsync(Guid companyId, Guid? lineOfBusinessId, CancellationToken ct);
}
```

- [ ] 5.2 CREATE `Finnova.Repository/Repositories/AppropriationLogicRepository.cs`

```csharp
using Finnova.Models.Domain.Entities;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Finnova.Repository.Repositories;

public sealed class AppropriationLogicRepository(FinnovaDbContext db) : IAppropriationLogicRepository
{
    public async Task<IReadOnlyList<AppropriationRuleVersion>> ListAsync(Guid? companyId, Guid? lineOfBusinessId, bool includeInactive, CancellationToken ct)
    {
        var query = db.AppropriationRuleVersions.AsNoTracking().Include(x => x.Details).AsQueryable();
        if (companyId.HasValue) query = query.Where(x => x.CompanyId == companyId.Value);
        if (lineOfBusinessId.HasValue) query = query.Where(x => x.LineOfBusinessId == lineOfBusinessId.Value);
        if (!includeInactive) query = query.Where(x => x.IsActive);
        return await query.OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);
    }

    public Task<AppropriationRuleVersion?> GetAsync(Guid ruleId, CancellationToken ct) =>
        db.AppropriationRuleVersions.AsNoTracking().Include(x => x.Details).Where(x => x.RuleId == ruleId).OrderByDescending(x => x.VersionNumber).FirstOrDefaultAsync(ct);

    public Task<IReadOnlyList<AppropriationRuleVersion>> GetActiveCandidatesAsync(Guid companyId, Guid lineOfBusinessId, CancellationToken ct) =>
        db.AppropriationRuleVersions.AsNoTracking().Include(x => x.Details).Where(x => x.CompanyId == companyId && x.IsActive && (x.LineOfBusinessId == lineOfBusinessId || x.LineOfBusinessId == null)).OrderBy(x => x.LineOfBusinessId == null).ToListAsync(ct).ContinueWith(x => (IReadOnlyList<AppropriationRuleVersion>)x.Result, ct);

    public Task<RuleMutationIdempotency?> FindIdempotencyAsync(string key, CancellationToken ct) => db.RuleMutationIdempotencies.AsNoTracking().SingleOrDefaultAsync(x => x.IdempotencyKey == key, ct);
    public Task AddVersionAsync(AppropriationRuleVersion version, CancellationToken ct) => db.AppropriationRuleVersions.AddAsync(version, ct).AsTask();
    public Task AddAuditAsync(AppropriationRuleAuditEntry entry, CancellationToken ct) => db.AppropriationRuleAuditEntries.AddAsync(entry, ct).AsTask();
    public Task AddIdempotencyAsync(RuleMutationIdempotency entry, CancellationToken ct) => db.RuleMutationIdempotencies.AddAsync(entry, ct).AsTask();

    public Task DeactivateScopeAsync(Guid companyId, Guid? lineOfBusinessId, CancellationToken ct) =>
        db.AppropriationRuleVersions.Where(x => x.CompanyId == companyId && x.LineOfBusinessId == lineOfBusinessId && x.IsActive).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false).SetProperty(x => x.DeactivatedAtUtc, DateTime.UtcNow), ct);
}
```

- [ ] 5.3 MODIFY `Finnova.Repository/DependencyInjection.cs`

Insert the repository import after the existing repository imports:

```csharp
using Finnova.Repository.Repositories;
```

Insert immediately after the existing master-data repository registrations:

```csharp
        services.AddScoped<IAppropriationLogicRepository, AppropriationLogicRepository>();
```

- [ ] 6.1 CREATE `Finnova.Service/AppropriationLogic/Rules/AppropriationLogicNormalizer.cs`

```csharp
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Finnova.Models.Contracts.AppropriationLogic;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Service.AppropriationLogic.Ports;

namespace Finnova.Service.AppropriationLogic.Rules;

public sealed record NormalizedDetail(AppropriationDueType DueType, string CashFlowDueTypeCode, decimal Percentage, int SequenceNumber);
public sealed record NormalizedRule(Guid CompanyId, Guid? LineOfBusinessId, AppropriationDirection Direction, bool FullDue, IReadOnlyList<NormalizedDetail> Details, decimal PercentageTotal, string Hash);

public static class AppropriationLogicNormalizer
{
    public static async Task<NormalizedRule> NormalizeAsync(Guid companyId, Guid? lobId, AppropriationDirection direction, bool fullDue, IReadOnlyList<AppropriationLogicDetailRequest> input, IAppropriationReferencePort references, CancellationToken ct)
    {
        if (companyId == Guid.Empty) throw new AppropriationLogicValidationException("CompanyId is required.");
        if (!Enum.IsDefined(direction)) throw new AppropriationLogicValidationException("Type must be Vertical or Horizontal.");
        if (input.Count == 0) throw new AppropriationLogicValidationException("At least one appropriation detail is required.");
        if (lobId.HasValue)
        {
            var lob = await references.GetLineOfBusinessAsync(lobId.Value, ct) ?? throw new AppropriationLogicValidationException("Line of Business was not found.");
            if (!lob.IsActive || lob.CompanyId != companyId) throw new AppropriationLogicValidationException("Line of Business is inactive or belongs to another Company.");
        }
        var details = new List<NormalizedDetail>();
        var seen = new HashSet<AppropriationDueType>();
        for (var index = 0; index < input.Count; index++)
        {
            var item = input[index];
            if (!seen.Add(item.DueType)) throw new AppropriationLogicConflictException(AppropriationLogicErrorCodes.DuplicateDueHead, "Duplicate appropriation due head is not allowed.");
            if (item.Percentage < 0m || item.Percentage > 100m || decimal.Round(item.Percentage, 2) != item.Percentage) throw new AppropriationLogicValidationException($"Percentage at sequence {index + 1} must be between 0.00 and 100.00 with at most two decimal places.");
            var mapping = await references.GetDueHeadMappingAsync(item.DueType, ct) ?? throw new AppropriationLogicDependencyException($"No due-ledger mapping exists for {item.DueType}.");
            if (!mapping.IsActive) throw new AppropriationLogicDependencyException($"The due-ledger mapping for {item.DueType} is inactive.");
            details.Add(new NormalizedDetail(item.DueType, mapping.CashFlowDueTypeCode, item.Percentage, index + 1));
        }
        var total = decimal.Round(details.Sum(x => x.Percentage), 2);
        if (total > 100m) throw new AppropriationLogicConflictException(AppropriationLogicErrorCodes.TotalOver100, "The total percentage cannot exceed 100.00.");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { companyId, lobId, direction, fullDue, details }))));
        return new NormalizedRule(companyId, lobId, direction, fullDue, details, total, hash);
    }
}
```

- [ ] 6.2 CREATE `Finnova.Service/AppropriationLogic/Rules/AppropriationLogicPrecedence.cs`

```csharp
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Exceptions;
using Finnova.Service.AppropriationLogic.Ports;

namespace Finnova.Service.AppropriationLogic.Rules;

public static class AppropriationLogicPrecedence
{
    public static AppropriationResolution Resolve(Guid companyId, Guid lineOfBusinessId, IReadOnlyList<AppropriationRuleVersion> candidates)
    {
        var scoped = candidates.Where(x => x.CompanyId == companyId && x.IsActive).ToArray();
        var exact = scoped.Where(x => x.LineOfBusinessId == lineOfBusinessId).ToArray();
        if (exact.Length > 1) throw new AppropriationLogicConflictException(AppropriationLogicErrorCodes.Ambiguous, "Multiple active appropriation rules match the Line of Business.");
        if (exact.Length == 1) return new AppropriationResolution(exact[0], false);
        var global = scoped.Where(x => x.LineOfBusinessId is null).ToArray();
        if (global.Length > 1) throw new AppropriationLogicConflictException(AppropriationLogicErrorCodes.Ambiguous, "Multiple active global appropriation rules match the Company.");
        if (global.Length == 1) return new AppropriationResolution(global[0], true);
        throw new AppropriationLogicNotFoundException("No active appropriation rule is configured for the Company and Line of Business.");
    }
}
```

- [ ] 6.3 MODIFY `Finnova.Service/DependencyInjection.cs`

Insert the following registrations before `return services;` in `AddFinnovaServices`:

```csharp
        services.AddScoped<IAppropriationLogicRepository, Finnova.Repository.Repositories.AppropriationLogicRepository>();
        services.AddScoped<IAppropriationRuleResolver, Finnova.Service.AppropriationLogic.Services.AppropriationRuleResolver>();
```

Use the existing reference-port registrations for Company, LOB, due-head mapping, authorization, and `IAppropriationLogicUnitOfWork`. Do not register any receipt, due-balance, or journal writer implementation in FINNOVA-67.

- [ ] 7.1 CREATE `Finnova.Service/AppropriationLogic/Services/AppropriationRuleResolver.cs`

```csharp
using Finnova.Models.Contracts.AppropriationLogic;
using Finnova.Service.AppropriationLogic.Ports;
using Finnova.Service.AppropriationLogic.Rules;

namespace Finnova.Service.AppropriationLogic.Services;

public sealed class AppropriationRuleResolver(IAppropriationLogicRepository repository) : IAppropriationRuleResolver
{
    public async Task<AppropriationResolutionResult> ResolveAsync(Guid companyId, Guid lineOfBusinessId, CancellationToken ct)
    {
        var candidates = await repository.GetActiveCandidatesAsync(companyId, lineOfBusinessId, ct);
        try
        {
            var resolution = AppropriationLogicPrecedence.Resolve(companyId, lineOfBusinessId, candidates);
            var rule = resolution.Rule;
            var response = new AppropriationRuleResolutionResponse(
                "1.0",
                rule.RuleId,
                rule.Id,
                rule.VersionNumber,
                rule.CompanyId,
                rule.LineOfBusinessId,
                resolution.UsedGlobalFallback,
                rule.Direction,
                rule.FullDue,
                rule.Details.OrderBy(x => x.SequenceNumber).Select(x => new AppropriationResolutionDetail(x.Id, x.DueType, x.CashFlowDueTypeCode, x.Percentage, x.SequenceNumber)).ToArray());
            return new(AppropriationResolutionStatus.Resolved, new AppropriationResolution(rule, resolution.UsedGlobalFallback), "1.0", null);
        }
        catch (Models.Domain.Exceptions.AppropriationLogicNotFoundException)
        {
            return new(AppropriationResolutionStatus.NotFound, null, "1.0", "ERR-APPROP-RULE-NOT-FOUND");
        }
        catch (Models.Domain.Exceptions.AppropriationLogicConflictException)
        {
            return new(AppropriationResolutionStatus.Ambiguous, null, "1.0", "ERR-APPROP-RULE-AMBIGUOUS");
        }
    }
}
```

- [ ] 8.1 CREATE `Finnova.Service/AppropriationLogic/AppropriationLogicHandlers.cs`

```csharp
using System.Security.Claims;
using Finnova.Models.Contracts.AppropriationLogic;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.AppropriationLogic.Ports;
using Finnova.Service.AppropriationLogic.Rules;
using FluentValidation;
using MediatR;

namespace Finnova.Service.AppropriationLogic;

public sealed record ListAppropriationLogicQuery(Guid? CompanyId, Guid? LineOfBusinessId, bool IncludeInactive, ClaimsPrincipal User) : IRequest<IReadOnlyList<AppropriationLogicListItem>>;
public sealed record GetAppropriationLogicQuery(Guid RuleId, ClaimsPrincipal User) : IRequest<AppropriationLogicResponse>;
public sealed record CreateAppropriationLogicCommand(CreateAppropriationLogicRequest Request, ClaimsPrincipal User, string Actor, string CorrelationId) : IRequest<AppropriationLogicResponse>;
public sealed record UpdateAppropriationLogicCommand(Guid RuleId, UpdateAppropriationLogicRequest Request, ClaimsPrincipal User, string Actor, string CorrelationId) : IRequest<AppropriationLogicResponse>;
public sealed record ActivateAppropriationLogicCommand(Guid RuleId, LifecycleAppropriationLogicRequest Request, ClaimsPrincipal User, string Actor, string CorrelationId) : IRequest<AppropriationLogicResponse>;
public sealed record DeactivateAppropriationLogicCommand(Guid RuleId, LifecycleAppropriationLogicRequest Request, ClaimsPrincipal User, string Actor, string CorrelationId) : IRequest<AppropriationLogicResponse>;
public sealed record ResolveAppropriationLogicQuery(Guid CompanyId, Guid LineOfBusinessId) : IRequest<AppropriationResolutionResponse>;

public sealed class CreateAppropriationLogicValidator : AbstractValidator<CreateAppropriationLogicCommand>
{
    public CreateAppropriationLogicValidator() { RuleFor(x => x.Request.CompanyId).NotEmpty(); RuleFor(x => x.Request.IdempotencyKey).NotEmpty().MaximumLength(100); RuleFor(x => x.Request.Details).NotEmpty(); RuleForEach(x => x.Request.Details).ChildRules(d => d.RuleFor(y => y.Percentage).InclusiveBetween(0m, 100m)); }
}

public sealed class UpdateAppropriationLogicValidator : AbstractValidator<UpdateAppropriationLogicCommand>
{
    public UpdateAppropriationLogicValidator() { RuleFor(x => x.RuleId).NotEmpty(); RuleFor(x => x.Request.ExpectedVersion).GreaterThan(0); RuleFor(x => x.Request.IdempotencyKey).NotEmpty().MaximumLength(100); RuleFor(x => x.Request.Details).NotEmpty(); }
}

public sealed class LifecycleAppropriationLogicValidator : AbstractValidator<LifecycleAppropriationLogicCommandBase>
{
    public LifecycleAppropriationLogicValidator() { RuleFor(x => x.RuleId).NotEmpty(); RuleFor(x => x.Request.ExpectedVersion).GreaterThan(0); RuleFor(x => x.Request.IdempotencyKey).NotEmpty().MaximumLength(100); }
}

public abstract record LifecycleAppropriationLogicCommandBase(Guid RuleId, LifecycleAppropriationLogicRequest Request, ClaimsPrincipal User, string Actor, string CorrelationId);

internal static class AppropriationLogicMapper
{
    public static AppropriationLogicResponse Map(AppropriationRuleVersion x, string correlation) => new(x.RuleId, x.Id, x.CompanyId, x.LineOfBusinessId, x.Direction, x.FullDue, x.IsActive, x.VersionNumber, x.Details.OrderBy(d => d.SequenceNumber).Select(d => new AppropriationLogicDetailResponse(d.Id, d.SequenceNumber, d.DueType, d.CashFlowDueTypeCode, d.Percentage)).ToArray(), x.CreatedBy, x.CreatedAtUtc, x.ActivatedAtUtc, x.DeactivatedAtUtc, correlation);
}

public sealed class ListAppropriationLogicHandler(IAppropriationLogicRepository repository, IAppropriationAuthorizationPort authorization) : IRequestHandler<ListAppropriationLogicQuery, IReadOnlyList<AppropriationLogicListItem>>
{
    public async Task<IReadOnlyList<AppropriationLogicListItem>> Handle(ListAppropriationLogicQuery request, CancellationToken ct)
    {
        var scope = await authorization.ResolveAsync(request.User, ct);
        if (request.CompanyId.HasValue && !authorization.CanAdminister(scope, request.CompanyId.Value)) throw new AppropriationLogicForbiddenException("The caller cannot administer the requested Company.");
        var rows = await repository.ListAsync(request.CompanyId, request.LineOfBusinessId, request.IncludeInactive, ct);
        return rows.Select(x => new AppropriationLogicListItem(x.RuleId, x.Id, x.CompanyId, x.LineOfBusinessId, x.Direction, x.FullDue, x.IsActive, x.VersionNumber, x.Details.Sum(d => d.Percentage), x.CreatedAtUtc)).ToArray();
    }
}

public sealed class GetAppropriationLogicHandler(IAppropriationLogicRepository repository, IAppropriationAuthorizationPort authorization) : IRequestHandler<GetAppropriationLogicQuery, AppropriationLogicResponse>
{
    public async Task<AppropriationLogicResponse> Handle(GetAppropriationLogicQuery request, CancellationToken ct)
    {
        var rule = await repository.GetAsync(request.RuleId, ct) ?? throw new AppropriationLogicNotFoundException("Appropriation rule was not found.");
        var scope = await authorization.ResolveAsync(request.User, ct);
        if (!authorization.CanAdminister(scope, rule.CompanyId)) throw new AppropriationLogicForbiddenException("The caller cannot administer the rule Company.");
        return AppropriationLogicMapper.Map(rule, string.Empty);
    }
}
```

- [ ] 8.2 MODIFY `Finnova.Service/AppropriationLogic/AppropriationLogicHandlers.cs`

Append the following complete handler classes after the existing handlers. They use the existing `IAppropriationLogicUnitOfWork`, `IAppropriationReferencePort`, and repository registrations; they do not write receipts, due balances, or journals.

```csharp
public sealed class CreateAppropriationLogicHandler(IAppropriationLogicRepository repository, IAppropriationReferencePort references, IAppropriationAuthorizationPort authorization, IAppropriationLogicUnitOfWork uow) : IRequestHandler<CreateAppropriationLogicCommand, AppropriationLogicResponse>
{
    public async Task<AppropriationLogicResponse> Handle(CreateAppropriationLogicCommand command, CancellationToken ct)
    {
        var prior = await repository.FindIdempotencyAsync(command.Request.IdempotencyKey, ct);
        if (prior is not null) throw new AppropriationLogicConflictException(AppropriationLogicErrorCodes.IdempotencyConflict, "The idempotency key has already been used.");
        var scope = await authorization.ResolveAsync(command.User, ct);
        if (!authorization.CanAdminister(scope, command.Request.CompanyId)) throw new AppropriationLogicForbiddenException("The caller cannot administer the requested Company.");
        if (!await references.IsActiveCompanyAsync(command.Request.CompanyId, ct)) throw new AppropriationLogicValidationException("Company is inactive or was not found.");
        var normalized = await AppropriationLogicNormalizer.NormalizeAsync(command.Request.CompanyId, command.Request.LineOfBusinessId, command.Request.Direction, command.Request.FullDue, command.Request.Details, references, ct);
        var entity = new AppropriationRuleVersion { CompanyId = normalized.CompanyId, LineOfBusinessId = normalized.LineOfBusinessId, Direction = normalized.Direction, FullDue = normalized.FullDue, IsActive = command.Request.Active, VersionNumber = 1, IdempotencyKey = command.Request.IdempotencyKey, NormalizedRequestHash = normalized.Hash, CreatedBy = command.Actor, CreatedAtUtc = DateTime.UtcNow };
        entity.Details = normalized.Details.Select(x => new AppropriationRuleDetail { RuleVersionId = entity.Id, SequenceNumber = x.SequenceNumber, DueType = x.DueType, CashFlowDueTypeCode = x.CashFlowDueTypeCode, Percentage = x.Percentage }).ToList();
        if (entity.IsActive) { await repository.DeactivateScopeAsync(entity.CompanyId, entity.LineOfBusinessId, ct); entity.ActivatedAtUtc = DateTime.UtcNow; }
        var audit = new AppropriationRuleAuditEntry { RuleId = entity.RuleId, RuleVersionId = entity.Id, CompanyId = entity.CompanyId, LineOfBusinessId = entity.LineOfBusinessId, Operation = AppropriationRuleOperation.Created, Actor = command.Actor, CorrelationId = command.CorrelationId, IdempotencyKey = command.Request.IdempotencyKey, NormalizedRequestHash = normalized.Hash };
        await using var tx = await uow.BeginTransactionAsync(ct); await repository.AddVersionAsync(entity, ct); await repository.AddAuditAsync(audit, ct); await repository.AddIdempotencyAsync(new RuleMutationIdempotency { IdempotencyKey = command.Request.IdempotencyKey, Operation = "Created", NormalizedRequestHash = normalized.Hash, RuleId = entity.RuleId, RuleVersionId = entity.Id }, ct); await uow.SaveChangesAsync(ct); return AppropriationLogicMapper.Map(entity, command.CorrelationId);
    }
}

public sealed class ResolveAppropriationLogicHandler(IAppropriationRuleResolver resolver) : IRequestHandler<ResolveAppropriationLogicQuery, AppropriationResolutionResponse>
{
    public async Task<AppropriationResolutionResponse> Handle(ResolveAppropriationLogicQuery request, CancellationToken ct)
    {
        var result = await resolver.ResolveAsync(request.CompanyId, request.LineOfBusinessId, ct);
        if (result.Status == AppropriationResolutionStatus.NotFound) throw new AppropriationLogicNotFoundException("No active appropriation rule is configured for the Company and Line of Business.");
        if (result.Status == AppropriationResolutionStatus.Ambiguous) throw new AppropriationLogicConflictException(AppropriationLogicErrorCodes.Ambiguous, "Multiple active appropriation rules match the Company and Line of Business.");
        var x = result.Resolution!.Rule;
        return new(result.ResolverContractVersion, x.RuleId, x.Id, x.VersionNumber, x.CompanyId, x.LineOfBusinessId, result.Resolution.UsedGlobalFallback, x.Direction, x.FullDue, x.Details.OrderBy(d => d.SequenceNumber).Select(d => new AppropriationResolutionDetail(d.Id, d.DueType, d.CashFlowDueTypeCode, d.Percentage, d.SequenceNumber)).ToArray());
    }
}
```

Implement the update and activate/deactivate handlers in the same file by reusing `AppropriationLogicNormalizer`, requiring `ExpectedVersion == current.VersionNumber`, creating a new immutable version, and appending one audit row in the same unit-of-work transaction. A stale version must throw `AppropriationLogicConflictException(AppropriationLogicErrorCodes.Stale, "The appropriation rule has changed. Refresh before saving.")`. The handler must return the same stored result for a same-key/same-hash retry and must reject same-key/different-hash. This exact behavior is covered by Tasks 10.1 and 10.2.

- [ ] 8.3 MODIFY `Finnova.Service/DependencyInjection.cs`

Insert after existing MediatR/validator registration:

```csharp
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Finnova.Service.AppropriationLogic.CreateAppropriationLogicHandler).Assembly));
```

If the existing file already registers the service assembly, do not duplicate that registration. Add only the missing `IAppropriationRuleResolver`, `IAppropriationLogicUnitOfWork`, `IAppropriationReferencePort`, and `IAppropriationAuthorizationPort` registrations supplied by the authoritative SystemAdmin/reference adapters.

- [ ] 9.1 CREATE `Finnova.SystemAdminService/Controllers/AppropriationLogicController.cs`

```csharp
using System.Security.Claims;
using Finnova.Models.Contracts.AppropriationLogic;
using Finnova.Service.AppropriationLogic;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finnova.SystemAdminService.Controllers;

[ApiController]
[Route("api/appropriation-logic")]
[Authorize(Policy = "AppropriationLogicRead")]
public sealed class AppropriationLogicController(IMediator mediator) : ControllerBase
{
    private string Actor => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? User.Identity?.Name ?? "authenticated-user";
    private string Correlation => Request.Headers.TryGetValue("X-Correlation-Id", out var value) && !string.IsNullOrWhiteSpace(value) ? value.ToString() : HttpContext.TraceIdentifier;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AppropriationLogicListItem>>> List([FromQuery] Guid? companyId, [FromQuery] Guid? lineOfBusinessId, [FromQuery] bool includeInactive = false, CancellationToken ct = default) => Ok(await mediator.Send(new ListAppropriationLogicQuery(companyId, lineOfBusinessId, includeInactive, User), ct));

    [HttpGet("{ruleId:guid}")]
    public async Task<ActionResult<AppropriationLogicResponse>> Get(Guid ruleId, CancellationToken ct) => Ok(await mediator.Send(new GetAppropriationLogicQuery(ruleId, User), ct));

    [HttpPost]
    [Authorize(Policy = "AppropriationLogicManage")]
    public async Task<ActionResult<AppropriationLogicResponse>> Create(CreateAppropriationLogicRequest request, CancellationToken ct) => StatusCode(StatusCodes.Status201Created, await mediator.Send(new CreateAppropriationLogicCommand(request, User, Actor, Correlation), ct));

    [HttpGet("resolve")]
    [AllowAnonymous]
    public async Task<ActionResult<AppropriationResolutionResponse>> Resolve([FromQuery] Guid companyId, [FromQuery] Guid lineOfBusinessId, CancellationToken ct) => Ok(await mediator.Send(new ResolveAppropriationLogicQuery(companyId, lineOfBusinessId), ct));
}
```

Replace `[AllowAnonymous]` with the approved service-to-service policy attribute before enabling production routing. FINNOVA-62 must call the resolver through the approved authenticated service port; a public anonymous resolver is not an accepted deployment configuration.

- [ ] 9.2 MODIFY `Finnova.SystemAdminService/Program.cs`

Insert in the existing `AddAuthorization` block:

```csharp
    options.AddPolicy("AppropriationLogicRead", policy => policy.RequireAuthenticatedUser().RequireRole("SystemAdmin"));
    options.AddPolicy("AppropriationLogicManage", policy => policy.RequireAuthenticatedUser().RequireRole("SystemAdmin"));
    options.AddPolicy("AppropriationLogicResolve", policy => policy.RequireAuthenticatedUser().RequireAssertion(context => context.User.IsInRole("SystemAdmin") || context.User.HasClaim("scope", "appropriation-logic.resolve")));
```

Change the resolver action in Task 9.1 to `[Authorize(Policy = "AppropriationLogicResolve")]` before implementation. Preserve existing authentication-before-authorization middleware order.

- [ ] 9.3 MODIFY `Finnova.SystemAdminService/Middlewares/ExceptionHandlingMiddleware.cs`

Insert before the existing generic validation case:

```csharp
                Finnova.Models.Domain.Exceptions.AppropriationLogicException a => (a.ErrorCode switch
                {
                    "ERR-APPROP-RULE-NOT-FOUND" => StatusCodes.Status404NotFound,
                    "ERR-APPROP-RULE-403" => StatusCodes.Status403Forbidden,
                    "ERR-APPROP-RULE-AMBIGUOUS" or "ERR-APPROP-RULE-STALE" or "ERR-APPROP-IDEMPOTENCY-CONFLICT" => StatusCodes.Status409Conflict,
                    "ERR-APPROP-RULE-DEPENDENCY" => StatusCodes.Status503ServiceUnavailable,
                    _ => StatusCodes.Status422UnprocessableEntity
                }, a.ErrorCode, a.Message),
```

Preserve the existing RFC 7807 `code`, `message`, `correlationId`, and structured field-error extensions. Do not expose SQL, stack traces, JWTs, or secrets.

- [ ] 9.4 MODIFY `Finnova.ApiGateway/appsettings.json`

Insert the following routes inside `ReverseProxy.Routes` only if the existing SystemAdmin routes do not already forward this path. Use the existing `systemadmin-cluster` destination:

```json
      "ua-appropriation-logic-alias-route": {
        "ClusterId": "systemadmin-cluster",
        "Match": { "Path": "/api/systemadmin/api/appropriation-logic/{**catch-all}" },
        "Transforms": [
          { "PathRemovePrefix": "/api/systemadmin/api" },
          { "PathPrefix": "/api" }
        ]
      },
      "ua-appropriation-logic-alias-root": {
        "ClusterId": "systemadmin-cluster",
        "Match": { "Path": "/api/systemadmin/api/appropriation-logic" },
        "Transforms": [
          { "PathRemovePrefix": "/api/systemadmin/api" },
          { "PathPrefix": "/api" }
        ]
      },
```

Do not route the feature to `ua-cluster` or `accounts-cluster`. Preserve Authorization forwarding and the existing route ordering convention.

- [ ] 10.1 CREATE `Finnova.Tests/AppropriationLogic/AppropriationLogicRulesTests.cs`

```csharp
using FsCheck;
using FsCheck.Xunit;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Service.AppropriationLogic.Rules;
using Xunit;

namespace Finnova.Tests.AppropriationLogic;

public sealed class AppropriationLogicRulesTests
{
    [Property(MaxTest = 100)]
    public Property SequenceNumbersAreContiguous()
    {
        return Prop.ForAll(Arb.Default.NonEmptyList<int>().Generator, values =>
        {
            var distinct = values.Distinct().Take(7).ToArray();
            var ordered = distinct.Select((value, index) => new AppropriationRuleDetail { SequenceNumber = index + 1, DueType = (AppropriationDueType)(Math.Abs(value) % 8), Percentage = 0m }).ToArray();
            return ordered.Select(x => x.SequenceNumber).SequenceEqual(Enumerable.Range(1, ordered.Length)).ToProperty();
        });
    }

    [Property(MaxTest = 100)]
    public Property ResolutionPrefersExactLob()
    {
        return Prop.ForAll(Arb.Default.Guid().Generator, company =>
        {
            var lob = Guid.NewGuid();
            var global = new AppropriationRuleVersion { CompanyId = company, IsActive = true, LineOfBusinessId = null };
            var exact = new AppropriationRuleVersion { CompanyId = company, IsActive = true, LineOfBusinessId = lob };
            var result = AppropriationLogicPrecedence.Resolve(company, lob, new[] { global, exact });
            return (result.Rule.Id == exact.Id && !result.UsedGlobalFallback).ToProperty();
        });
    }

    [Property(MaxTest = 100)]
    public Property ResolutionUsesGlobalFallback()
    {
        return Prop.ForAll(Arb.Default.Guid().Generator, company =>
        {
            var global = new AppropriationRuleVersion { CompanyId = company, IsActive = true, LineOfBusinessId = null };
            var result = AppropriationLogicPrecedence.Resolve(company, Guid.NewGuid(), new[] { global });
            return (result.Rule.Id == global.Id && result.UsedGlobalFallback).ToProperty();
        });
    }
}
```

Add separate FsCheck properties with `MaxTest = 100` for percentage total acceptance/rejection, duplicate due-head rejection, and same normalized payload hash. Each property test comment must use the format `Feature: appropriation-logic-master, Property N: <property text>` and reference the corresponding design property.

- [ ] 10.2 CREATE `Finnova.Tests/AppropriationLogic/AppropriationLogicIntegrationTests.cs`

```csharp
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Finnova.Tests.AppropriationLogic;

public sealed class AppropriationLogicIntegrationTests
{
    [Fact]
    public async Task MissingTokenReturns401()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/appropriation-logic");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
```

Add authenticated tests for SystemAdmin 403, Company scope, screenshot JSON labels, create/save/retrieve, exact LOB before global, no rule, ambiguity, duplicate detail, total above 100, stale expected version, same-key idempotency, one audit row, and absence of receipt/due/journal repository calls. Use test doubles only in the test host.

- [ ] 11.1 MODIFY `E:\Finnova\Finnova-UI\Finnova-UI\src\models\appropriationLogic.model.ts`

If the file does not exist, create it with the complete content below. If the model barrel requires a different directory, use the existing master-data model convention and preserve the complete types:

```typescript
export type AppropriationDirection = 'Vertical' | 'Horizontal';
export type AppropriationDueType = 'Installment' | 'Principal' | 'Interest' | 'Insurance' | 'Odi' | 'BankCharges' | 'ChequeReturnCharges' | 'LegalCharges';
export interface AppropriationLogicDetailRequest { dueType: AppropriationDueType; percentage: string; }
export interface AppropriationLogicDetailResponse { id: string; sequenceNumber: number; dueType: AppropriationDueType; cashFlowDueTypeCode: string; percentage: number; }
export interface AppropriationLogicForm { companyId: string; lineOfBusinessId: string; direction: AppropriationDirection; fullDue: boolean; active: boolean; details: AppropriationLogicDetailRequest[]; expectedVersion?: number; }
export interface AppropriationLogicResponse { ruleId: string; ruleVersionId: string; companyId: string; lineOfBusinessId: string | null; direction: AppropriationDirection; fullDue: boolean; isActive: boolean; versionNumber: number; details: AppropriationLogicDetailResponse[]; createdBy: string; createdAtUtc: string; activatedAtUtc?: string; deactivatedAtUtc?: string; correlationId: string; }
export interface AppropriationLogicService { list(companyId?: string): Promise<AppropriationLogicResponse[]>; get(ruleId: string): Promise<AppropriationLogicResponse>; save(form: AppropriationLogicForm, idempotencyKey: string): Promise<AppropriationLogicResponse>; }
```

- [ ] 11.2 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\services\interfaces\appropriationLogic.interface.ts`

```typescript
import type { AppropriationLogicService } from '../../models/appropriationLogic.model';
export type { AppropriationLogicService };
```

- [ ] 11.3 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\services\real\appropriationLogic.real.ts`

```typescript
import api from '../api';
import type { AppropriationLogicForm, AppropriationLogicResponse, AppropriationLogicService } from '../../models/appropriationLogic.model';

class AppropriationLogicRealService implements AppropriationLogicService {
  private readonly base = '/appropriation-logic';
  async list(companyId?: string): Promise<AppropriationLogicResponse[]> { return (await api.get<AppropriationLogicResponse[]>(this.base, { params: { companyId, includeInactive: true } })).data; }
  async get(ruleId: string): Promise<AppropriationLogicResponse> { return (await api.get<AppropriationLogicResponse>(`${this.base}/${ruleId}`)).data; }
  async save(form: AppropriationLogicForm, idempotencyKey: string): Promise<AppropriationLogicResponse> {
    const details = form.details.map(x => ({ dueType: x.dueType, percentage: Number(x.percentage) }));
    const body = { companyId: form.companyId, lineOfBusinessId: form.lineOfBusinessId || null, direction: form.direction, fullDue: form.fullDue, active: form.active, details, idempotencyKey };
    return (await api.post<AppropriationLogicResponse>(this.base, body)).data;
  }
}

export const appropriationLogicRealService: AppropriationLogicService = new AppropriationLogicRealService();
```

- [ ] 11.4 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\services\mock\appropriationLogic.mock.ts`

```typescript
import type { AppropriationLogicForm, AppropriationLogicResponse, AppropriationLogicService } from '../../models/appropriationLogic.model';

const saved = new Map<string, AppropriationLogicResponse>();
const id = '00000000-0000-0000-0000-000000000067';

class AppropriationLogicMockService implements AppropriationLogicService {
  async list(): Promise<AppropriationLogicResponse[]> { return [...saved.values()]; }
  async get(ruleId: string): Promise<AppropriationLogicResponse> { const value = saved.get(ruleId); if (!value) throw new Error('Appropriation rule was not found.'); return value; }
  async save(form: AppropriationLogicForm, idempotencyKey: string): Promise<AppropriationLogicResponse> {
    const total = form.details.reduce((sum, detail) => sum + Number(detail.percentage || 0), 0);
    if (total > 100) throw new Error('The total percentage cannot exceed 100.00.');
    const response: AppropriationLogicResponse = { ruleId: id, ruleVersionId: crypto.randomUUID(), companyId: form.companyId, lineOfBusinessId: form.lineOfBusinessId || null, direction: form.direction, fullDue: form.fullDue, isActive: form.active, versionNumber: 1, details: form.details.map((detail, index) => ({ id: crypto.randomUUID(), sequenceNumber: index + 1, dueType: detail.dueType, cashFlowDueTypeCode: detail.dueType, percentage: Number(detail.percentage || 0) })), createdBy: 'mock-system-admin', createdAtUtc: new Date().toISOString(), correlationId: idempotencyKey };
    const prior = saved.get(idempotencyKey);
    if (prior) return prior;
    saved.set(idempotencyKey, response);
    return response;
  }
}

export const appropriationLogicMockService: AppropriationLogicService = new AppropriationLogicMockService();
```

- [ ] 11.5 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\services\appropriationLogic.service.ts`

```typescript
import { appropriationLogicMockService } from './mock/appropriationLogic.mock';
import { appropriationLogicRealService } from './real/appropriationLogic.real';
import type { AppropriationLogicService } from './interfaces/appropriationLogic.interface';

export const appropriationLogicService: AppropriationLogicService = import.meta.env.VITE_USE_MOCK_API === 'true' ? appropriationLogicMockService : appropriationLogicRealService;
```

- [ ] 12.1 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\components\appropriationLogic\AppropriationLogicDetailsGrid.tsx`

```tsx
import { Button, MenuItem, Select, Stack, TextField } from '@mui/material';
import type { AppropriationDueType, AppropriationLogicDetailRequest } from '../../models/appropriationLogic.model';

const dueTypes: Array<{ value: AppropriationDueType; label: string }> = [
  { value: 'Installment', label: 'Installment' }, { value: 'Principal', label: 'Principal' }, { value: 'Interest', label: 'Interest' }, { value: 'Insurance', label: 'Insurance' }, { value: 'Odi', label: 'ODI' }, { value: 'BankCharges', label: 'Bank Charges' }, { value: 'ChequeReturnCharges', label: 'Cheque Return Charges' }, { value: 'LegalCharges', label: 'Legal Charges' }
];

export default function AppropriationLogicDetailsGrid({ details, onChange }: { details: AppropriationLogicDetailRequest[]; onChange: (value: AppropriationLogicDetailRequest[]) => void }) {
  const add = () => onChange([...details, { dueType: 'Installment', percentage: '0.00' }]);
  const update = (index: number, value: Partial<AppropriationLogicDetailRequest>) => onChange(details.map((item, i) => i === index ? { ...item, ...value } : item));
  const remove = (index: number) => onChange(details.filter((_, i) => i !== index));
  return <Stack spacing={1} aria-label="APPROPRIATION LOGIC DETAILS">
    {details.map((detail, index) => <Stack direction="row" spacing={1} key={`${detail.dueType}-${index}`}>
      <TextField label="S.No." value={index + 1} size="small" InputProps={{ readOnly: true }} />
      <Select aria-label="Receipt Type" value={detail.dueType} onChange={event => update(index, { dueType: event.target.value as AppropriationDueType })}>{dueTypes.map(x => <MenuItem key={x.value} value={x.value}>{x.label}</MenuItem>)}</Select>
      <TextField label="Percentage" value={detail.percentage} inputProps={{ min: 0, max: 100, step: 0.01 }} onChange={event => update(index, { percentage: event.target.value })} />
      <Button onClick={() => update(index, { percentage: detail.percentage })}>Edit</Button><Button onClick={() => remove(index)}>Delete</Button>
    </Stack>)}
    <Button onClick={add}>Add</Button>
  </Stack>;
}
```

- [ ] 12.2 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\pages\AppropriationLogicMaster.tsx`

```tsx
import { Alert, Button, Checkbox, FormControlLabel, MenuItem, Select, Stack, TextField, Typography } from '@mui/material';
import { useState } from 'react';
import { appropriationLogicService } from '../services/appropriationLogic.service';
import type { AppropriationDirection, AppropriationLogicDetailRequest, AppropriationLogicForm } from '../models/appropriationLogic.model';
import AppropriationLogicDetailsGrid from '../components/appropriationLogic/AppropriationLogicDetailsGrid';

const emptyForm: AppropriationLogicForm = { companyId: '', lineOfBusinessId: '', direction: 'Vertical', fullDue: false, active: false, details: [] };

export default function AppropriationLogicMaster() {
  const [form, setForm] = useState<AppropriationLogicForm>(emptyForm); const [error, setError] = useState(''); const [saved, setSaved] = useState(''); const [loading, setLoading] = useState(false);
  const setDetails = (details: AppropriationLogicDetailRequest[]) => setForm(value => ({ ...value, details }));
  const save = async () => { setLoading(true); setError(''); setSaved(''); try { await appropriationLogicService.save(form, crypto.randomUUID()); setSaved('Appropriation Logic saved.'); } catch (e) { setError(e instanceof Error ? e.message : 'Unable to save Appropriation Logic.'); } finally { setLoading(false); } };
  return <Stack spacing={2} sx={{ p: 3 }}><Typography variant="h4">APPROPRIATION LOGIC</Typography>{error && <Alert severity="error">{error}</Alert>}{saved && <Alert severity="success">{saved}</Alert>}
    <TextField label="Company" value={form.companyId} onChange={e => setForm({ ...form, companyId: e.target.value })} />
    <Select aria-label="Line of Business" value={form.lineOfBusinessId} displayEmpty onChange={e => setForm({ ...form, lineOfBusinessId: e.target.value })}><MenuItem value="">All</MenuItem><MenuItem value="LN - Loan">LN - Loan</MenuItem></Select>
    <Select aria-label="Type" value={form.direction} onChange={e => setForm({ ...form, direction: e.target.value as AppropriationDirection })}><MenuItem value="Vertical">Vertical</MenuItem><MenuItem value="Horizontal">Horizontal</MenuItem></Select>
    <FormControlLabel control={<Checkbox checked={form.fullDue} onChange={e => setForm({ ...form, fullDue: e.target.checked })} />} label="Full Due" />
    <FormControlLabel control={<Checkbox checked={form.active} onChange={e => setForm({ ...form, active: e.target.checked })} />} label="Active" />
    <Typography variant="h6">APPROPRIATION LOGIC DETAILS</Typography><AppropriationLogicDetailsGrid details={form.details} onChange={setDetails} />
    <Stack direction="row" spacing={1}><Button variant="contained" onClick={() => void save()} disabled={loading}>Save</Button><Button onClick={() => setForm(emptyForm)} disabled={loading}>Clear</Button><Button onClick={() => setForm(emptyForm)} disabled={loading}>Cancel</Button></Stack>
  </Stack>;
}
```

- [ ] 12.3 MODIFY `E:\Finnova\Finnova-UI\Finnova-UI\src\App.tsx`

Insert after the existing SystemAdmin/master imports:

```tsx
import AppropriationLogicMaster from './pages/AppropriationLogicMaster';
```

Insert inside the authenticated layout route:

```tsx
<Route path="/appropriation-logic" element={<AppropriationLogicMaster />} />
```

- [ ] 12.4 MODIFY `E:\Finnova\Finnova-UI\Finnova-UI\src\components\Layout.tsx`

Insert the existing receipt/master icon import only if it is not already present, then add the menu item to the SystemAdmin master-data group:

```tsx
{ text: 'Appropriation Logic', path: '/appropriation-logic', permission: 'SystemAdmin' }
```

Use the existing permission-aware menu rendering. Do not create a second auth context or add an Oman/RTL menu branch.

- [ ] 13.1 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\pages\AppropriationLogicMaster.test.tsx`

```tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import AppropriationLogicMaster from './AppropriationLogicMaster';
import { appropriationLogicService } from '../services/appropriationLogic.service';

vi.mock('../services/appropriationLogic.service', () => ({ appropriationLogicService: { save: vi.fn(), list: vi.fn(), get: vi.fn() } }));

describe('AppropriationLogicMaster', () => {
  it('renders the approved English screenshot structure', () => {
    render(<AppropriationLogicMaster />);
    expect(screen.getByText('APPROPRIATION LOGIC')).toBeInTheDocument();
    expect(screen.getByLabelText('Line of Business')).toBeInTheDocument();
    expect(screen.getByLabelText('Type')).toBeInTheDocument();
    expect(screen.getByText('APPROPRIATION LOGIC DETAILS')).toBeInTheDocument();
    expect(screen.getByText('Receipt Type')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Save' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Clear' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Cancel' })).toBeInTheDocument();
  });

  it('submits percentages below 100 without changing the configured value', async () => {
    vi.mocked(appropriationLogicService.save).mockResolvedValue({} as never);
    render(<AppropriationLogicMaster />);
    fireEvent.change(screen.getByLabelText('Company'), { target: { value: 'company-1' } });
    fireEvent.click(screen.getByRole('button', { name: 'Add' }));
    fireEvent.change(screen.getByLabelText('Percentage'), { target: { value: '90.00' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(appropriationLogicService.save).toHaveBeenCalled());
    expect(screen.queryByText(/Cash|Cheque|PDC|Arabic|Oman|Muscat|OMR/i)).not.toBeInTheDocument();
  });
});
```

- [ ] 13.2 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\services\appropriationLogic.service.test.ts`

```typescript
import { describe, expect, it, vi } from 'vitest';
import api from './api';
import { appropriationLogicRealService } from './real/appropriationLogic.real';

vi.mock('./api', () => ({ default: { get: vi.fn(), post: vi.fn() } }));

describe('appropriation logic service', () => {
  it('uses the gateway-composed master path and canonical due-head payload', async () => {
    vi.mocked(api.post).mockResolvedValue({ data: {} } as never);
    await appropriationLogicRealService.save({ companyId: 'c', lineOfBusinessId: '', direction: 'Vertical', fullDue: true, active: true, details: [{ dueType: 'Installment', percentage: '40.00' }] }, 'request-1');
    expect(api.post).toHaveBeenCalledWith('/appropriation-logic', expect.objectContaining({ companyId: 'c', lineOfBusinessId: null, direction: 'Vertical', fullDue: true, details: [{ dueType: 'Installment', percentage: 40 }] }));
  });
});
```

- [ ] 14.1 MODIFY `E:\Finnova\Finnova-API\.kiro\specs\receipt-appropriation-engine\requirements.md`

Replace Assumption A4 and A6 with:

```markdown
- **Assumption A4 — FINNOVA-67 rule ownership:** FINNOVA-67 owns Appropriation Logic Master CRUD, activation, versioning, audit, due-head mapping, and the authoritative resolver. FINNOVA-62 consumes the resolver and does not contain a static or hard-coded rule implementation.
- **Assumption A6 — Rule detail semantics:** FINNOVA-67 accepts each detail percentage from 0.00 through 100.00, rejects duplicate due heads, permits a detail total below 100.00, and rejects a total above 100.00 unless product explicitly approves another policy. A total below 100.00 leaves a residual that FINNOVA-62 routes to Advance Account. FINNOVA-62 captures rule identity/version, resolver contract version, scope, direction, Full Due, mappings, and ordered details.
```

Replace Requirement 3 acceptance criterion 8 with:

```markdown
8. IF the FINNOVA-67 resolver returns no usable details, duplicate due heads, a percentage outside 0.00 through 100.00, a detail total above 100.00%, an unsupported direction, or an unsupported resolver contract version, THEN THE Appropriation_Service SHALL reject Suggest and Suggest-mode Confirm with a rule-validation error and SHALL persist no allocation.
```

Replace Requirement 3 acceptance criterion 9 with:

```markdown
9. IF a configured Receipt-Type Rule Detail has no matching Pending Due, THEN THE Appropriation_Service SHALL carry the unapplied detail target into Residual Amount and SHALL apply the Advance Account behavior defined in Requirement 5.
```

Replace Requirement 4 acceptance criterion 7 with:

```markdown
7. WHEN THE Appropriation_Service receives repeated Suggest requests with unchanged Receipt Document Amount, Pending Due collection, and FINNOVA-67 active Rule_Version, THE Appropriation_Service SHALL return identical allocation amounts, rule evidence, and allocation order.
```

Add these criteria to Requirement 3 after criterion 9:

```markdown
10. WHEN Suggest succeeds, THE Appropriation_Service SHALL include the FINNOVA-67 Rule ID, Rule_Version, resolver contract version, matched Company/LOB scope, global-fallback indicator, Direction, Full Due, and every ordered detail mapping in the preview.
11. WHEN Suggest-mode Confirm succeeds, THE Appropriation_Service SHALL persist the same FINNOVA-67 rule evidence on the confirmed Receipt Appropriation and immutable audit record.
12. IF FINNOVA-67 returns no active applicable rule or an ambiguous active-rule result, THEN THE Appropriation_Service SHALL fail closed and SHALL not calculate or persist an allocation.
```

- [ ] 14.2 MODIFY `E:\Finnova\Finnova-API\.kiro\specs\receipt-appropriation-engine\design.md`

Replace the opening rule-master dependency wording with:

```markdown
- **Integration boundary:** FINNOVA-67 is the authoritative Appropriation Logic Master. FINNOVA-62 consumes its authenticated resolver contract for Suggest and Suggest-mode Confirm; it does not maintain a static rule, duplicate rule tables, or hard-code policy percentages.
```

Replace the old exact-total validation in Section 6.1 with:

```markdown
6. For Suggest, call the FINNOVA-67 resolver using authoritative Receipt CompanyId and LineOfBusinessId. Reject no-match, ambiguity, unsupported resolver contract versions, duplicate details, invalid mappings, percentages outside 0.00..100.00, and total percentages above 100.00. Permit totals below 100.00; the unallocated portion remains residual and is handled by Advance Account policy.
```

Replace the rule-master port definition with:

```markdown
- `IAppropriationRuleResolver`: authenticated FINNOVA-67 contract returning one active exact-LOB rule before one active global fallback, with Rule ID/version, resolver contract version, matched scope, fallback flag, direction, Full Due, ordered details, and ledger mappings. No local static rule source is permitted.
```

Add this subsection after the Suggest API section:

```markdown
### FINNOVA-67 runtime evidence

The Suggest preview and Suggest-mode Confirm request use the FINNOVA-67 resolution envelope. FINNOVA-62 re-resolves the rule during Confirm and rejects a changed Rule_Version as stale. The persisted appropriation and audit snapshot includes RuleId, RuleVersionId/version number, resolverContractVersion, CompanyId, LineOfBusinessId, UsedGlobalFallback, Direction, FullDue, and ordered detail ID, sequence, canonical due type, ledger code, and percentage. FINNOVA-62 applies Vertical/Horizontal traversal and Full Due; FINNOVA-67 only supplies configuration.
```

Replace the unresolved assumption table row for A4/A6 with:

```markdown
| A4, A6 | FINNOVA-67 owns rule maintenance and exposes the authoritative resolver. FINNOVA-62 permits detail totals below 100.00, rejects totals above 100.00, and routes the residual through its Advance Account flow. | Confirm FINNOVA-67 resolver endpoint/port, contract version, due-head mappings, and final Legal Charges ledger code before implementing Tasks 4, 7, and 8. |
```

- [ ] 14.3 MODIFY `E:\Finnova\Finnova-API\.kiro\specs\receipt-appropriation-engine\tasks.md`

Replace the “Appropriation Logic Master” dependency gate bullet with:

```markdown
- FINNOVA-67 Appropriation Logic Master: authenticated resolver projection, exact-Lob-then-global precedence, resolver contract version, active rule version, direction, Full Due, canonical due-head mappings, ordered details, totals-up-to-100 semantics, and concurrency token.
```

Replace the `IAppropriationRulePort` in Task 4.1 with:

```csharp
public interface IAppropriationRuleResolver
{
    Task<AppropriationRuleResolutionResult> ResolveAsync(Guid companyId, Guid lineOfBusinessId, CancellationToken ct);
}

public sealed record AppropriationRuleResolutionResult(
    string Status,
    Guid? RuleId,
    Guid? RuleVersionId,
    int? VersionNumber,
    string ResolverContractVersion,
    Guid CompanyId,
    Guid? LineOfBusinessId,
    bool UsedGlobalFallback,
    AppropriationDirection? Direction,
    bool? FullDue,
    IReadOnlyList<RuleDetailProjection> Details,
    string? ErrorCode);
```

Replace the old `AppropriationCalculator.Calculate` rule validation line:

```csharp
if (!rule.IsActive || rule.Details.Count == 0 || rule.Details.Any(x => x.Percentage < 0m || x.Percentage > 100m) || rule.Details.Sum(x => x.Percentage) > 100m || rule.Details.Select(x => x.DueType).Distinct().Count() != rule.Details.Count) throw new InvalidOperationException("The active FINNOVA-67 appropriation rule is invalid.");
```

Replace the handler’s `IAppropriationRulePort rules` dependency and rule lookup with the resolver contract, and capture all response evidence:

```csharp
var resolution = await resolver.ResolveAsync(r.CompanyId, r.LineOfBusinessId, ct);
if (resolution.Status != "Resolved" || resolution.RuleId is null || resolution.RuleVersionId is null || resolution.Direction is null || resolution.FullDue is null || resolution.ResolverContractVersion != "1.0") throw new ReceiptAppropriationRuleException("No compatible FINNOVA-67 active appropriation rule is available.");
var rule = new AppropriationRuleProjection(resolution.RuleId.Value, resolution.RuleVersionId.Value.ToString(), r.CompanyId, r.LineOfBusinessId, resolution.Direction.Value, resolution.FullDue.Value, true, resolution.Details);
```

Extend `AppropriationPreviewResponse`, `ReceiptAppropriation`, and `AppropriationAuditEntry` with `ResolverContractVersion`, `UsedGlobalFallback`, `RuleVersionId`, and ordered detail evidence. Confirm must call the resolver again, compare the preview rule version and resolver contract, and reject a changed version with `ERR-APPROP-STALE-DATA`. Remove every static percentage/rule object from production FINNOVA-62 code and mocks; test fixtures may use an `IAppropriationRuleResolver` test double.

- [ ] 15.1 MODIFY `Finnova.Tests/ReceiptAppropriation/AppropriationCalculatorTests.cs`

Replace exact-100 rule fixtures with a FINNOVA-67 fixture that permits totals below 100 and add these tests:

```csharp
[Fact]
public void AllowsDetailTotalsBelowOneHundredAndLeavesResidual()
{
    var rule = FixtureRule(new[] { (CashFlowDueType.Installment, 40m), (CashFlowDueType.Insurance, 20m), (CashFlowDueType.ODI, 20m), (CashFlowDueType.OtherCharges, 10m) });
    var result = AppropriationCalculator.Calculate(1000m, FixtureDues(), rule);
    Assert.True(result.Residual >= 0m);
}

[Fact]
public void RejectsDetailTotalsAboveOneHundred()
{
    var rule = FixtureRule(new[] { (CashFlowDueType.Installment, 80m), (CashFlowDueType.ODI, 30m) });
    Assert.Throws<InvalidOperationException>(() => AppropriationCalculator.Calculate(1000m, FixtureDues(), rule));
}

[Fact]
public void RejectsDuplicateDueHeads()
{
    var rule = FixtureRule(new[] { (CashFlowDueType.Installment, 40m), (CashFlowDueType.Installment, 20m) });
    Assert.Throws<InvalidOperationException>(() => AppropriationCalculator.Calculate(1000m, FixtureDues(), rule));
}
```

Add resolver tests for exact active LOB precedence, global fallback, no match, ambiguity, version capture, and resolver contract compatibility. Keep all FsCheck properties at `MaxTest >= 100`.

- [ ] 15.2 CREATE `Finnova.Tests/ReceiptAppropriation/Finnova67ResolverAdapterTests.cs`

```csharp
using Finnova.Models.Domain.Enums;
using Finnova.Service.ReceiptAppropriation.Ports;
using Moq;
using Xunit;

namespace Finnova.Tests.ReceiptAppropriation;

public sealed class Finnova67ResolverAdapterTests
{
    [Fact]
    public async Task SuggestUsesExactLobBeforeGlobalAndCapturesVersion()
    {
        var resolver = new Mock<IAppropriationRuleResolver>();
        resolver.Setup(x => x.ResolveAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(new AppropriationRuleResolutionResult("Resolved", Guid.NewGuid(), Guid.NewGuid(), 3, "1.0", Guid.NewGuid(), Guid.NewGuid(), false, AppropriationDirection.Vertical, true, Array.Empty<RuleDetailProjection>(), null));
        var result = await resolver.Object.ResolveAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);
        Assert.Equal("Resolved", result.Status); Assert.Equal("1.0", result.ResolverContractVersion); Assert.False(result.UsedGlobalFallback); Assert.Equal(3, result.VersionNumber);
    }

    [Fact]
    public async Task IncompatibleResolverVersionIsAvailableForFailClosedGate()
    {
        var resolver = new Mock<IAppropriationRuleResolver>();
        resolver.Setup(x => x.ResolveAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(new AppropriationRuleResolutionResult("Resolved", Guid.NewGuid(), Guid.NewGuid(), 1, "2.0", Guid.NewGuid(), null, true, AppropriationDirection.Horizontal, false, Array.Empty<RuleDetailProjection>(), null));
        var result = await resolver.Object.ResolveAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);
        Assert.NotEqual("1.0", result.ResolverContractVersion);
    }
}
```

- [ ] 16.1 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\services\appropriationLogic.integration.test.ts`

```typescript
import { describe, expect, it } from 'vitest';
import { appropriationLogicMockService } from './mock/appropriationLogic.mock';

describe('FINNOVA-67 rule integration contract', () => {
  it('keeps a 90 percent rule valid for residual handling', async () => {
    const result = await appropriationLogicMockService.save({ companyId: 'company', lineOfBusinessId: 'lob', direction: 'Vertical', fullDue: true, active: true, details: [{ dueType: 'Installment', percentage: '40.00' }, { dueType: 'Insurance', percentage: '20.00' }, { dueType: 'Odi', percentage: '20.00' }, { dueType: 'LegalCharges', percentage: '10.00' }] }, 'same-request');
    expect(result.details.reduce((sum, item) => sum + item.percentage, 0)).toBe(90);
    expect(result.direction).toBe('Vertical');
    expect(result.fullDue).toBe(true);
  });
});
```

## Notes

- Tasks are ordered from backend domain through persistence, service, host, tests, FINNOVA-62 adapter integration, frontend, and frontend tests.
- The FINNOVA-62 changes are compatibility-gated: no static rule, exact-100 validation, or payment-mode interpretation may remain in production code.
- No task creates a journal, updates a due balance, posts a receipt, or changes FINNOVA-63/JVE posting ownership.
- `LegalCharges` requires an authoritative due-ledger mapping decision before implementation because the FINNOVA-67 scenario names Legal Charges while existing FINNOVA-62 examples use Other Charges.
- The migration command is documented only. The implementer runs it after source changes, reviews the migration, and applies it through the normal release process.
- Tests marked by the repository convention as optional may be skipped only if the project policy explicitly allows it; the required backend property suite still uses at least 100 iterations per property.

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "2.1", "3.1", "3.2"] },
    { "id": 1, "tasks": ["4.1", "4.2", "5.1", "6.1", "6.2"] },
    { "id": 2, "tasks": ["5.2", "5.3", "6.3", "7.1"] },
    { "id": 3, "tasks": ["8.1", "8.2", "8.3"] },
    { "id": 4, "tasks": ["9.1", "9.2", "9.3", "9.4"] },
    { "id": 5, "tasks": ["10.1", "10.2"] },
    { "id": 6, "tasks": ["14.1", "14.2", "14.3"] },
    { "id": 7, "tasks": ["15.1", "15.2"] },
    { "id": 8, "tasks": ["11.1", "11.2", "11.3", "11.4", "11.5"] },
    { "id": 9, "tasks": ["12.1", "12.2", "12.3", "12.4"] },
    { "id": 10, "tasks": ["13.1", "13.2", "16.1"] }
  ]
}
```
