# Implementation Plan: FINNOVA-65 Challan Rule Creation and Deposit Bank Mapping

## Overview

This file is a copy-paste implementation script for FINNOVA-65. Apply tasks in order. This spec update changes only the three spec documents; do not modify source files, run builds, run tests, or run migrations while reviewing or delivering the spec.

The implementation uses one `ChallanRuleSet` per `LineOfBusiness + Location/level-5 Branch + DepositBankCode`, with a direct `DraweeBankMappingRow[]` collection. The creation/edit screen never contains multiple deposit-bank groups. Tasks marked `*` are optional test tasks, but every test task remains in the dependency graph.

## Compatibility gates

- Confirm the authoritative Deposit Bank Codes source. If the source identifies an account by account code rather than bank code, preserve the screenshot label `Deposit Bank Codes` and map the request to that account-code identity. Do not reuse customer `BankAccount` as an F&A deposit account without approval.
- Confirm the existing `LineOfBusiness`, level-5 `Location`/Branch, active FINNOVA-16 `DraweeBank`, Receipt Processing, and FINNOVA-63 JVE contract names before copying adapters. Use `BranchId` internally for the UI's `Location` field.
- Leave `Finnova.Models.Domain.Entities.ChallanRule` and `ChallanRuleConfiguration` unchanged. FINNOVA-16 `ChallanRule` remains the separate format/validation/routing entity keyed only by `DraweeBankId`.
- Before applying the migration command, inspect existing migration history and confirm the approved foreign-key ownership. The command is documented here for the later implementation task and is not run as part of spec delivery.
- Preserve existing project namespaces and registration conventions if the repository uses a nearby equivalent. Do not create a second DbContext, HTTP client, Redux store, RxJS pipeline, receipt aggregate, or journal owner.

## Tasks

- [ ] 1. Create backend one-set domain entities, lifecycle enum, and exceptions
  - [ ] 1.1 CREATE `Finnova.Models/Domain/Enums/ChallanRuleSetLifecycle.cs`
    ```csharp
    namespace Finnova.Models.Domain.Enums;

    public enum ChallanRuleSetLifecycle
    {
        Inactive = 0,
        Active = 1
    }
    ```
    _Requirements: 2.6, 5.1, 8.1_

  - [ ] 1.2 CREATE `Finnova.Models/Domain/Entities/ChallanRuleSet.cs`
    ```csharp
    using Finnova.Models.Domain.Enums;

    namespace Finnova.Models.Domain.Entities;

    public sealed class ChallanRuleSet
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid LineOfBusinessId { get; set; }
        public Guid BranchId { get; set; }
        public string DepositBankCode { get; set; } = string.Empty;
        public Guid DepositBankAccountId { get; set; }
        public string DepositBankNameSnapshot { get; set; } = string.Empty;
        public string AccountNumberSnapshot { get; set; } = string.Empty;
        public string? DepositBankBranchSnapshot { get; set; }
        public string? MicrCodeSnapshot { get; set; }
        public string? GlCodeSnapshot { get; set; }
        public string? SlCodeSnapshot { get; set; }
        public ChallanRuleSetLifecycle Lifecycle { get; set; } = ChallanRuleSetLifecycle.Active;
        public bool IsActive => Lifecycle == ChallanRuleSetLifecycle.Active;
        public DateOnly EffectiveFrom { get; set; }
        public DateOnly? EffectiveTo { get; set; }
        public long RunningRuleVersion { get; set; } = 1;
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
        public ICollection<DraweeBankMappingRow> DraweeBankRows { get; set; } = new List<DraweeBankMappingRow>();
    }
    ```
    _Requirements: 2.6, 3.5, 5.1, 6.1, 8.1_

  - [ ] 1.3 CREATE `Finnova.Models/Domain/Entities/DraweeBankMappingRow.cs`
    ```csharp
    namespace Finnova.Models.Domain.Entities;

    public sealed class DraweeBankMappingRow
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ChallanRuleSetId { get; set; }
        public string DraweeBankKey { get; set; } = string.Empty;
        public bool IsOthers { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
    ```
    _Requirements: 4.1–4.6, 5.2–5.4_

  - [ ] 1.4 CREATE `Finnova.Models/Domain/Entities/ChallanRuleSetAuditEntry.cs` and `ChallanRuleSetIdempotency.cs`
    ```csharp
    namespace Finnova.Models.Domain.Entities;

    public sealed class ChallanRuleSetAuditEntry
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ChallanRuleSetId { get; set; }
        public Guid? MappingRowId { get; set; }
        public string Operation { get; set; } = string.Empty;
        public string? BeforeSnapshot { get; set; }
        public string? AfterSnapshot { get; set; }
        public string ChangedBy { get; set; } = string.Empty;
        public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
        public long RuleVersion { get; set; }
    }

    public sealed class ChallanRuleSetIdempotency
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Operation { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string RequestFingerprint { get; set; } = string.Empty;
        public string ResultJson { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
    ```
    _Requirements: 6.1–6.5, 8.1, 9.5_

  - [ ] 1.5 CREATE `Finnova.Models/Domain/Exceptions/ChallanRuleSetExceptions.cs`
    ```csharp
    namespace Finnova.Models.Domain.Exceptions;

    public abstract class ChallanRuleSetException : Exception
    {
        protected ChallanRuleSetException(string message, string code) : base(message) => Code = code;
        public string Code { get; }
    }

    public sealed class ChallanRuleSetValidationException(string message)
        : ChallanRuleSetException(message, "ERR-CRS-400");

    public sealed class ChallanRuleSetNotFoundException(string message)
        : ChallanRuleSetException(message, "ERR-CRS-404");

    public sealed class ChallanRuleSetConflictException(string message)
        : ChallanRuleSetException(message, "ERR-CRS-409");

    public sealed class ChallanRuleSetConcurrencyException(string message)
        : ChallanRuleSetException(message, "ERR-CRS-412");

    public sealed class ChallanRuleSetConfigurationException(string message)
        : ChallanRuleSetException(message, "ERR-CRS-422");

    public sealed class ChallanRuleSetDependencyException(string message)
        : ChallanRuleSetException(message, "ERR-CRS-503");
    ```
    _Requirements: 3.2–3.3, 5.1–5.5, 6.2–6.3, 9.1–9.4_

- [ ] 2. Create backend contracts, lookup ports, and runtime snapshot contract
  - [ ] 2.1 CREATE `Finnova.Models/Contracts/ChallanRuleSets/ChallanRuleSetContracts.cs`
    ```csharp
    namespace Finnova.Models.Contracts.ChallanRuleSets;

    public sealed record ChallanRuleSetRowRequest(string DraweeBankKey);

    public sealed record CreateChallanRuleSetRequest(
        Guid LineOfBusinessId,
        Guid BranchId,
        string DepositBankCode,
        bool IsActive,
        DateOnly EffectiveFrom,
        DateOnly? EffectiveTo,
        IReadOnlyList<ChallanRuleSetRowRequest> DraweeBankRows,
        string IdempotencyKey);

    public sealed record UpdateChallanRuleSetRequest(
        Guid BranchId,
        string DepositBankCode,
        bool IsActive,
        DateOnly EffectiveFrom,
        DateOnly? EffectiveTo,
        IReadOnlyList<ChallanRuleSetRowRequest> DraweeBankRows,
        long ExpectedRuleVersion,
        string IdempotencyKey);

    public sealed record ChallanRuleSetRowResponse(
        Guid Id,
        string DraweeBankKey,
        bool IsOthers,
        bool IsActive);

    public sealed record ChallanRuleSetResponse(
        Guid Id,
        Guid LineOfBusinessId,
        string LineOfBusinessName,
        Guid BranchId,
        string LocationName,
        string DepositBankCode,
        Guid DepositBankAccountId,
        string DepositBankName,
        string AccountNumber,
        bool IsActive,
        DateOnly EffectiveFrom,
        DateOnly? EffectiveTo,
        long RunningRuleVersion,
        IReadOnlyList<ChallanRuleSetRowResponse> DraweeBankRows,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc);

    public sealed record ChallanRuleSetAuditResponse(
        Guid Id,
        Guid ChallanRuleSetId,
        Guid? MappingRowId,
        string Operation,
        string? BeforeSnapshot,
        string? AfterSnapshot,
        string ChangedBy,
        DateTime ChangedAtUtc,
        long RuleVersion);

    public sealed record LineOfBusinessOption(Guid Id, string DisplayName, bool IsActive);
    public sealed record LocationOption(Guid BranchId, string DisplayName, int Level, bool IsActive);
    public sealed record DepositBankCodeOption(
        string DepositBankCode,
        Guid DepositBankAccountId,
        string DepositBankName,
        string AccountNumber,
        bool IsActive);

    public sealed record ChallanRoutingResolveRequest(
        Guid LineOfBusinessId,
        Guid BranchId,
        string DraweeBankKey,
        DateOnly EvaluationDate,
        string CorrelationId);

    public sealed record ChallanRoutingSnapshot(
        Guid ChallanRuleSetId,
        long RuleVersion,
        string MatchedDraweeBankKey,
        bool UsedOthers,
        Guid DepositBankAccountId,
        string DepositBankCode,
        string DepositBankName,
        string AccountNumber,
        string? DepositBankBranch,
        string? MicrCode,
        string? GlCode,
        string? SlCode,
        DateTime ResolvedAtUtc,
        string CorrelationId);
    ```
    _Requirements: 3.1–3.5, 4.1–4.6, 6.1–6.6, 8.1–8.5_

  - [ ] 2.2 CREATE `Finnova.Models/Contracts/ChallanRuleSets/IChallanRoutingResolver.cs`
    ```csharp
    using Finnova.Models.Contracts.ChallanRuleSets;

    namespace Finnova.Models.Contracts.ChallanRuleSets;

    public interface IChallanRoutingResolver
    {
        Task<ChallanRoutingSnapshot> ResolveAsync(
            ChallanRoutingResolveRequest request,
            CancellationToken cancellationToken = default);
    }
    ```
    _Requirements: 8.3–8.6, 9.4_

  - [ ] 2.3 CREATE `Finnova.Service/ChallanRuleSet/Ports/ReferencePorts.cs`
    ```csharp
    namespace Finnova.Service.ChallanRuleSet.Ports;

    public sealed record LineOfBusinessReference(Guid Id, string Name, bool IsActive);
    public sealed record LocationReference(Guid BranchId, string Name, int Level, bool IsActive);
    public sealed record DraweeBankReference(Guid Id, string BankCode, bool IsActive);
    public sealed record DepositBankAccountReference(
        Guid Id,
        string DepositBankCode,
        string DepositBankName,
        string AccountNumber,
        string? BranchName,
        string? MicrCode,
        string? GlCode,
        string? SlCode,
        bool IsActive);

    public interface ILineOfBusinessReference
    {
        Task<LineOfBusinessReference?> GetAsync(Guid id, CancellationToken cancellationToken);
    }

    public interface ILocationReference
    {
        Task<LocationReference?> GetLevelFiveAsync(Guid branchId, CancellationToken cancellationToken);
        Task<IReadOnlyList<LocationReference>> GetActiveForLineOfBusinessAsync(Guid lobId, CancellationToken cancellationToken);
    }

    public interface IDraweeBankReference
    {
        Task<DraweeBankReference?> GetByCodeAsync(string normalizedCode, CancellationToken cancellationToken);
    }

    public interface IDepositBankAccountReference
    {
        Task<IReadOnlyList<DepositBankAccountReference>> GetActiveByCodeAsync(string depositBankCode, Guid branchId, CancellationToken cancellationToken);
    }
    ```
    _Requirements: 2.1–2.3, 3.1–3.3, 6.2_

- [ ] 3. Create EF Core model configuration and DbSets
  - [ ] 3.1 MODIFY `Finnova.Repository/Context/FinnovaDbContext.cs` — add the following DbSets beside existing master-data DbSets
    ```csharp
    public DbSet<ChallanRuleSet> ChallanRuleSets => Set<ChallanRuleSet>();
    public DbSet<DraweeBankMappingRow> DraweeBankMappingRows => Set<DraweeBankMappingRow>();
    public DbSet<ChallanRuleSetAuditEntry> ChallanRuleSetAuditEntries => Set<ChallanRuleSetAuditEntry>();
    public DbSet<ChallanRuleSetIdempotency> ChallanRuleSetIdempotencies => Set<ChallanRuleSetIdempotency>();
    ```
    Add the required `using Finnova.Models.Domain.Entities;` if absent. Do not change FINNOVA-16 DbSets.
    _Requirements: 6.1, 8.1_

  - [ ] 3.2 CREATE `Finnova.Repository/Configuration/ChallanRuleSetConfiguration.cs`
    ```csharp
    using Finnova.Models.Domain.Entities;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;

    namespace Finnova.Repository.Configuration;

    public sealed class ChallanRuleSetConfiguration : IEntityTypeConfiguration<ChallanRuleSet>
    {
        public void Configure(EntityTypeBuilder<ChallanRuleSet> builder)
        {
            builder.ToTable("challan_rule_sets");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.DepositBankCode).IsRequired().HasMaxLength(50);
            builder.Property(x => x.DepositBankNameSnapshot).IsRequired().HasMaxLength(200);
            builder.Property(x => x.AccountNumberSnapshot).IsRequired().HasMaxLength(50);
            builder.Property(x => x.DepositBankBranchSnapshot).HasMaxLength(150);
            builder.Property(x => x.Lifecycle).IsRequired();
            builder.Property(x => x.RunningRuleVersion).IsRequired();
            builder.Property(x => x.RowVersion).IsRowVersion();
            builder.HasIndex(x => new { x.LineOfBusinessId, x.BranchId, x.DepositBankCode })
                .HasFilter("[Lifecycle] = 1").IsUnique();
            builder.HasMany(x => x.DraweeBankRows).WithOne().HasForeignKey(x => x.ChallanRuleSetId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public sealed class DraweeBankMappingRowConfiguration : IEntityTypeConfiguration<DraweeBankMappingRow>
    {
        public void Configure(EntityTypeBuilder<DraweeBankMappingRow> builder)
        {
            builder.ToTable("challan_rule_set_drawee_bank_rows");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.DraweeBankKey).IsRequired().HasMaxLength(50);
            builder.HasIndex(x => new { x.ChallanRuleSetId, x.DraweeBankKey })
                .IsUnique();
        }
    }

    public sealed class ChallanRuleSetAuditEntryConfiguration : IEntityTypeConfiguration<ChallanRuleSetAuditEntry>
    {
        public void Configure(EntityTypeBuilder<ChallanRuleSetAuditEntry> builder)
        {
            builder.ToTable("challan_rule_set_audit_entries");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Operation).IsRequired().HasMaxLength(50);
            builder.Property(x => x.ChangedBy).IsRequired().HasMaxLength(200);
            builder.Property(x => x.BeforeSnapshot).HasColumnType("nvarchar(max)");
            builder.Property(x => x.AfterSnapshot).HasColumnType("nvarchar(max)");
            builder.HasIndex(x => new { x.ChallanRuleSetId, x.ChangedAtUtc, x.Id });
        }
    }

    public sealed class ChallanRuleSetIdempotencyConfiguration : IEntityTypeConfiguration<ChallanRuleSetIdempotency>
    {
        public void Configure(EntityTypeBuilder<ChallanRuleSetIdempotency> builder)
        {
            builder.ToTable("challan_rule_set_idempotencies");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Operation).IsRequired().HasMaxLength(80);
            builder.Property(x => x.Key).IsRequired().HasMaxLength(100);
            builder.Property(x => x.RequestFingerprint).IsRequired().HasMaxLength(128);
            builder.Property(x => x.ResultJson).IsRequired().HasColumnType("nvarchar(max)");
            builder.HasIndex(x => new { x.Operation, x.Key }).IsUnique();
        }
    }
    ```
    The active concrete-key uniqueness across different sets and the active `OTHERS` uniqueness across a LOB + Location require a database-supported scope guard, filtered/indexed projection, or serializable transaction strategy. Document the chosen provider-specific enforcement in the migration rather than weakening the invariant.
    _Requirements: 5.1–5.5, 6.1–6.3_

  - [ ] 3.3 MODIFY `<generated EF migration files>` — generate only after Tasks 1–3.2 and the compatibility gate pass
    ```powershell
    dotnet ef migrations add AddChallanRuleSetDepositBankMapping --project Finnova.Repository --startup-project Finnova.SystemAdminService
    ```
    The migration must create the set, row, audit, and idempotency tables, active-set uniqueness enforcement, scope-wide concrete/OTHERS uniqueness enforcement, concurrency column, and indexes. Do not add an FK to customer `BankAccount` without approval. This command is for the later implementation task; do not run it during spec delivery.
    _Requirements: 5.1–5.5, 6.1, 8.1_

- [ ] 4. Create normalization, resolver, repositories, and DI registration
  - [ ] 4.1 CREATE `Finnova.Service/ChallanRuleSet/Domain/DraweeBankKeyNormalizer.cs`
    ```csharp
    using System.Text.RegularExpressions;
    using Finnova.Models.Domain.Exceptions;

    namespace Finnova.Service.ChallanRuleSet.Domain;

    public static partial class DraweeBankKeyNormalizer
    {
        public const string Others = "OTHERS";
        private const int MaxLength = 50;

        public static string Normalize(string? value)
        {
            var normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
            if (normalized.Length == 0)
                throw new ChallanRuleSetValidationException("Drawee Bank is required.");
            if (normalized.Length > MaxLength)
                throw new ChallanRuleSetValidationException("Drawee Bank must be at most 50 characters.");
            if (!KeyPattern().IsMatch(normalized))
                throw new ChallanRuleSetValidationException("Drawee Bank must contain only letters and digits.");
            return normalized;
        }

        public static bool IsOthers(string value) => Normalize(value) == Others;

        [GeneratedRegex("^[A-Z0-9]+$", RegexOptions.CultureInvariant)]
        private static partial Regex KeyPattern();
    }
    ```
    _Requirements: 4.1, 4.4, 5.4, 10.2_

  - [ ] 4.2 CREATE `Finnova.Service/ChallanRuleSet/Domain/ChallanRuleSetResolver.cs`
    ```csharp
    using Finnova.Models.Contracts.ChallanRuleSets;
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Exceptions;

    namespace Finnova.Service.ChallanRuleSet.Domain;

    public static class ChallanRuleSetResolver
    {
        public static ChallanRoutingSnapshot Resolve(
            ChallanRuleSet set,
            IReadOnlyCollection<DraweeBankMappingRow> rows,
            string incomingKey,
            DateTime resolvedAtUtc,
            string correlationId)
        {
            if (!set.IsActive)
                throw new ChallanRuleSetConfigurationException("No active challan rule exists for this LOB and Location.");

            var normalized = DraweeBankKeyNormalizer.Normalize(incomingKey);
            var applicable = rows.Where(row => row.IsActive).ToArray();
            var explicitRows = applicable.Where(row => !row.IsOthers && row.DraweeBankKey == normalized).ToArray();
            var candidates = explicitRows.Length > 0
                ? explicitRows
                : applicable.Where(row => row.IsOthers).ToArray();

            if (candidates.Length == 0)
                throw new ChallanRuleSetConfigurationException("No active challan rule exists for this LOB and Location.");
            if (candidates.Length != 1)
                throw new ChallanRuleSetConfigurationException("Multiple active challan rules exist for this LOB and Location.");

            var row = candidates[0];
            return new ChallanRoutingSnapshot(
                set.Id,
                set.RunningRuleVersion,
                row.DraweeBankKey,
                row.IsOthers,
                set.DepositBankAccountId,
                set.DepositBankCode,
                set.DepositBankNameSnapshot,
                set.AccountNumberSnapshot,
                set.DepositBankBranchSnapshot,
                set.MicrCodeSnapshot,
                set.GlCodeSnapshot,
                set.SlCodeSnapshot,
                resolvedAtUtc,
                correlationId);
        }
    }
    ```
    _Requirements: 4.5, 5.6, 8.3–8.6, 9.4_

  - [ ] 4.3 CREATE `Finnova.Repository/Interfaces/IChallanRuleSetRepository.cs`
    ```csharp
    using Finnova.Models.Domain.Entities;

    namespace Finnova.Repository.Interfaces;

    public interface IChallanRuleSetRepository
    {
        Task<(IReadOnlyList<ChallanRuleSet> Items, int Total)> GetPagedAsync(string? search, bool? isActive, int page, int pageSize, CancellationToken cancellationToken);
        Task<ChallanRuleSet?> GetDetailAsync(Guid id, CancellationToken cancellationToken);
        Task<ChallanRuleSet?> GetActiveForResolutionAsync(Guid lobId, Guid branchId, DateOnly evaluationDate, CancellationToken cancellationToken);
        Task<bool> ActiveSetExistsAsync(Guid lobId, Guid branchId, string depositBankCode, Guid? excludingId, CancellationToken cancellationToken);
        Task<bool> ActiveConcreteKeyExistsAsync(Guid lobId, Guid branchId, string key, Guid? excludingSetId, CancellationToken cancellationToken);
        Task<bool> ActiveOthersExistsAsync(Guid lobId, Guid branchId, Guid? excludingSetId, CancellationToken cancellationToken);
        Task AddAsync(ChallanRuleSet set, CancellationToken cancellationToken);
    }
    ```
    _Requirements: 5.1–5.5, 8.1–8.2_

  - [ ] 4.4 CREATE `Finnova.Repository/Interfaces/IChallanRuleSetAuditRepository.cs` and `IChallanRuleSetIdempotencyRepository.cs`
    ```csharp
    using Finnova.Models.Domain.Entities;

    namespace Finnova.Repository.Interfaces;

    public interface IChallanRuleSetAuditRepository
    {
        Task<IReadOnlyList<ChallanRuleSetAuditEntry>> GetAsync(Guid setId, CancellationToken cancellationToken);
        Task AddAsync(ChallanRuleSetAuditEntry entry, CancellationToken cancellationToken);
    }

    public interface IChallanRuleSetIdempotencyRepository
    {
        Task<ChallanRuleSetIdempotency?> FindAsync(string operation, string key, CancellationToken cancellationToken);
        Task AddAsync(ChallanRuleSetIdempotency entry, CancellationToken cancellationToken);
    }
    ```
    _Requirements: 6.4–6.5, 8.1, 9.5_

  - [ ] 4.5 CREATE `Finnova.Repository/Repositories/ChallanRuleSetRepository.cs`, audit repository, idempotency repository, and add DI registrations
    ```csharp
    public sealed class ChallanRuleSetRepository(FinnovaDbContext db) : IChallanRuleSetRepository
    {
        public async Task<(IReadOnlyList<ChallanRuleSet> Items, int Total)> GetPagedAsync(string? search, bool? isActive, int page, int pageSize, CancellationToken ct)
        {
            var query = db.ChallanRuleSets.AsNoTracking().Include(x => x.DraweeBankRows).AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(x => x.DepositBankCode.Contains(term) || x.DepositBankNameSnapshot.Contains(term) || x.DraweeBankRows.Any(r => r.DraweeBankKey.Contains(term)));
            }
            if (isActive.HasValue) query = query.Where(x => x.IsActive == isActive.Value);
            var total = await query.CountAsync(ct);
            var items = await query.OrderBy(x => x.LineOfBusinessId).ThenBy(x => x.BranchId).ThenBy(x => x.DepositBankCode)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
            return (items, total);
        }

        public Task<ChallanRuleSet?> GetDetailAsync(Guid id, CancellationToken ct)
            => db.ChallanRuleSets.Include(x => x.DraweeBankRows).SingleOrDefaultAsync(x => x.Id == id, ct);

        public Task<ChallanRuleSet?> GetActiveForResolutionAsync(Guid lobId, Guid branchId, DateOnly date, CancellationToken ct)
            => db.ChallanRuleSets.AsNoTracking().Include(x => x.DraweeBankRows)
                .Where(x => x.LineOfBusinessId == lobId && x.BranchId == branchId && x.IsActive && x.EffectiveFrom <= date && (x.EffectiveTo == null || x.EffectiveTo >= date))
                .SingleOrDefaultAsync(ct);

        public Task<bool> ActiveSetExistsAsync(Guid lobId, Guid branchId, string code, Guid? excludingId, CancellationToken ct)
            => db.ChallanRuleSets.AnyAsync(x => x.IsActive && x.LineOfBusinessId == lobId && x.BranchId == branchId && x.DepositBankCode == code && (!excludingId.HasValue || x.Id != excludingId.Value), ct);

        public Task<bool> ActiveConcreteKeyExistsAsync(Guid lobId, Guid branchId, string key, Guid? excludingSetId, CancellationToken ct)
            => db.ChallanRuleSets.AnyAsync(x => x.IsActive && x.LineOfBusinessId == lobId && x.BranchId == branchId && (!excludingSetId.HasValue || x.Id != excludingSetId.Value) && x.DraweeBankRows.Any(r => r.IsActive && !r.IsOthers && r.DraweeBankKey == key), ct);

        public Task<bool> ActiveOthersExistsAsync(Guid lobId, Guid branchId, Guid? excludingSetId, CancellationToken ct)
            => db.ChallanRuleSets.AnyAsync(x => x.IsActive && x.LineOfBusinessId == lobId && x.BranchId == branchId && (!excludingSetId.HasValue || x.Id != excludingSetId.Value) && x.DraweeBankRows.Any(r => r.IsActive && r.IsOthers), ct);

        public async Task AddAsync(ChallanRuleSet set, CancellationToken ct) => await db.ChallanRuleSets.AddAsync(set, ct);
    }
    ```
    Place audit/idempotency implementations beside the repository and register `IChallanRuleSetRepository`, `IChallanRuleSetAuditRepository`, and `IChallanRuleSetIdempotencyRepository` in the existing repository DI method. Add the concrete-key and OTHERS checks under a serializable transaction or the chosen provider-specific scope guard.
    _Requirements: 5.1–5.5, 6.1–6.5, 8.1–8.2_

- [ ] 5. Implement CQRS full-replacement Save, mapping, validation, audit, and concurrency
  - [ ] 5.1 CREATE `Finnova.Service/ChallanRuleSet/Commands/ChallanRuleSetCommands.cs`
    ```csharp
    using Finnova.Models.Contracts.ChallanRuleSets;
    using MediatR;

    namespace Finnova.Service.ChallanRuleSet.Commands;

    public sealed record CreateChallanRuleSetCommand(CreateChallanRuleSetRequest Request, string ActorId) : IRequest<ChallanRuleSetResponse>;
    public sealed record UpdateChallanRuleSetCommand(Guid SetId, UpdateChallanRuleSetRequest Request, string ActorId) : IRequest<ChallanRuleSetResponse>;
    public sealed record GetChallanRuleSetQuery(Guid SetId) : IRequest<ChallanRuleSetResponse>;
    public sealed record GetChallanRuleSetsQuery(string? Search, bool? IsActive, int Page, int PageSize) : IRequest<object>;
    public sealed record GetChallanRuleSetAuditQuery(Guid SetId) : IRequest<IReadOnlyList<ChallanRuleSetAuditResponse>>;
    ```
    _Requirements: 6.1–6.6, 8.1–8.2_

  - [ ] 5.2 CREATE `Finnova.Service/ChallanRuleSet/ChallanRuleSetMapper.cs`
    ```csharp
    using Finnova.Models.Contracts.ChallanRuleSets;
    using Finnova.Models.Domain.Entities;

    namespace Finnova.Service.ChallanRuleSet;

    public static class ChallanRuleSetMapper
    {
        public static ChallanRuleSetResponse ToResponse(this ChallanRuleSet set, string lobName, string locationName)
            => new(set.Id, set.LineOfBusinessId, lobName, set.BranchId, locationName, set.DepositBankCode,
                set.DepositBankAccountId, set.DepositBankNameSnapshot, set.AccountNumberSnapshot, set.IsActive,
                set.EffectiveFrom, set.EffectiveTo, set.RunningRuleVersion,
                set.DraweeBankRows.Where(row => row.IsActive).OrderBy(row => row.DraweeBankKey)
                    .Select(row => new ChallanRuleSetRowResponse(row.Id, row.DraweeBankKey, row.IsOthers, row.IsActive)).ToArray(),
                set.CreatedAtUtc, set.UpdatedAtUtc);
    }
    ```
    _Requirements: 3.5, 4.4, 6.4, 8.2_

  - [ ] 5.3 CREATE `Finnova.Service/ChallanRuleSet/Commands/ChallanRuleSetCommandHandlers.cs`
    ```csharp
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using Finnova.Models.Contracts.ChallanRuleSets;
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Context;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.ChallanRuleSet.Domain;
    using Finnova.Service.ChallanRuleSet.Ports;
    using MediatR;
    using Microsoft.EntityFrameworkCore;

    namespace Finnova.Service.ChallanRuleSet.Commands;

    public sealed class CreateChallanRuleSetHandler(
        IChallanRuleSetRepository repository,
        IChallanRuleSetAuditRepository audit,
        IChallanRuleSetIdempotencyRepository idempotency,
        ILineOfBusinessReference lobs,
        ILocationReference locations,
        IDraweeBankReference draweeBanks,
        IDepositBankAccountReference accounts,
        FinnovaDbContext db) : IRequestHandler<CreateChallanRuleSetCommand, ChallanRuleSetResponse>
    {
        public async Task<ChallanRuleSetResponse> Handle(CreateChallanRuleSetCommand message, CancellationToken ct)
        {
            var request = message.Request;
            if (request.DraweeBankRows.Count == 0) throw new ChallanRuleSetValidationException("At least one Drawee Bank is required.");
            if (request.EffectiveTo < request.EffectiveFrom) throw new ChallanRuleSetValidationException("Effective To cannot be earlier than Effective From.");
            if (await lobs.GetAsync(request.LineOfBusinessId, ct) is not { IsActive: true }) throw new ChallanRuleSetValidationException("Line of Business does not exist or is inactive.");
            if (await locations.GetLevelFiveAsync(request.BranchId, ct) is not { IsActive: true, Level: 5 }) throw new ChallanRuleSetValidationException("Location does not exist, is inactive, or is not a level-5 Location.");

            var code = request.DepositBankCode.Trim().ToUpperInvariant();
            var accountMatches = await accounts.GetActiveByCodeAsync(code, request.BranchId, ct);
            if (accountMatches.Count == 0) throw new ChallanRuleSetConfigurationException("No active deposit-bank account exists for the selected Deposit Bank Code.");
            if (accountMatches.Count != 1) throw new ChallanRuleSetConfigurationException("Multiple active deposit-bank accounts exist for the selected Deposit Bank Code.");
            if (request.IsActive && await repository.ActiveSetExistsAsync(request.LineOfBusinessId, request.BranchId, code, null, ct)) throw new ChallanRuleSetConflictException("An active Challan Rule Set already exists for this LOB, Location, and Deposit Bank Code.");

            var normalized = new List<(string Key, bool IsOthers)>();
            foreach (var rawRow in request.DraweeBankRows)
            {
                var key = DraweeBankKeyNormalizer.Normalize(rawRow.DraweeBankKey);
                if (normalized.Any(row => row.Key == key)) throw new ChallanRuleSetConflictException("Duplicate Drawee Bank rows are not allowed.");
                if (key != DraweeBankKeyNormalizer.Others && await draweeBanks.GetByCodeAsync(key, ct) is not { IsActive: true }) throw new ChallanRuleSetValidationException($"Drawee Bank '{key}' does not exist or is inactive.");
                if (request.IsActive && key != DraweeBankKeyNormalizer.Others && await repository.ActiveConcreteKeyExistsAsync(request.LineOfBusinessId, request.BranchId, key, null, ct)) throw new ChallanRuleSetConflictException("Drawee bank is already mapped for this LOB and Location.");
                if (request.IsActive && key == DraweeBankKeyNormalizer.Others && await repository.ActiveOthersExistsAsync(request.LineOfBusinessId, request.BranchId, null, ct)) throw new ChallanRuleSetConflictException("OTHERS is already mapped for this LOB and Location.");
                normalized.Add((key, key == DraweeBankKeyNormalizer.Others));
            }

            var account = accountMatches[0];
            var set = new ChallanRuleSet { LineOfBusinessId = request.LineOfBusinessId, BranchId = request.BranchId, DepositBankCode = code, DepositBankAccountId = account.Id, DepositBankNameSnapshot = account.DepositBankName, AccountNumberSnapshot = account.AccountNumber, DepositBankBranchSnapshot = account.BranchName, MicrCodeSnapshot = account.MicrCode, GlCodeSnapshot = account.GlCode, SlCodeSnapshot = account.SlCode, Lifecycle = request.IsActive ? Finnova.Models.Domain.Enums.ChallanRuleSetLifecycle.Active : Finnova.Models.Domain.Enums.ChallanRuleSetLifecycle.Inactive, EffectiveFrom = request.EffectiveFrom, EffectiveTo = request.EffectiveTo };
            foreach (var row in normalized) set.DraweeBankRows.Add(new DraweeBankMappingRow { ChallanRuleSetId = set.Id, DraweeBankKey = row.Key, IsOthers = row.IsOthers });

            await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            await repository.AddAsync(set, ct);
            await audit.AddAsync(new ChallanRuleSetAuditEntry { ChallanRuleSetId = set.Id, Operation = "CreateSet", AfterSnapshot = JsonSerializer.Serialize(set), ChangedBy = message.ActorId, RuleVersion = set.RunningRuleVersion }, ct);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            var lob = await lobs.GetAsync(set.LineOfBusinessId, ct) ?? throw new ChallanRuleSetDependencyException("Line of Business could not be loaded after Save.");
            var location = await locations.GetLevelFiveAsync(set.BranchId, ct) ?? throw new ChallanRuleSetDependencyException("Location could not be loaded after Save.");
            return set.ToResponse(lob.Name, location.Name);
        }
    }
    ```
    Implement the update handler in the same file using the same validation sequence. It must load the set, check `ExpectedRuleVersion`, resolve the single selected account, normalize and validate the complete incoming row list, inactivate removed rows, retain matching row IDs, add new rows, increment `RunningRuleVersion` once, write one audit event, and commit all changes in one transaction. Idempotent equivalent retries return the stored result without a second version or audit increment.
    _Requirements: 2.6, 3.1–3.5, 4.1–4.6, 5.1–5.5, 6.1–6.6, 9.3, 9.5_

  - [ ] 5.4 CREATE validators, query handlers, and service registration
    ```csharp
    using FluentValidation;
    using Finnova.Models.Contracts.ChallanRuleSets;

    namespace Finnova.Service.ChallanRuleSet.Validation;

    public sealed class CreateChallanRuleSetRequestValidator : AbstractValidator<CreateChallanRuleSetRequest>
    {
        public CreateChallanRuleSetRequestValidator()
        {
            RuleFor(x => x.LineOfBusinessId).NotEmpty();
            RuleFor(x => x.BranchId).NotEmpty();
            RuleFor(x => x.DepositBankCode).NotEmpty().MaximumLength(50);
            RuleFor(x => x.DraweeBankRows).NotEmpty();
            RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(100);
        }
    }

    public sealed class UpdateChallanRuleSetRequestValidator : AbstractValidator<UpdateChallanRuleSetRequest>
    {
        public UpdateChallanRuleSetRequestValidator()
        {
            RuleFor(x => x.BranchId).NotEmpty();
            RuleFor(x => x.DepositBankCode).NotEmpty().MaximumLength(50);
            RuleFor(x => x.DraweeBankRows).NotEmpty();
            RuleFor(x => x.ExpectedRuleVersion).GreaterThan(0);
            RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(100);
        }
    }
    ```
    Register MediatR handlers, validators, reference adapters, repository implementations, and `IChallanRoutingResolver` in the existing service/repository DI methods. Do not register a second authentication or database stack.
    _Requirements: 6.1–6.6, 8.1–8.3_

- [ ] 6. Add SystemAdmin controller, middleware mapping, and gateway route
  - [ ] 6.1 CREATE `Finnova.SystemAdminService/Controllers/ChallanRuleSetController.cs`
    ```csharp
    using System.Security.Claims;
    using Finnova.Models.Contracts.ChallanRuleSets;
    using Finnova.Service.ChallanRuleSet.Commands;
    using MediatR;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

    namespace Finnova.SystemAdminService.Controllers;

    [ApiController]
    [Route("api/challan-rule-sets")]
    [Authorize(Policy = "SystemAdmin")]
    public sealed class ChallanRuleSetController(IMediator mediator) : ControllerBase
    {
        [HttpGet("{id:guid}")]
        public Task<ChallanRuleSetResponse> Get(Guid id, CancellationToken ct)
            => mediator.Send(new GetChallanRuleSetQuery(id), ct);

        [HttpPost]
        public Task<ChallanRuleSetResponse> Create(CreateChallanRuleSetRequest request, CancellationToken ct)
            => mediator.Send(new CreateChallanRuleSetCommand(request, ActorId()), ct);

        [HttpPut("{id:guid}")]
        public Task<ChallanRuleSetResponse> Update(Guid id, UpdateChallanRuleSetRequest request, CancellationToken ct)
            => mediator.Send(new UpdateChallanRuleSetCommand(id, request, ActorId()), ct);

        [HttpGet("{id:guid}/audit")]
        public Task<IReadOnlyList<ChallanRuleSetAuditResponse>> Audit(Guid id, CancellationToken ct)
            => mediator.Send(new GetChallanRuleSetAuditQuery(id), ct);

        private string ActorId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? string.Empty;
    }
    ```
    Add the existing list endpoint shape beside the detail endpoint if the service uses a separate query route. Do not add row-level mutation endpoints; the UI submits the complete row list through Create/Update.
    _Requirements: 8.1–8.2, 9.1–9.2_

  - [ ] 6.2 MODIFY `Finnova.SystemAdminService/Middlewares/ExceptionHandlingMiddleware.cs` — add this branch before the generic exception branch
    ```csharp
    if (exception is Finnova.Models.Domain.Exceptions.ChallanRuleSetException ruleSetException)
    {
        var status = ruleSetException.Code switch
        {
            "ERR-CRS-404" => StatusCodes.Status404NotFound,
            "ERR-CRS-409" => StatusCodes.Status409Conflict,
            "ERR-CRS-412" => StatusCodes.Status412PreconditionFailed,
            "ERR-CRS-422" => StatusCodes.Status422UnprocessableEntity,
            "ERR-CRS-503" => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status400BadRequest
        };
        context.Response.StatusCode = status;
        await WriteProblemDetailsAsync(context, status, ruleSetException.Code, ruleSetException.Message);
        return;
    }
    ```
    Preserve all existing FINNOVA-16 exception mappings.
    _Requirements: 3.2–3.3, 5.1–5.5, 6.2–6.3, 9.3–9.4_

  - [ ] 6.3 MODIFY `Finnova.ApiGateway/appsettings.json` — add only if the existing SystemAdmin catch-all does not already forward the route
    ```json
    "challan-rule-set-route": {
      "ClusterId": "systemadmin-cluster",
      "Match": { "Path": "/api/systemadmin/api/challan-rule-sets/{**catch-all}" },
      "Transforms": [ { "PathPattern": "/api/challan-rule-sets/{**catch-all}" } ]
    },
    "ua-challan-rule-set-alias-route": {
      "ClusterId": "systemadmin-cluster",
      "Match": { "Path": "/api/ua/api/challan-rule-sets/{**catch-all}" },
      "Transforms": [ { "PathPattern": "/api/challan-rule-sets/{**catch-all}" } ]
    }
    ```
    Do not add duplicate routes. Keep the UI alias only if the shared UI axios base URL requires it.
    _Requirements: 8.1–8.2, 10.3_

- [ ] 7. Add pre-posting resolver adapter and JVE snapshot boundary
  - [ ] 7.1 CREATE `Finnova.Service/ChallanRuleSet/ChallanRoutingResolverService.cs`
    ```csharp
    using Finnova.Models.Contracts.ChallanRuleSets;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.ChallanRuleSet.Domain;

    namespace Finnova.Service.ChallanRuleSet;

    public sealed class ChallanRoutingResolverService(IChallanRuleSetRepository repository) : IChallanRoutingResolver
    {
        public async Task<ChallanRoutingSnapshot> ResolveAsync(ChallanRoutingResolveRequest request, CancellationToken cancellationToken = default)
        {
            var set = await repository.GetActiveForResolutionAsync(request.LineOfBusinessId, request.BranchId, request.EvaluationDate, cancellationToken)
                ?? throw new ChallanRuleSetConfigurationException("No active challan rule exists for this LOB and Location.");
            return ChallanRuleSetResolver.Resolve(set, set.DraweeBankRows, request.DraweeBankKey, DateTime.UtcNow, request.CorrelationId);
        }
    }
    ```
    _Requirements: 4.5–4.6, 8.3–8.6, 9.4_

  - [ ] 7.2 CREATE `Finnova.Service/ChallanRuleSet/Adapters/ReceiptChallanRoutingAdapter.cs`
    ```csharp
    using Finnova.Models.Contracts.ChallanRuleSets;
    using Finnova.Models.Domain.Exceptions;

    namespace Finnova.Service.ChallanRuleSet.Adapters;

    public interface IReceiptRoutingSnapshotStore
    {
        Task SaveAsync(Guid receiptId, ChallanRoutingSnapshot snapshot, CancellationToken cancellationToken);
        Task<bool> ExistsAsync(Guid receiptId, CancellationToken cancellationToken);
    }

    public interface IReceiptPostingStateReader
    {
        Task<bool> IsPostedAsync(Guid receiptId, CancellationToken cancellationToken);
    }

    public interface IReceiptChallanRoutingAdapter
    {
        Task<ChallanRoutingSnapshot> ResolveAndPersistSnapshotAsync(Guid receiptId, ChallanRoutingResolveRequest request, CancellationToken cancellationToken = default);
        Task EnsureSnapshotBeforePostingAsync(Guid receiptId, CancellationToken cancellationToken = default);
    }

    public sealed class ReceiptChallanRoutingAdapter(
        IChallanRoutingResolver resolver,
        IReceiptRoutingSnapshotStore snapshots,
        IReceiptPostingStateReader postingState) : IReceiptChallanRoutingAdapter
    {
        public async Task<ChallanRoutingSnapshot> ResolveAndPersistSnapshotAsync(Guid receiptId, ChallanRoutingResolveRequest request, CancellationToken cancellationToken = default)
        {
            if (await postingState.IsPostedAsync(receiptId, cancellationToken))
                throw new ChallanRuleSetConfigurationException("Posted records are not re-resolved by Challan Rule Engine.");
            var snapshot = await resolver.ResolveAsync(request, cancellationToken);
            await snapshots.SaveAsync(receiptId, snapshot, cancellationToken);
            return snapshot;
        }

        public async Task EnsureSnapshotBeforePostingAsync(Guid receiptId, CancellationToken cancellationToken = default)
        {
            if (!await snapshots.ExistsAsync(receiptId, cancellationToken))
                throw new ChallanRuleSetConfigurationException("Challan routing must be resolved before JV submission.");
        }
    }
    ```
    _Requirements: 8.4–8.6, 9.4–9.5_

  - [ ] 7.3 MODIFY the authoritative FINNOVA-61 receipt/challan contract and FINNOVA-63 JVE mapper at the confirmed insertion points
    ```csharp
    // Add these fields to the existing receipt/challan routing snapshot contract only
    // when equivalent delivered fields do not already exist.
    public Guid? ChallanRuleSetId { get; set; }
    public long? ChallanRuleVersion { get; set; }
    public string? ChallanMatchedDraweeBankKey { get; set; }
    public bool? ChallanUsedOthers { get; set; }
    public Guid? ChallanDepositBankAccountId { get; set; }
    public string? ChallanDepositBankCodeSnapshot { get; set; }
    public string? ChallanDepositBankNameSnapshot { get; set; }
    public string? ChallanDepositAccountNumberSnapshot { get; set; }
    public string? ChallanDepositBankBranchSnapshot { get; set; }
    public string? ChallanDepositMicrCodeSnapshot { get; set; }
    public string? ChallanDepositGlCodeSnapshot { get; set; }
    public string? ChallanDepositSlCodeSnapshot { get; set; }
    public DateTime? ChallanRoutingResolvedAtUtc { get; set; }
    public string? ChallanRoutingCorrelationId { get; set; }
    ```
    Before creating the approved FINNOVA-63 request, call `EnsureSnapshotBeforePostingAsync`. Copy the persisted snapshot into the JVE request without inserting or updating canonical journal entities. Do not call the resolver when the receipt is already posted. Use delivered FINNOVA-61/63 names when equivalent fields already exist.
    _Requirements: 8.4–8.6, 10.5_

- [ ]* 8. Add backend unit, property, and integration tests
  - [ ]* 8.1 CREATE `Finnova.Tests/ChallanRuleSet/ChallanRuleSetUnitTests.cs`
    ```csharp
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Service.ChallanRuleSet.Domain;
    using Xunit;

    namespace Finnova.Tests.ChallanRuleSet;

    public sealed class ChallanRuleSetUnitTests
    {
        [Fact]
        public void Others_alias_is_canonicalized()
            => Assert.Equal("OTHERS", DraweeBankKeyNormalizer.Normalize("  others "));

        [Fact]
        public void Invalid_key_is_rejected()
            => Assert.Throws<ChallanRuleSetValidationException>(() => DraweeBankKeyNormalizer.Normalize("HDFC-BANK"));

        [Fact]
        public void Explicit_row_wins_over_others()
        {
            var set = new ChallanRuleSet { DepositBankCode = "HDFC", DepositBankAccountId = Guid.NewGuid(), DepositBankNameSnapshot = "HDFC Bank", AccountNumberSnapshot = "1234567890" };
            var explicitRow = new DraweeBankMappingRow { ChallanRuleSetId = set.Id, DraweeBankKey = "HDFC", IsOthers = false };
            var othersRow = new DraweeBankMappingRow { ChallanRuleSetId = set.Id, DraweeBankKey = "OTHERS", IsOthers = true };
            set.DraweeBankRows.Add(explicitRow);
            set.DraweeBankRows.Add(othersRow);
            var result = ChallanRuleSetResolver.Resolve(set, set.DraweeBankRows.ToArray(), "hdfc", DateTime.UtcNow, "test");
            Assert.Equal("HDFC", result.MatchedDraweeBankKey);
            Assert.False(result.UsedOthers);
        }

        [Fact]
        public void Missing_row_fails_closed()
        {
            var set = new ChallanRuleSet { DepositBankCode = "HDFC", DepositBankAccountId = Guid.NewGuid(), DepositBankNameSnapshot = "HDFC Bank", AccountNumberSnapshot = "1234567890" };
            Assert.Throws<ChallanRuleSetConfigurationException>(() => ChallanRuleSetResolver.Resolve(set, Array.Empty<DraweeBankMappingRow>(), "SBI", DateTime.UtcNow, "test"));
        }
    }
    ```
    _Requirements: 3.2–3.3, 4.5, 5.6, 9.4, 10.1_

  - [ ]* 8.2 CREATE `Finnova.Tests/ChallanRuleSet/ChallanRuleSetPropertyTests.cs`
    ```csharp
    using FsCheck;
    using FsCheck.Xunit;
    using Finnova.Models.Domain.Entities;
    using Finnova.Service.ChallanRuleSet.Domain;

    namespace Finnova.Tests.ChallanRuleSet;

    public sealed class ChallanRuleSetPropertyTests
    {
        // Feature: challan-rule-engine-deposit-bank-mapping, Property 1: Key normalization is idempotent
        [Property(MaxTest = 100)]
        public Property Key_normalization_is_idempotent(NonEmptyString value)
        {
            var candidate = new string(value.Get.Where(char.IsLetterOrDigit).ToArray());
            if (candidate.Length == 0) return true.ToProperty();
            var once = DraweeBankKeyNormalizer.Normalize(candidate);
            return (once == DraweeBankKeyNormalizer.Normalize(once)).ToProperty();
        }

        // Feature: challan-rule-engine-deposit-bank-mapping, Property 3: Explicit mapping precedes OTHERS fallback
        [Property(MaxTest = 100)]
        public Property Explicit_mapping_precedes_others(PositiveInt seed)
        {
            var key = $"BANK{seed.Get}";
            var set = new ChallanRuleSet { DepositBankCode = "HDFC", DepositBankAccountId = Guid.NewGuid(), DepositBankNameSnapshot = "HDFC Bank", AccountNumberSnapshot = "1234567890" };
            set.DraweeBankRows.Add(new DraweeBankMappingRow { ChallanRuleSetId = set.Id, DraweeBankKey = key });
            set.DraweeBankRows.Add(new DraweeBankMappingRow { ChallanRuleSetId = set.Id, DraweeBankKey = "OTHERS", IsOthers = true });
            var result = ChallanRuleSetResolver.Resolve(set, set.DraweeBankRows.ToArray(), key.ToLowerInvariant(), DateTime.UtcNow, "pbt");
            return (!result.UsedOthers && result.MatchedDraweeBankKey == key).ToProperty();
        }
    }
    ```
    Add one property-based test for full-replacement duplicate rejection using the actual command handler fixture. Keep each property at 100 iterations or higher and retain the exact feature/property tag.
    _Requirements: 4.4–4.5, 5.1–5.6, 6.1–6.3, 10.2_

  - [ ]* 8.3 CREATE `Finnova.Tests/ChallanRuleSet/ChallanRuleSetApiTests.cs`
    ```csharp
    using System.Net;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Xunit;

    namespace Finnova.Tests.ChallanRuleSet;

    public sealed class ChallanRuleSetApiTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient client;

        public ChallanRuleSetApiTests(WebApplicationFactory<Program> factory) => client = factory.CreateClient();

        [Fact]
        public async Task Missing_authentication_returns_unauthorized()
        {
            using var response = await client.GetAsync("/api/challan-rule-sets/00000000-0000-0000-0000-000000000001");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Missing_runtime_mapping_fails_closed()
        {
            using var response = await client.GetAsync("/internal/challan-routing/resolve?lineOfBusinessId=00000000-0000-0000-0000-000000000001&branchId=00000000-0000-0000-0000-000000000002&draweeBankKey=SBI");
            Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        }
    }
    ```
    Replace placeholder host authentication and seeded IDs with the existing `Finnova.Tests` factory. Add examples for 403, active-set conflict, concrete-key conflict, OTHERS conflict, rollback, and full-replacement update. Do not weaken fail-closed assertions.
    _Requirements: 5.1–5.5, 6.1–6.6, 8.1–8.6, 9.1–9.4, 10.1, 10.3, 10.5_

- [ ] 9. Create frontend one-set models and service layer
  - [ ] 9.1 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\models\challanRuleSet.model.ts`
    ```typescript
    export interface LineOfBusinessOption { id: string; displayName: string; isActive: boolean; }
    export interface LocationOption { branchId: string; displayName: string; level: number; isActive: boolean; }
    export interface DepositBankCodeOption { depositBankCode: string; depositBankAccountId: string; depositBankName: string; accountNumber: string; isActive: boolean; }
    export interface ChallanRuleSetRow { id: string; draweeBankKey: string; isOthers: boolean; isActive: boolean; }
    export interface ChallanRuleSet { id: string; lineOfBusinessId: string; lineOfBusinessName: string; branchId: string; locationName: string; depositBankCode: string; depositBankAccountId: string; depositBankName: string; accountNumber: string; isActive: boolean; effectiveFrom: string; effectiveTo: string | null; runningRuleVersion: number; draweeBankRows: ChallanRuleSetRow[]; createdAtUtc: string; updatedAtUtc: string; }
    export interface ChallanRuleSetRowRequest { draweeBankKey: string; }
    export interface CreateChallanRuleSetRequest { lineOfBusinessId: string; branchId: string; depositBankCode: string; isActive: boolean; effectiveFrom: string; effectiveTo: string | null; draweeBankRows: ChallanRuleSetRowRequest[]; idempotencyKey: string; }
    export interface UpdateChallanRuleSetRequest extends Omit<CreateChallanRuleSetRequest, 'lineOfBusinessId'> { expectedRuleVersion: number; }
    export interface Paginated<T> { data: T[]; total: number; page: number; pageSize: number; totalPages: number; }
    export interface ChallanRuleSetAudit { id: string; challanRuleSetId: string; mappingRowId: string | null; operation: string; beforeSnapshot: string | null; afterSnapshot: string | null; changedBy: string; changedAtUtc: string; ruleVersion: number; }
    ```
    _Requirements: 1.1–1.7, 2.1–2.6, 3.1–3.5, 4.1–4.6, 8.1–8.2_

  - [ ] 9.2 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\services\interfaces\challanRuleSet.service.ts`
    ```typescript
    import type { ChallanRuleSet, ChallanRuleSetAudit, CreateChallanRuleSetRequest, DepositBankCodeOption, LineOfBusinessOption, LocationOption, Paginated, UpdateChallanRuleSetRequest } from '../../models/challanRuleSet.model';

    export interface ChallanRuleSetService {
      list(search: string, page: number, pageSize: number, isActive?: boolean): Promise<Paginated<ChallanRuleSet>>;
      get(id: string): Promise<ChallanRuleSet>;
      getLineOfBusinesses(): Promise<LineOfBusinessOption[]>;
      getLocations(lineOfBusinessId: string): Promise<LocationOption[]>;
      getDepositBankCodes(lineOfBusinessId: string, branchId: string): Promise<DepositBankCodeOption[]>;
      create(request: CreateChallanRuleSetRequest): Promise<ChallanRuleSet>;
      update(id: string, request: UpdateChallanRuleSetRequest): Promise<ChallanRuleSet>;
      audit(id: string): Promise<ChallanRuleSetAudit[]>;
    }
    ```
    _Requirements: 2.1–2.3, 6.1–6.6, 8.1–8.2_

  - [ ] 9.3 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\services\real\challanRuleSet.service.ts`
    ```typescript
    import api from '../api';
    import type { ChallanRuleSet, ChallanRuleSetAudit, CreateChallanRuleSetRequest, DepositBankCodeOption, LineOfBusinessOption, LocationOption, Paginated, UpdateChallanRuleSetRequest } from '../../models/challanRuleSet.model';
    import type { ChallanRuleSetService } from '../interfaces/challanRuleSet.service';

    export const realChallanRuleSetService: ChallanRuleSetService = {
      async list(search, page, pageSize, isActive) { return (await api.get<Paginated<ChallanRuleSet>>('/challan-rule-sets', { params: { search, page, pageSize, isActive } })).data; },
      async get(id) { return (await api.get<ChallanRuleSet>(`/challan-rule-sets/${id}`)).data; },
      async getLineOfBusinesses() { return (await api.get<LineOfBusinessOption[]>('/line-of-businesses', { params: { isActive: true } })).data; },
      async getLocations(lineOfBusinessId) { return (await api.get<LocationOption[]>('/locations', { params: { lineOfBusinessId, level: 5, isActive: true } })).data; },
      async getDepositBankCodes(lineOfBusinessId, branchId) { return (await api.get<DepositBankCodeOption[]>('/deposit-bank-codes', { params: { lineOfBusinessId, branchId, isActive: true } })).data; },
      async create(request) { return (await api.post<ChallanRuleSet>('/challan-rule-sets', request)).data; },
      async update(id, request) { return (await api.put<ChallanRuleSet>(`/challan-rule-sets/${id}`, request)).data; },
      async audit(id) { return (await api.get<ChallanRuleSetAudit[]>(`/challan-rule-sets/${id}/audit`)).data; },
    };
    ```
    Preserve the shared axios instance and its centralized 401/403/409/timeout handling.
    _Requirements: 2.1–2.3, 3.1–3.5, 6.1–6.6, 8.1–8.2_

  - [ ] 9.4 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\services\mock\challanRuleSet.service.ts` and `src/services/challanRuleSet.service.ts`
    ```typescript
    import type { ChallanRuleSetService } from '../interfaces/challanRuleSet.service';
    import type { ChallanRuleSet, CreateChallanRuleSetRequest, UpdateChallanRuleSetRequest } from '../../models/challanRuleSet.model';

    const clone = <T,>(value: T): T => structuredClone(value);
    const lobs = [{ id: 'lob-loan', displayName: 'LN - Loan', isActive: true }];
    const locations = [{ branchId: 'branch-mumbai', displayName: 'Mumbai', level: 5, isActive: true }];
    const banks = [
      { depositBankCode: 'HDFC', depositBankAccountId: 'account-hdfc', depositBankName: 'HDFC Bank', accountNumber: '000123456789', isActive: true },
      { depositBankCode: 'SBI', depositBankAccountId: 'account-sbi', depositBankName: 'State Bank of India', accountNumber: '000987654321', isActive: true },
    ];
    let saved: ChallanRuleSet = { id: 'set-mumbai-hdfc', lineOfBusinessId: 'lob-loan', lineOfBusinessName: 'LN - Loan', branchId: 'branch-mumbai', locationName: 'Mumbai', depositBankCode: 'HDFC', depositBankAccountId: 'account-hdfc', depositBankName: 'HDFC Bank', accountNumber: '000123456789', isActive: true, effectiveFrom: '2025-01-01', effectiveTo: null, runningRuleVersion: 1, draweeBankRows: [{ id: 'row-hdfc', draweeBankKey: 'HDFC', isOthers: false, isActive: true }, { id: 'row-sbi', draweeBankKey: 'SBI', isOthers: false, isActive: true }], createdAtUtc: '2025-01-01T00:00:00Z', updatedAtUtc: '2025-01-01T00:00:00Z' };

    export const mockChallanRuleSetService: ChallanRuleSetService = {
      async list(search, page, pageSize) { const data = !search || `${saved.lineOfBusinessName} ${saved.locationName} ${saved.depositBankCode}`.toLowerCase().includes(search.toLowerCase()) ? [clone(saved)] : []; return { data, total: data.length, page, pageSize, totalPages: Math.ceil(data.length / pageSize) }; },
      async get() { return clone(saved); },
      async getLineOfBusinesses() { return clone(lobs); },
      async getLocations() { return clone(locations); },
      async getDepositBankCodes() { return clone(banks); },
      async create(request: CreateChallanRuleSetRequest) { saved = { ...saved, ...request, id: crypto.randomUUID(), lineOfBusinessName: 'LN - Loan', locationName: 'Mumbai', depositBankName: request.depositBankCode === 'SBI' ? 'State Bank of India' : 'HDFC Bank', depositBankAccountId: request.depositBankCode === 'SBI' ? 'account-sbi' : 'account-hdfc', accountNumber: request.depositBankCode === 'SBI' ? '000987654321' : '000123456789', draweeBankRows: request.draweeBankRows.map((row, index) => ({ id: `row-${index}`, draweeBankKey: row.draweeBankKey.trim().toUpperCase(), isOthers: row.draweeBankKey.trim().toUpperCase() === 'OTHERS', isActive: true })), runningRuleVersion: 1, updatedAtUtc: new Date().toISOString() }; return clone(saved); },
      async update(_id, request: UpdateChallanRuleSetRequest) { if (request.expectedRuleVersion !== saved.runningRuleVersion) throw new Error('The Challan Rule Set was changed by another user. Refresh before saving.'); saved = { ...saved, ...request, draweeBankRows: request.draweeBankRows.map((row, index) => ({ id: saved.draweeBankRows[index]?.id ?? `row-${index}`, draweeBankKey: row.draweeBankKey.trim().toUpperCase(), isOthers: row.draweeBankKey.trim().toUpperCase() === 'OTHERS', isActive: true })), runningRuleVersion: saved.runningRuleVersion + 1, updatedAtUtc: new Date().toISOString() }; return clone(saved); },
      async audit() { return []; },
    };
    ```
    In `src/services/challanRuleSet.service.ts`, select `mockChallanRuleSetService` when `import.meta.env.VITE_USE_MOCK_API === 'true'`, otherwise the real implementation, and export the interface/model barrels. Keep all fixtures India-specific and English-only.
    _Requirements: 1.1–1.7, 2.1–2.6, 3.1–3.5, 4.1–4.6, 6.4–6.6, 10.4–10.5_

- [ ] 10. Create the screenshot-matching Challan Rule Creation page, route, and navigation
  - [ ] 10.1 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\pages\system-admin\ChallanRuleCreation.tsx`
    ```tsx
    import { useEffect, useMemo, useState } from 'react';
    import { Alert, Box, Button, Checkbox, FormControl, FormControlLabel, InputLabel, MenuItem, Paper, Select, Stack, Table, TableBody, TableCell, TableHead, TableRow, TextField, Typography } from '@mui/material';
    import { useNavigate, useParams } from 'react-router-dom';
    import { challanRuleSetService } from '../../services/challanRuleSet.service';
    import type { ChallanRuleSetRowRequest, DepositBankCodeOption, LineOfBusinessOption, LocationOption } from '../../models/challanRuleSet.model';

    export default function ChallanRuleCreation() {
      const navigate = useNavigate();
      const { id } = useParams<{ id: string }>();
      const [lobs, setLobs] = useState<LineOfBusinessOption[]>([]);
      const [locations, setLocations] = useState<LocationOption[]>([]);
      const [banks, setBanks] = useState<DepositBankCodeOption[]>([]);
      const [lobId, setLobId] = useState('');
      const [branchId, setBranchId] = useState('');
      const [bankCode, setBankCode] = useState('');
      const [accountNo, setAccountNo] = useState('');
      const [active, setActive] = useState(true);
      const [rows, setRows] = useState<ChallanRuleSetRowRequest[]>([]);
      const [editingIndex, setEditingIndex] = useState<number | null>(null);
      const [draftBank, setDraftBank] = useState('');
      const [ruleSetId, setRuleSetId] = useState<string | undefined>(id);
      const [version, setVersion] = useState(1);
      const [error, setError] = useState<string | null>(null);
      const [saving, setSaving] = useState(false);
      const selectedBank = useMemo(() => banks.find(bank => bank.depositBankCode === bankCode), [banks, bankCode]);

      useEffect(() => { void challanRuleSetService.getLineOfBusinesses().then(setLobs).catch(error => setError(error instanceof Error ? error.message : 'Unable to load Line of Business.')); }, []);
      useEffect(() => { if (!lobId) { setLocations([]); return; } void challanRuleSetService.getLocations(lobId).then(setLocations).catch(error => setError(error instanceof Error ? error.message : 'Unable to load Location.')); }, [lobId]);
      useEffect(() => { if (!lobId || !branchId) { setBanks([]); return; } void challanRuleSetService.getDepositBankCodes(lobId, branchId).then(setBanks).catch(error => setError(error instanceof Error ? error.message : 'Unable to load Deposit Bank Codes.')); }, [lobId, branchId]);
      useEffect(() => { if (selectedBank) setAccountNo(selectedBank.accountNumber); else setAccountNo(''); }, [selectedBank]);
      useEffect(() => { if (!ruleSetId) return; void challanRuleSetService.get(ruleSetId).then(set => { setLobId(set.lineOfBusinessId); setBranchId(set.branchId); setBankCode(set.depositBankCode); setAccountNo(set.accountNumber); setActive(set.isActive); setRows(set.draweeBankRows.filter(row => row.isActive).map(row => ({ draweeBankKey: row.draweeBankKey }))); setVersion(set.runningRuleVersion); }).catch(error => setError(error instanceof Error ? error.message : 'Unable to load Challan Rule Set.')); }, [ruleSetId]);

      const resetDependents = () => { setBranchId(''); setBankCode(''); setBanks([]); setAccountNo(''); setRows([]); setEditingIndex(null); setDraftBank(''); };
      const clear = () => { setLobId(''); setLocations([]); resetDependents(); setActive(true); setError(null); };
      const addOrEditRow = () => { const key = draftBank.trim().toUpperCase(); if (!key) { setError('Drawee Bank is required.'); return; } const next = [...rows]; if (editingIndex === null) next.push({ draweeBankKey: key }); else next[editingIndex] = { draweeBankKey: key }; setRows(next); setDraftBank(''); setEditingIndex(null); setError(null); };
      const save = async () => { if (!lobId || !branchId || !bankCode) { setError('Line of Business, Location, and Deposit Bank Codes are required.'); return; } if (!accountNo) { setError('Account No could not be resolved for the selected Deposit Bank Code.'); return; } if (rows.length === 0) { setError('At least one Drawee Bank is required.'); return; } setSaving(true); setError(null); try { const payload = { lineOfBusinessId: lobId, branchId, depositBankCode: bankCode, isActive: active, effectiveFrom: new Date().toISOString().slice(0, 10), effectiveTo: null, draweeBankRows: rows, idempotencyKey: crypto.randomUUID() }; const result = ruleSetId ? await challanRuleSetService.update(ruleSetId, { ...payload, expectedRuleVersion: version }) : await challanRuleSetService.create(payload); setRuleSetId(result.id); setVersion(result.runningRuleVersion); setAccountNo(result.accountNumber); setRows(result.draweeBankRows.filter(row => row.isActive).map(row => ({ draweeBankKey: row.draweeBankKey }))); } catch (error) { setError(error instanceof Error ? error.message : 'Unable to save Challan Rule Set.'); } finally { setSaving(false); } };

      return <Box sx={{ p: 3 }}><Typography variant="h4" gutterBottom>Challan Rule Creation</Typography>{error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}<Paper sx={{ p: 3 }}><Stack spacing={2}><Stack direction={{ xs: 'column', md: 'row' }} spacing={2}><FormControl fullWidth required><InputLabel>Line of Business</InputLabel><Select label="Line of Business" value={lobId} onChange={event => { setLobId(event.target.value); resetDependents(); }}>{lobs.map(option => <MenuItem key={option.id} value={option.id}>{option.displayName}</MenuItem>)}</Select></FormControl><FormControl fullWidth required disabled={!lobId}><InputLabel>Location</InputLabel><Select label="Location" value={branchId} onChange={event => { setBranchId(event.target.value); setBankCode(''); setBanks([]); setAccountNo(''); setRows([]); }}>{locations.map(option => <MenuItem key={option.branchId} value={option.branchId}>{option.displayName}</MenuItem>)}</Select></FormControl><FormControl fullWidth required disabled={!branchId}><InputLabel>Deposit Bank Codes</InputLabel><Select label="Deposit Bank Codes" value={bankCode} onChange={event => setBankCode(event.target.value)}>{banks.map(option => <MenuItem key={option.depositBankCode} value={option.depositBankCode}>{option.depositBankName}</MenuItem>)}</Select></FormControl><TextField fullWidth label="Account No" value={accountNo} InputProps={{ readOnly: true }} /><FormControlLabel control={<Checkbox checked={active} onChange={event => setActive(event.target.checked)} />} label="Active" /></Stack><Table aria-label="Drawee Bank mapping grid"><TableHead><TableRow><TableCell>Sl.No.</TableCell><TableCell>Drawee Bank</TableCell><TableCell>Actions</TableCell></TableRow></TableHead><TableBody>{rows.map((row, index) => <TableRow key={`${row.draweeBankKey}-${index}`}><TableCell>{index + 1}</TableCell><TableCell>{row.draweeBankKey}</TableCell><TableCell><Button onClick={() => { setEditingIndex(index); setDraftBank(row.draweeBankKey); }}>Edit</Button><Button color="error" onClick={() => setRows(current => current.filter((_, rowIndex) => rowIndex !== index))}>Delete</Button></TableCell></TableRow>)}<TableRow><TableCell>{rows.length + 1}</TableCell><TableCell><TextField size="small" label="Drawee Bank" value={draftBank} onChange={event => setDraftBank(event.target.value)} /></TableCell><TableCell><Button onClick={addOrEditRow}>{editingIndex === null ? 'Add' : 'Update'}</Button></TableCell></TableRow></TableBody></Table><Stack direction="row" justifyContent="flex-end" spacing={2}><Button variant="contained" onClick={() => void save()} disabled={saving}>Save</Button><Button onClick={clear} disabled={saving}>Clear</Button><Button onClick={() => navigate(-1)} disabled={saving}>Cancel</Button></Stack></Stack></Paper></Box>;
    }
    ```
    Preserve server-error state by changing only `error` on failure; do not reset fields or rows in the catch path. Use the existing page/component conventions if the repository splits the form or grid into focused components.
    _Requirements: 1.1–1.7, 2.1–2.5, 3.1–3.4, 4.1–4.4, 6.2–6.6, 7.1–7.4, 9.3, 10.4_

  - [ ] 10.2 MODIFY `E:\Finnova\Finnova-UI\Finnova-UI\src\App.tsx` — add the page import and authenticated route beside existing SystemAdmin master routes
    ```tsx
    <Route path="/system-admin/challan-rule-creation" element={<ChallanRuleCreation />} />
    <Route path="/system-admin/challan-rule-creation/:id" element={<ChallanRuleCreation />} />
    ```
    Use the existing route guard and import style.
    _Requirements: 1.1, 8.1, 10.4_

  - [ ] 10.3 MODIFY `E:\Finnova\Finnova-UI\Finnova-UI\src\components\Layout.tsx` — add the permission-filtered SystemAdmin navigation item
    ```tsx
    { label: 'Challan Rule Creation', path: '/system-admin/challan-rule-creation' }
    ```
    Do not add a second auth store or navigation system.
    _Requirements: 1.1, 8.1, 10.4_

- [ ]* 11. Add frontend Vitest/React Testing Library coverage
  - [ ]* 11.1 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\pages\system-admin\ChallanRuleCreation.test.tsx`
    ```tsx
    import { fireEvent, render, screen, waitFor } from '@testing-library/react';
    import { describe, expect, it, vi } from 'vitest';
    import ChallanRuleCreation from './ChallanRuleCreation';
    import { challanRuleSetService } from '../../services/challanRuleSet.service';

    describe('ChallanRuleCreation', () => {
      it('renders the approved screen structure and India examples', async () => {
        render(<ChallanRuleCreation />);
        expect(screen.getByText('Challan Rule Creation')).toBeInTheDocument();
        expect(screen.getByLabelText('Line of Business')).toBeInTheDocument();
        expect(screen.getByLabelText('Location')).toBeInTheDocument();
        expect(screen.getByLabelText('Deposit Bank Codes')).toBeInTheDocument();
        expect(screen.getByLabelText('Account No')).toHaveAttribute('readonly');
        expect(screen.getByText('Sl.No.')).toBeInTheDocument();
        expect(screen.getByText('Drawee Bank')).toBeInTheDocument();
        expect(screen.getByRole('button', { name: 'Save' })).toBeInTheDocument();
        expect(screen.getByRole('button', { name: 'Clear' })).toBeInTheDocument();
        expect(screen.getByRole('button', { name: 'Cancel' })).toBeInTheDocument();
        await waitFor(() => expect(screen.getByText('LN - Loan')).toBeInTheDocument());
      });

      it('loads dependent fields and resolves a read-only Account No', async () => {
        render(<ChallanRuleCreation />);
        await waitFor(() => screen.getByText('LN - Loan'));
        fireEvent.mouseDown(screen.getByLabelText('Line of Business'));
        fireEvent.click(screen.getByText('LN - Loan'));
        await waitFor(() => expect(screen.getByText('Mumbai')).toBeInTheDocument());
        fireEvent.mouseDown(screen.getByLabelText('Location'));
        fireEvent.click(screen.getByText('Mumbai'));
        await waitFor(() => expect(screen.getByText('HDFC Bank')).toBeInTheDocument());
        fireEvent.mouseDown(screen.getByLabelText('Deposit Bank Codes'));
        fireEvent.click(screen.getByText('HDFC Bank'));
        expect(await screen.findByDisplayValue('000123456789')).toBeInTheDocument();
      });

      it('adds, edits, deletes, clears, and preserves rows on Save error', async () => {
        const save = vi.spyOn(challanRuleSetService, 'create').mockRejectedValueOnce(new Error('Drawee bank is already mapped for this LOB and Location.'));
        render(<ChallanRuleCreation />);
        const drawee = screen.getByLabelText('Drawee Bank');
        fireEvent.change(drawee, { target: { value: 'HDFC' } });
        fireEvent.click(screen.getByRole('button', { name: 'Add' }));
        expect(screen.getByText('HDFC')).toBeInTheDocument();
        fireEvent.click(screen.getByRole('button', { name: 'Edit' }));
        fireEvent.change(drawee, { target: { value: 'SBI' } });
        fireEvent.click(screen.getByRole('button', { name: 'Update' }));
        fireEvent.click(screen.getByRole('button', { name: 'Delete' }));
        await waitFor(() => expect(screen.getByText('Drawee bank is already mapped for this LOB and Location.')).toBeInTheDocument());
        expect(save).toHaveBeenCalledTimes(0);
        fireEvent.click(screen.getByRole('button', { name: 'Clear' }));
        expect(screen.getByLabelText('Account No')).toHaveValue('');
        save.mockRestore();
      });
    });
    ```
    Add focused tests for Cancel making no mutation request, Active checkbox, server-error preservation, and exact `Edit`/`Delete`/`Add` actions. Use one-shot Vitest execution and no RTL/Redux/RxJS alternatives.
    _Requirements: 1.1–1.7, 2.4–2.5, 3.1–3.4, 4.1–4.4, 6.4–6.6, 7.1–7.4, 9.3, 10.4_

  - [ ]* 11.2 MODIFY `E:\Finnova\Finnova-UI\Finnova-UI\package.json` only if the script is absent
    ```json
    "scripts": {
      "test": "vitest run"
    }
    ```
    Preserve existing scripts and package versions. Do not add a second test framework.
    _Requirements: 10.4_

## Notes

- Tasks marked `*` are optional test tasks; implementation tasks are not optional.
- The UI title and labels must remain exactly `Challan Rule Creation`, `Line of Business`, `Location`, `Deposit Bank Codes`, `Account No`, `Active`, `Sl.No.`, `Drawee Bank`, `Save`, `Clear`, and `Cancel`.
- The screen has one selected Deposit Bank Code and no multi-group editor. Multiple Drawee Banks are rows under that one selected set.
- Use India-only English fixtures such as `LN - Loan`, `Mumbai`, `HDFC Bank`, `HDFC`, and `SBI`. Do not introduce non-India or non-English values.
- `Location` is the UI term; backend persistence uses the authoritative level-5 `BranchId`.
- FINNOVA-65 remains separate from FINNOVA-16 `ChallanRule`, which remains keyed only by `DraweeBankId` and owns format/validation/routing configuration.
- Receipt Processing resolves before challan preparation/authorization/JVE submission, persists the Routing_Snapshot, FINNOVA-63 includes it in the JVE request, JVE remains the only canonical journal owner, and no resolution occurs after posting.
- No source file, migration, build, or test is changed or run as part of spec delivery.

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3", "1.4", "1.5", "2.1", "2.2", "2.3", "4.1", "4.2"] },
    { "id": 1, "tasks": ["3.1", "3.2", "4.3", "4.4", "5.1", "5.2", "9.1", "9.2"] },
    { "id": 2, "tasks": ["3.3", "4.5", "5.3", "5.4", "9.3", "9.4"] },
    { "id": 3, "tasks": ["6.1", "6.2", "6.3", "7.1", "7.2", "10.1"] },
    { "id": 4, "tasks": ["7.3", "8.1", "8.2", "8.3", "10.2", "10.3"] },
    { "id": 5, "tasks": ["11.1", "11.2"] }
  ]
}
```
