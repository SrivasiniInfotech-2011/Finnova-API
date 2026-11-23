# Implementation Plan: Delinquent Provision Rate & Parameter Master

## Overview

This script implements FINNOVA-15 as a SystemAdmin master-data feature with backend C#/.NET and frontend React/TypeScript changes. Apply the compatibility gates before using any source task. The feature remains separate from Receipt Processing, receipt appropriation, canonical journals, and the JVE boundary.

## Tasks

Every step below is executable by copying the complete code block or the exact anchored modification into the named repository. Apply the steps in order. The implementation language is C# for the backend and TypeScript/React for the frontend. Do not create Company, Line of Business, Receipt Processing, appropriation, or journal models in this feature.

## 1. Backend domain

- [ ] 1.1 CREATE `Finnova.Models/Domain/Enums/ProvisionParameterEnums.cs`

```csharp
namespace Finnova.Models.Domain.Enums;

public enum DelinquentType
{
    Statutory = 0,
    Company = 1
}

public enum PeriodType
{
    Month = 0,
    Days = 1
}

public enum ProvisionAuditAction
{
    Create = 0,
    Update = 1,
    Activate = 2,
    Deactivate = 3
}
```

- [ ] 1.2 CREATE `Finnova.Models/Domain/Entities/ProvisionParameterEntities.cs`

```csharp
using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

public sealed class ProvisionParameterSet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Guid? LineOfBusinessId { get; set; }
    public DelinquentType DelinquentType { get; set; }
    public PeriodType PeriodType { get; set; }
    public bool IsActive { get; set; } = true;
    public int? CutOffMonth { get; set; }
    public DateOnly? EffectiveStartDate { get; set; }
    public DateOnly? EffectiveEndDate { get; set; }
    public int AgeingDays { get; set; }
    public int Version { get; set; } = 1;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<ProvisionBucket> Buckets { get; set; } = new();
}

public sealed class ProvisionBucket
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProvisionParameterSetId { get; set; }
    public int SortOrder { get; set; }
    public string Category { get; set; } = string.Empty;
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int AgeFrom { get; set; }
    public int? AgeTo { get; set; }
    public decimal BaseProvisionPercentage { get; set; }
    public decimal CashProvisionPercentage { get; set; }
    public ProvisionParameterSet? ProvisionParameterSet { get; set; }
}

public sealed class ProvisionParameterAuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProvisionParameterSetId { get; set; }
    public int Version { get; set; }
    public ProvisionAuditAction Action { get; set; }
    public string ActorId { get; set; } = string.Empty;
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
    public string CorrelationId { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string BeforeSnapshotJson { get; set; } = string.Empty;
    public string AfterSnapshotJson { get; set; } = string.Empty;
}

public sealed class ProvisionIdempotencyRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string RequestFingerprint { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string ResponseJson { get; set; } = string.Empty;
    public Guid ResourceId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAtUtc { get; set; }
}
```

- [ ] 1.3 CREATE `Finnova.Models/Domain/Exceptions/ProvisionParameterExceptions.cs`

```csharp
namespace Finnova.Models.Domain.Exceptions;

public abstract class ProvisionParameterException(string message, string errorCode)
    : Exception(message)
{
    public string ErrorCode { get; } = errorCode;
}

public sealed class ProvisionParameterNotFoundException(Guid id)
    : ProvisionParameterException($"Provision parameter set '{id}' was not found.", "ERR-PROV-404");

public sealed class ProvisionParameterValidationException(string message)
    : ProvisionParameterException(message, "ERR-PROV-VALIDATION");

public sealed class ProvisionRateRangeOverlapException()
    : ProvisionParameterException("Provision rate ranges must not overlap", "ERR-PROV-RANGE-OVERLAP");

public sealed class ProvisionEffectiveScopeOverlapException()
    : ProvisionParameterException("The active provision parameter effective scope overlaps an existing set.", "ERR-PROV-EFFECTIVE-OVERLAP");

public sealed class ProvisionConcurrencyException()
    : ProvisionParameterException("The provision parameter was changed by another administrator.", "ERR-PROV-CONCURRENCY");

public sealed class ProvisionIdempotencyConflictException()
    : ProvisionParameterException("The idempotency key was reused with different provision parameter data.", "ERR-PROV-IDEMPOTENCY-CONFLICT");

public sealed class ProvisionNoMatchException()
    : ProvisionParameterException("No active provision parameter matches the request.", "ERR-PROV-NO-MATCH");

public sealed class ProvisionAmbiguousException()
    : ProvisionParameterException("Multiple active provision parameters match the request.", "ERR-PROV-AMBIGUOUS");

public sealed class ProvisionAuditImmutableException()
    : ProvisionParameterException("Provision parameter audit entries are immutable.", "ERR-PROV-AUDIT-IMMUTABLE");
```

## 2. Backend contracts

- [ ] 2.1 CREATE `Finnova.Models/Contracts/ProvisionParameters/ProvisionParameterContracts.cs`

```csharp
using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Contracts.ProvisionParameters;

public sealed record ProvisionBucketRequest(
    string Category,
    DateOnly? StartDate,
    DateOnly? EndDate,
    int AgeFrom,
    int? AgeTo,
    decimal BaseProvisionPercentage,
    decimal CashProvisionPercentage);

public sealed record CreateProvisionParameterRequest(
    Guid CompanyId,
    Guid? LineOfBusinessId,
    DelinquentType DelinquentType,
    PeriodType PeriodType,
    bool? IsActive,
    int? CutOffMonth,
    DateOnly? EffectiveStartDate,
    DateOnly? EffectiveEndDate,
    int AgeingDays,
    IReadOnlyList<ProvisionBucketRequest> Buckets);

public sealed record UpdateProvisionParameterRequest(
    Guid CompanyId,
    Guid? LineOfBusinessId,
    DelinquentType DelinquentType,
    PeriodType PeriodType,
    bool IsActive,
    int? CutOffMonth,
    DateOnly? EffectiveStartDate,
    DateOnly? EffectiveEndDate,
    int AgeingDays,
    int Version,
    string RowVersion,
    IReadOnlyList<ProvisionBucketRequest> Buckets);

public sealed record ProvisionBucketResponse(
    Guid Id,
    int SortOrder,
    string Category,
    DateOnly? StartDate,
    DateOnly? EndDate,
    int AgeFrom,
    int? AgeTo,
    decimal BaseProvisionPercentage,
    decimal CashProvisionPercentage);

public sealed record ProvisionParameterResponse(
    Guid Id,
    Guid CompanyId,
    Guid? LineOfBusinessId,
    DelinquentType DelinquentType,
    PeriodType PeriodType,
    bool IsActive,
    int? CutOffMonth,
    DateOnly? EffectiveStartDate,
    DateOnly? EffectiveEndDate,
    int AgeingDays,
    int Version,
    string RowVersion,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<ProvisionBucketResponse> Buckets);

public sealed record ProvisionParameterSummaryResponse(
    Guid Id,
    Guid CompanyId,
    Guid? LineOfBusinessId,
    DelinquentType DelinquentType,
    PeriodType PeriodType,
    bool IsActive,
    DateOnly? EffectiveStartDate,
    DateOnly? EffectiveEndDate,
    int Version);

public sealed record ProvisionParameterAuditResponse(
    Guid Id,
    Guid ProvisionParameterSetId,
    int Version,
    string Action,
    string ActorId,
    DateTime ChangedAtUtc,
    string CorrelationId,
    string IdempotencyKey,
    string BeforeSnapshotJson,
    string AfterSnapshotJson);

public sealed record ProvisionParameterQuery(
    Guid? CompanyId,
    Guid? LineOfBusinessId,
    bool GlobalScope,
    DelinquentType? DelinquentType,
    PeriodType? PeriodType,
    bool? IsActive,
    DateOnly? EvaluationDate,
    int Page = 1,
    int PageSize = 20);

public sealed record ReferenceOption(Guid Id, string Name);
```

- [ ] 2.2 CREATE `Finnova.Models/Contracts/ProvisionParameters/ProvisionResolverContracts.cs`

```csharp
using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Contracts.ProvisionParameters;

public sealed record ResolvedProvisionBucket(
    int SortOrder,
    string Category,
    DateOnly? StartDate,
    DateOnly? EndDate,
    int AgeFrom,
    int? AgeTo,
    decimal BaseProvisionPercentage,
    decimal CashProvisionPercentage);

public sealed record ResolvedProvisionParameter(
    Guid SetId,
    int Version,
    Guid CompanyId,
    Guid? LineOfBusinessId,
    DelinquentType DelinquentType,
    PeriodType PeriodType,
    int? CutOffMonth,
    int AgeingDays,
    DateOnly? EffectiveStartDate,
    DateOnly? EffectiveEndDate,
    IReadOnlyList<ResolvedProvisionBucket> Buckets);
```

## 3. EF Core persistence

- [ ] 3.1 CREATE `Finnova.Repository/Configuration/ProvisionParameterConfiguration.cs`

```csharp
using Finnova.Models.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finnova.Repository.Configuration;

public sealed class ProvisionParameterConfiguration : IEntityTypeConfiguration<ProvisionParameterSet>
{
    public void Configure(EntityTypeBuilder<ProvisionParameterSet> builder)
    {
        builder.ToTable("provision_parameter_sets");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CompanyId).IsRequired();
        builder.Property(x => x.LineOfBusinessId);
        builder.Property(x => x.DelinquentType).IsRequired();
        builder.Property(x => x.PeriodType).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CutOffMonth);
        builder.Property(x => x.EffectiveStartDate).HasColumnType("date");
        builder.Property(x => x.EffectiveEndDate).HasColumnType("date");
        builder.Property(x => x.AgeingDays).IsRequired();
        builder.Property(x => x.Version).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();
        builder.HasIndex(x => new { x.CompanyId, x.DelinquentType, x.LineOfBusinessId, x.IsActive });
        builder.HasIndex(x => new { x.CompanyId, x.EffectiveStartDate, x.EffectiveEndDate });
        builder.HasMany(x => x.Buckets)
            .WithOne(x => x.ProvisionParameterSet)
            .HasForeignKey(x => x.ProvisionParameterSetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ProvisionBucketConfiguration : IEntityTypeConfiguration<ProvisionBucket>
{
    public void Configure(EntityTypeBuilder<ProvisionBucket> builder)
    {
        builder.ToTable("provision_parameter_buckets");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Category).IsRequired().HasMaxLength(100);
        builder.Property(x => x.StartDate).HasColumnType("date");
        builder.Property(x => x.EndDate).HasColumnType("date");
        builder.Property(x => x.AgeFrom).IsRequired();
        builder.Property(x => x.AgeTo);
        builder.Property(x => x.BaseProvisionPercentage).HasPrecision(5, 2).IsRequired();
        builder.Property(x => x.CashProvisionPercentage).HasPrecision(5, 2).IsRequired();
        builder.HasIndex(x => new { x.ProvisionParameterSetId, x.SortOrder }).IsUnique();
    }
}

public sealed class ProvisionParameterAuditConfiguration : IEntityTypeConfiguration<ProvisionParameterAuditEntry>
{
    public void Configure(EntityTypeBuilder<ProvisionParameterAuditEntry> builder)
    {
        builder.ToTable("provision_parameter_audit_entries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ActorId).IsRequired().HasMaxLength(200);
        builder.Property(x => x.CorrelationId).IsRequired().HasMaxLength(100);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(200);
        builder.Property(x => x.BeforeSnapshotJson).IsRequired();
        builder.Property(x => x.AfterSnapshotJson).IsRequired();
        builder.Property(x => x.ChangedAtUtc).IsRequired();
        builder.HasIndex(x => new { x.ProvisionParameterSetId, x.ChangedAtUtc });
    }
}

public sealed class ProvisionIdempotencyConfiguration : IEntityTypeConfiguration<ProvisionIdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<ProvisionIdempotencyRecord> builder)
    {
        builder.ToTable("provision_parameter_idempotency");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Key).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Operation).IsRequired().HasMaxLength(50);
        builder.Property(x => x.RequestFingerprint).IsRequired().HasMaxLength(128);
        builder.Property(x => x.ResponseJson).IsRequired();
        builder.HasIndex(x => new { x.Key, x.Operation }).IsUnique();
    }
}
```

- [ ] 3.2 MODIFY `Finnova.Repository/Context/FinnovaDbContext.cs` at the existing DbSet block after `public DbSet<NationalityAuditEntry> NationalityAuditEntries => Set<NationalityAuditEntry>();`

```csharp
    public DbSet<ProvisionParameterSet> ProvisionParameterSets => Set<ProvisionParameterSet>();
    public DbSet<ProvisionBucket> ProvisionBuckets => Set<ProvisionBucket>();
    public DbSet<ProvisionParameterAuditEntry> ProvisionParameterAuditEntries => Set<ProvisionParameterAuditEntry>();
    public DbSet<ProvisionIdempotencyRecord> ProvisionIdempotencyRecords => Set<ProvisionIdempotencyRecord>();
```

Add this using at the top if it is not already present:

```csharp
using Finnova.Models.Domain.Entities;
```

- [ ] 3.3 MODIFY `Finnova.Repository/Migrations` by running this command after the source tasks are applied; do not run it during spec creation.

```powershell
dotnet ef migrations add AddDelinquentProvisionParameterMaster --project Finnova.Repository --startup-project Finnova.SystemAdminService --context FinnovaDbContext
```

## 4. Repository and resolver ports

- [ ] 4.1 CREATE `Finnova.Repository/Interfaces/IProvisionParameterRepository.cs`

```csharp
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;

namespace Finnova.Repository.Interfaces;

public interface IProvisionParameterRepository : IRepository<ProvisionParameterSet>
{
    Task<(IReadOnlyList<ProvisionParameterSet> Items, int Total)> SearchAsync(
        Guid? companyId,
        Guid? lineOfBusinessId,
        bool globalScope,
        DelinquentType? delinquentType,
        PeriodType? periodType,
        bool? isActive,
        DateOnly? evaluationDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProvisionParameterSet>> FindActiveCandidatesAsync(
        Guid companyId,
        DelinquentType delinquentType,
        Guid? lineOfBusinessId,
        DateOnly evaluationDate,
        CancellationToken cancellationToken = default);
}

public interface IProvisionParameterAuditRepository : IRepository<ProvisionParameterAuditEntry>
{
    Task<IReadOnlyList<ProvisionParameterAuditEntry>> GetBySetIdAsync(Guid setId, CancellationToken cancellationToken = default);
}

public interface IProvisionIdempotencyRepository : IRepository<ProvisionIdempotencyRecord>
{
    Task<ProvisionIdempotencyRecord?> FindAsync(string key, string operation, CancellationToken cancellationToken = default);
}
```

- [ ] 4.2 CREATE `Finnova.Repository/Repositories/ProvisionParameterRepository.cs`

```csharp
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Finnova.Repository.Repositories;

public sealed class ProvisionParameterRepository(FinnovaDbContext db)
    : RepositoryBase<ProvisionParameterSet>(db), IProvisionParameterRepository
{
    public async Task<(IReadOnlyList<ProvisionParameterSet> Items, int Total)> SearchAsync(
        Guid? companyId, Guid? lineOfBusinessId, bool globalScope, DelinquentType? delinquentType,
        PeriodType? periodType, bool? isActive, DateOnly? evaluationDate, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        IQueryable<ProvisionParameterSet> query = DbSet.AsNoTracking().Include(x => x.Buckets);
        if (companyId.HasValue) query = query.Where(x => x.CompanyId == companyId.Value);
        if (globalScope) query = query.Where(x => x.LineOfBusinessId == null);
        else if (lineOfBusinessId.HasValue) query = query.Where(x => x.LineOfBusinessId == lineOfBusinessId.Value);
        if (delinquentType.HasValue) query = query.Where(x => x.DelinquentType == delinquentType.Value);
        if (periodType.HasValue) query = query.Where(x => x.PeriodType == periodType.Value);
        if (isActive.HasValue) query = query.Where(x => x.IsActive == isActive.Value);
        if (evaluationDate.HasValue)
        {
            var date = evaluationDate.Value;
            query = query.Where(x => (!x.EffectiveStartDate.HasValue || x.EffectiveStartDate <= date) &&
                (!x.EffectiveEndDate.HasValue || x.EffectiveEndDate >= date));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.CompanyId)
            .ThenBy(x => x.DelinquentType)
            .ThenBy(x => x.LineOfBusinessId)
            .ThenByDescending(x => x.EffectiveStartDate)
            .ThenByDescending(x => x.Version)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<IReadOnlyList<ProvisionParameterSet>> FindActiveCandidatesAsync(
        Guid companyId, DelinquentType delinquentType, Guid? lineOfBusinessId,
        DateOnly evaluationDate, CancellationToken cancellationToken = default)
    {
        return await DbSet.AsNoTracking().Include(x => x.Buckets)
            .Where(x => x.CompanyId == companyId && x.DelinquentType == delinquentType && x.IsActive)
            .Where(x => x.LineOfBusinessId == lineOfBusinessId || x.LineOfBusinessId == null)
            .Where(x => (!x.EffectiveStartDate.HasValue || x.EffectiveStartDate <= evaluationDate) &&
                        (!x.EffectiveEndDate.HasValue || x.EffectiveEndDate >= evaluationDate))
            .OrderByDescending(x => x.Version)
            .ToListAsync(cancellationToken);
    }
}

public sealed class ProvisionParameterAuditRepository(FinnovaDbContext db)
    : RepositoryBase<ProvisionParameterAuditEntry>(db), IProvisionParameterAuditRepository
{
    public async Task<IReadOnlyList<ProvisionParameterAuditEntry>> GetBySetIdAsync(
        Guid setId, CancellationToken cancellationToken = default)
        => await DbSet.AsNoTracking().Where(x => x.ProvisionParameterSetId == setId)
            .OrderByDescending(x => x.ChangedAtUtc).ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);
}

public sealed class ProvisionIdempotencyRepository(FinnovaDbContext db)
    : RepositoryBase<ProvisionIdempotencyRecord>(db), IProvisionIdempotencyRepository
{
    public Task<ProvisionIdempotencyRecord?> FindAsync(
        string key, string operation, CancellationToken cancellationToken = default)
        => DbSet.AsNoTracking().SingleOrDefaultAsync(x => x.Key == key && x.Operation == operation, cancellationToken);
}
```

- [ ] 4.3 CREATE `Finnova.Service/ProvisionParameters/IProvisionParameterResolver.cs`

```csharp
using Finnova.Models.Contracts.ProvisionParameters;
using Finnova.Models.Domain.Enums;

namespace Finnova.Service.ProvisionParameters;

public interface IProvisionParameterResolver
{
    Task<ResolvedProvisionParameter> ResolveAsync(
        Guid companyId,
        DelinquentType delinquentType,
        Guid? lineOfBusinessId,
        DateOnly evaluationDate,
        CancellationToken cancellationToken = default);
}
```

- [ ] 4.4 CREATE `Finnova.Service/ProvisionParameters/ProvisionParameterResolver.cs`

```csharp
using Finnova.Models.Contracts.ProvisionParameters;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Exceptions;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Interfaces;

namespace Finnova.Service.ProvisionParameters;

public sealed class ProvisionParameterResolver(IProvisionParameterRepository repository)
    : IProvisionParameterResolver
{
    public async Task<ResolvedProvisionParameter> ResolveAsync(
        Guid companyId, DelinquentType delinquentType, Guid? lineOfBusinessId,
        DateOnly evaluationDate, CancellationToken cancellationToken = default)
    {
        var candidates = await repository.FindActiveCandidatesAsync(
            companyId, delinquentType, lineOfBusinessId, evaluationDate, cancellationToken);
        var exact = candidates.Where(x => lineOfBusinessId.HasValue && x.LineOfBusinessId == lineOfBusinessId).ToList();
        var global = candidates.Where(x => x.LineOfBusinessId == null).ToList();
        var selected = exact.Count > 0 ? exact : global;
        if (selected.Count == 0) throw new ProvisionNoMatchException();
        if (selected.Count != 1) throw new ProvisionAmbiguousException();
        return selected[0].ToResolved();
    }
}

internal static class ProvisionResolverMapper
{
    public static ResolvedProvisionParameter ToResolved(this ProvisionParameterSet set)
        => new(set.Id, set.Version, set.CompanyId, set.LineOfBusinessId, set.DelinquentType,
            set.PeriodType, set.CutOffMonth, set.AgeingDays, set.EffectiveStartDate, set.EffectiveEndDate,
            set.Buckets.OrderBy(x => x.SortOrder).Select(x => new ResolvedProvisionBucket(
                x.SortOrder, x.Category, x.StartDate, x.EndDate, x.AgeFrom, x.AgeTo,
                x.BaseProvisionPercentage, x.CashProvisionPercentage)).ToList());
}
```

- [ ] 4.5 MODIFY `Finnova.Repository/DependencyInjection.cs` at the existing repository registration block.

```csharp
        services.AddScoped<IProvisionParameterRepository, ProvisionParameterRepository>();
        services.AddScoped<IProvisionParameterAuditRepository, ProvisionParameterAuditRepository>();
        services.AddScoped<IProvisionIdempotencyRepository, ProvisionIdempotencyRepository>();
        services.AddScoped<Finnova.Service.ProvisionParameters.IProvisionParameterResolver,
            Finnova.Service.ProvisionParameters.ProvisionParameterResolver>();
```

## 5. Pure validation and normalization rules

- [ ] 5.1 CREATE `Finnova.Service/ProvisionParameters/ProvisionParameterRules.cs`

```csharp
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Finnova.Models.Contracts.ProvisionParameters;
using Finnova.Models.Domain.Exceptions;

namespace Finnova.Service.ProvisionParameters;

public sealed record ProvisionInterval(int From, int? To);
public sealed record ProvisionDateInterval(DateOnly? Start, DateOnly? End);

public static class ProvisionParameterRules
{
    public const int MaxAgeingDays = 36500;
    public static readonly string[] Categories = ["Standard", "Special Mention", "Sub-Standard", "Doubtful", "Loss"];

    public static void ValidateBuckets(IReadOnlyList<ProvisionBucketRequest> buckets,
        DateOnly? setStart, DateOnly? setEnd)
    {
        if (buckets.Count == 0) throw new ProvisionParameterValidationException("At least one provision bucket is required.");
        for (var index = 0; index < buckets.Count; index++)
        {
            var bucket = buckets[index];
            if (string.IsNullOrWhiteSpace(bucket.Category) || !Categories.Contains(bucket.Category.Trim(), StringComparer.Ordinal))
                throw new ProvisionParameterValidationException($"Buckets[{index}].Category is not a supported English category.");
            if (bucket.AgeFrom < 0 || (bucket.AgeTo.HasValue && bucket.AgeTo.Value < bucket.AgeFrom))
                throw new ProvisionParameterValidationException($"Buckets[{index}] has an invalid age range.");
            if (!bucket.AgeTo.HasValue && index != buckets.Count - 1)
                throw new ProvisionParameterValidationException("Only the final provision bucket may be open-ended.");
            ValidateDateRange(bucket.StartDate, bucket.EndDate, $"Buckets[{index}]");
            if (setStart.HasValue && bucket.StartDate.HasValue && bucket.StartDate < setStart)
                throw new ProvisionParameterValidationException($"Buckets[{index}].StartDate is outside the set effective range.");
            if (setEnd.HasValue && bucket.EndDate.HasValue && bucket.EndDate > setEnd)
                throw new ProvisionParameterValidationException($"Buckets[{index}].EndDate is outside the set effective range.");
            ValidateRate(bucket.BaseProvisionPercentage, $"Buckets[{index}].BaseProvisionPercentage");
            ValidateRate(bucket.CashProvisionPercentage, $"Buckets[{index}].CashProvisionPercentage");
            if (bucket.CashProvisionPercentage > bucket.BaseProvisionPercentage)
                throw new ProvisionParameterValidationException("Cash provision percentage must not exceed base provision percentage.");
        }

        for (var left = 0; left < buckets.Count; left++)
        for (var right = left + 1; right < buckets.Count; right++)
        {
            if (AgeOverlaps(new(buckets[left].AgeFrom, buckets[left].AgeTo), new(buckets[right].AgeFrom, buckets[right].AgeTo)))
                throw new ProvisionRateRangeOverlapException();
            var leftDate = new ProvisionDateInterval(buckets[left].StartDate ?? setStart, buckets[left].EndDate ?? setEnd);
            var rightDate = new ProvisionDateInterval(buckets[right].StartDate ?? setStart, buckets[right].EndDate ?? setEnd);
            if (DateOverlaps(leftDate, rightDate) && (buckets[left].StartDate.HasValue || buckets[left].EndDate.HasValue) &&
                (buckets[right].StartDate.HasValue || buckets[right].EndDate.HasValue))
                throw new ProvisionParameterValidationException("Provision bucket date ranges must not overlap.");
        }
    }

    public static bool AgeOverlaps(ProvisionInterval left, ProvisionInterval right)
        => left.To is null ? right.To is null || right.To.Value >= left.From :
           right.To is null ? left.To.Value >= right.From :
           left.From <= right.To.Value && right.From <= left.To.Value;

    public static bool DateOverlaps(ProvisionDateInterval left, ProvisionDateInterval right)
        => (!left.End.HasValue || !right.Start.HasValue || left.End.Value >= right.Start.Value) &&
           (!right.End.HasValue || !left.Start.HasValue || right.End.Value >= left.Start.Value);

    public static void ValidateDateRange(DateOnly? start, DateOnly? end, string field)
    {
        if (end.HasValue && !start.HasValue) throw new ProvisionParameterValidationException($"{field}.EndDate requires StartDate.");
        if (start.HasValue && end.HasValue && end < start)
            throw new ProvisionParameterValidationException($"{field} has an invalid date range.");
    }

    public static void ValidateRate(decimal value, string field)
    {
        if (value < 0m || value > 100m || decimal.Round(value, 2) != value)
            throw new ProvisionParameterValidationException($"{field} must be between 0 and 100 with at most two decimal places.");
    }

    public static string Fingerprint(string operation, object normalizedRequest)
    {
        var json = JsonSerializer.Serialize(new { operation, normalizedRequest }, new JsonSerializerOptions { WriteIndented = false });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }
}
```

## 6. CQRS application slice

- [ ] 6.1 CREATE `Finnova.Service/ProvisionParameters/ProvisionParameterCommands.cs`

```csharp
using Finnova.Models.Contracts.ProvisionParameters;
using MediatR;

namespace Finnova.Service.ProvisionParameters;

public sealed record CreateProvisionParameterCommand(
    CreateProvisionParameterRequest Request,
    string ActorId,
    string CorrelationId,
    string IdempotencyKey) : IRequest<ProvisionParameterResponse>;

public sealed record UpdateProvisionParameterCommand(
    Guid Id,
    UpdateProvisionParameterRequest Request,
    string ActorId,
    string CorrelationId,
    string IdempotencyKey) : IRequest<ProvisionParameterResponse>;

public sealed record SearchProvisionParametersQuery(ProvisionParameterQuery Query)
    : IRequest<PaginatedResponse<ProvisionParameterSummaryResponse>>;

public sealed record GetProvisionParameterQuery(Guid Id) : IRequest<ProvisionParameterResponse>;
public sealed record GetProvisionParameterAuditQuery(Guid Id) : IRequest<IReadOnlyList<ProvisionParameterAuditResponse>>;
```

- [ ] 6.2 CREATE `Finnova.Service/ProvisionParameters/ProvisionParameterValidators.cs`

```csharp
using FluentValidation;
using Finnova.Models.Contracts.ProvisionParameters;

namespace Finnova.Service.ProvisionParameters;

public sealed class CreateProvisionParameterValidator : AbstractValidator<CreateProvisionParameterCommand>
{
    public CreateProvisionParameterValidator()
    {
        RuleFor(x => x.Request.CompanyId).NotEmpty();
        RuleFor(x => x.Request.AgeingDays).InclusiveBetween(0, ProvisionParameterRules.MaxAgeingDays);
        RuleFor(x => x.Request.EffectiveEndDate).GreaterThanOrEqualTo(x => x.Request.EffectiveStartDate)
            .When(x => x.Request.EffectiveStartDate.HasValue && x.Request.EffectiveEndDate.HasValue);
        RuleFor(x => x.Request.CutOffMonth).InclusiveBetween(1, 31).When(x => x.Request.CutOffMonth.HasValue);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Request.Buckets).NotEmpty();
    }
}

public sealed class UpdateProvisionParameterValidator : AbstractValidator<UpdateProvisionParameterCommand>
{
    public UpdateProvisionParameterValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Request.CompanyId).NotEmpty();
        RuleFor(x => x.Request.Version).GreaterThan(0);
        RuleFor(x => x.Request.RowVersion).NotEmpty();
        RuleFor(x => x.Request.AgeingDays).InclusiveBetween(0, ProvisionParameterRules.MaxAgeingDays);
        RuleFor(x => x.Request.EffectiveEndDate).GreaterThanOrEqualTo(x => x.Request.EffectiveStartDate)
            .When(x => x.Request.EffectiveStartDate.HasValue && x.Request.EffectiveEndDate.HasValue);
        RuleFor(x => x.Request.CutOffMonth).InclusiveBetween(1, 31).When(x => x.Request.CutOffMonth.HasValue);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Request.Buckets).NotEmpty();
    }
}

public sealed class SearchProvisionParametersValidator : AbstractValidator<SearchProvisionParametersQuery>
{
    public SearchProvisionParametersValidator()
    {
        RuleFor(x => x.Query.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.Query.PageSize).InclusiveBetween(1, 100);
    }
}
```

- [ ] 6.3 CREATE `Finnova.Service/ProvisionParameters/ProvisionParameterMapper.cs`

```csharp
using Finnova.Models.Contracts.ProvisionParameters;
using Finnova.Models.Domain.Entities;

namespace Finnova.Service.ProvisionParameters;

public static class ProvisionParameterMapper
{
    public static ProvisionParameterResponse ToResponse(this ProvisionParameterSet set)
        => new(set.Id, set.CompanyId, set.LineOfBusinessId, set.DelinquentType, set.PeriodType,
            set.IsActive, set.CutOffMonth, set.EffectiveStartDate, set.EffectiveEndDate, set.AgeingDays,
            set.Version, Convert.ToBase64String(set.RowVersion), set.CreatedAtUtc, set.UpdatedAtUtc,
            set.Buckets.OrderBy(x => x.SortOrder).Select(x => new ProvisionBucketResponse(
                x.Id, x.SortOrder, x.Category, x.StartDate, x.EndDate, x.AgeFrom, x.AgeTo,
                x.BaseProvisionPercentage, x.CashProvisionPercentage)).ToList());

    public static ProvisionParameterSummaryResponse ToSummary(this ProvisionParameterSet set)
        => new(set.Id, set.CompanyId, set.LineOfBusinessId, set.DelinquentType, set.PeriodType,
            set.IsActive, set.EffectiveStartDate, set.EffectiveEndDate, set.Version);

    public static ProvisionParameterAuditResponse ToResponse(this ProvisionParameterAuditEntry entry)
        => new(entry.Id, entry.ProvisionParameterSetId, entry.Version, entry.Action.ToString(), entry.ActorId,
            entry.ChangedAtUtc, entry.CorrelationId, entry.IdempotencyKey, entry.BeforeSnapshotJson, entry.AfterSnapshotJson);
}
```

- [ ] 6.4 CREATE `Finnova.Service/ProvisionParameters/ProvisionParameterHandlers.cs`

```csharp
using System.Text.Json;
using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.ProvisionParameters;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Finnova.Service.ProvisionParameters;

public sealed class CreateProvisionParameterHandler(
    FinnovaDbContext db,
    IProvisionParameterRepository repository,
    IProvisionParameterAuditRepository auditRepository,
    IProvisionIdempotencyRepository idempotencyRepository)
    : IRequestHandler<CreateProvisionParameterCommand, ProvisionParameterResponse>
{
    public async Task<ProvisionParameterResponse> Handle(CreateProvisionParameterCommand command, CancellationToken ct)
    {
        var request = command.Request;
        ProvisionParameterRules.ValidateDateRange(request.EffectiveStartDate, request.EffectiveEndDate, "Effective date");
        ProvisionParameterRules.ValidateBuckets(request.Buckets, request.EffectiveStartDate, request.EffectiveEndDate);
        var fingerprint = ProvisionParameterRules.Fingerprint("create", request);
        var existing = await idempotencyRepository.FindAsync(command.IdempotencyKey, "create", ct);
        if (existing is not null)
        {
            if (existing.RequestFingerprint != fingerprint) throw new ProvisionIdempotencyConflictException();
            return JsonSerializer.Deserialize<ProvisionParameterResponse>(existing.ResponseJson)!;
        }

        var entity = new ProvisionParameterSet
        {
            CompanyId = request.CompanyId,
            LineOfBusinessId = request.LineOfBusinessId,
            DelinquentType = request.DelinquentType,
            PeriodType = request.PeriodType,
            IsActive = request.IsActive ?? true,
            CutOffMonth = request.CutOffMonth,
            EffectiveStartDate = request.EffectiveStartDate,
            EffectiveEndDate = request.EffectiveEndDate,
            AgeingDays = request.AgeingDays,
            Version = 1,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            Buckets = request.Buckets.Select((x, i) => new ProvisionBucket
            {
                SortOrder = i + 1,
                Category = x.Category.Trim(),
                StartDate = x.StartDate,
                EndDate = x.EndDate,
                AgeFrom = x.AgeFrom,
                AgeTo = x.AgeTo,
                BaseProvisionPercentage = x.BaseProvisionPercentage,
                CashProvisionPercentage = x.CashProvisionPercentage
            }).ToList()
        };
        await db.Database.BeginTransactionAsync(ct);
        try
        {
            await repository.AddAsync(entity, ct);
            var response = entity.ToResponse();
            await auditRepository.AddAsync(new ProvisionParameterAuditEntry
            {
                ProvisionParameterSetId = entity.Id,
                Version = entity.Version,
                Action = ProvisionAuditAction.Create,
                ActorId = command.ActorId,
                CorrelationId = command.CorrelationId,
                IdempotencyKey = command.IdempotencyKey,
                AfterSnapshotJson = JsonSerializer.Serialize(response),
                BeforeSnapshotJson = string.Empty,
                ChangedAtUtc = DateTime.UtcNow
            }, ct);
            await idempotencyRepository.AddAsync(new ProvisionIdempotencyRecord
            {
                Key = command.IdempotencyKey,
                Operation = "create",
                RequestFingerprint = fingerprint,
                StatusCode = 201,
                ResponseJson = JsonSerializer.Serialize(response),
                ResourceId = entity.Id
            }, ct);
            await db.Database.CurrentTransaction!.CommitAsync(ct);
            return response;
        }
        catch
        {
            await db.Database.CurrentTransaction!.RollbackAsync(ct);
            throw;
        }
    }
}

public sealed class UpdateProvisionParameterHandler(
    FinnovaDbContext db,
    IProvisionParameterRepository repository,
    IProvisionParameterAuditRepository auditRepository,
    IProvisionIdempotencyRepository idempotencyRepository)
    : IRequestHandler<UpdateProvisionParameterCommand, ProvisionParameterResponse>
{
    public async Task<ProvisionParameterResponse> Handle(UpdateProvisionParameterCommand command, CancellationToken ct)
    {
        var request = command.Request;
        ProvisionParameterRules.ValidateDateRange(request.EffectiveStartDate, request.EffectiveEndDate, "Effective date");
        ProvisionParameterRules.ValidateBuckets(request.Buckets, request.EffectiveStartDate, request.EffectiveEndDate);
        var fingerprint = ProvisionParameterRules.Fingerprint("update", request);
        var replay = await idempotencyRepository.FindAsync(command.IdempotencyKey, "update", ct);
        if (replay is not null)
        {
            if (replay.RequestFingerprint != fingerprint) throw new ProvisionIdempotencyConflictException();
            return JsonSerializer.Deserialize<ProvisionParameterResponse>(replay.ResponseJson)!;
        }

        var entity = await repository.GetByIdAsync(command.Id, ct) ?? throw new ProvisionParameterNotFoundException(command.Id);
        if (entity.Version != request.Version || Convert.ToBase64String(entity.RowVersion) != request.RowVersion)
            throw new ProvisionConcurrencyException();
        var before = entity.ToResponse();
        entity.CompanyId = request.CompanyId;
        entity.LineOfBusinessId = request.LineOfBusinessId;
        entity.DelinquentType = request.DelinquentType;
        entity.PeriodType = request.PeriodType;
        entity.IsActive = request.IsActive;
        entity.CutOffMonth = request.CutOffMonth;
        entity.EffectiveStartDate = request.EffectiveStartDate;
        entity.EffectiveEndDate = request.EffectiveEndDate;
        entity.AgeingDays = request.AgeingDays;
        entity.Version++;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        entity.Buckets = request.Buckets.Select((x, i) => new ProvisionBucket
        {
            ProvisionParameterSetId = entity.Id,
            SortOrder = i + 1,
            Category = x.Category.Trim(),
            StartDate = x.StartDate,
            EndDate = x.EndDate,
            AgeFrom = x.AgeFrom,
            AgeTo = x.AgeTo,
            BaseProvisionPercentage = x.BaseProvisionPercentage,
            CashProvisionPercentage = x.CashProvisionPercentage
        }).ToList();
        await db.Database.BeginTransactionAsync(ct);
        try
        {
            await repository.UpdateAsync(entity, ct);
            var response = entity.ToResponse();
            var action = before.IsActive == response.IsActive
                ? ProvisionAuditAction.Update
                : response.IsActive ? ProvisionAuditAction.Activate : ProvisionAuditAction.Deactivate;
            await auditRepository.AddAsync(new ProvisionParameterAuditEntry
            {
                ProvisionParameterSetId = entity.Id,
                Version = entity.Version,
                Action = action,
                ActorId = command.ActorId,
                CorrelationId = command.CorrelationId,
                IdempotencyKey = command.IdempotencyKey,
                BeforeSnapshotJson = JsonSerializer.Serialize(before),
                AfterSnapshotJson = JsonSerializer.Serialize(response),
                ChangedAtUtc = DateTime.UtcNow
            }, ct);
            await idempotencyRepository.AddAsync(new ProvisionIdempotencyRecord
            {
                Key = command.IdempotencyKey,
                Operation = "update",
                RequestFingerprint = fingerprint,
                StatusCode = 200,
                ResponseJson = JsonSerializer.Serialize(response),
                ResourceId = entity.Id
            }, ct);
            await db.Database.CurrentTransaction!.CommitAsync(ct);
            return response;
        }
        catch (DbUpdateConcurrencyException)
        {
            await db.Database.CurrentTransaction!.RollbackAsync(ct);
            throw new ProvisionConcurrencyException();
        }
        catch
        {
            await db.Database.CurrentTransaction!.RollbackAsync(ct);
            throw;
        }
    }
}

public sealed class SearchProvisionParametersHandler(IProvisionParameterRepository repository)
    : IRequestHandler<SearchProvisionParametersQuery, PaginatedResponse<ProvisionParameterSummaryResponse>>
{
    public async Task<PaginatedResponse<ProvisionParameterSummaryResponse>> Handle(SearchProvisionParametersQuery request, CancellationToken ct)
    {
        var q = request.Query;
        var result = await repository.SearchAsync(q.CompanyId, q.LineOfBusinessId, q.GlobalScope,
            q.DelinquentType, q.PeriodType, q.IsActive, q.EvaluationDate, q.Page, q.PageSize, ct);
        var data = result.Items.Select(x => x.ToSummary()).ToList();
        return new PaginatedResponse<ProvisionParameterSummaryResponse>(data, result.Total, q.Page, q.PageSize,
            (int)Math.Ceiling(result.Total / (double)q.PageSize));
    }
}

public sealed class GetProvisionParameterHandler(IProvisionParameterRepository repository)
    : IRequestHandler<GetProvisionParameterQuery, ProvisionParameterResponse>
{
    public async Task<ProvisionParameterResponse> Handle(GetProvisionParameterQuery request, CancellationToken ct)
        => (await repository.GetByIdAsync(request.Id, ct))?.ToResponse()
           ?? throw new ProvisionParameterNotFoundException(request.Id);
}

public sealed class GetProvisionParameterAuditHandler(IProvisionParameterAuditRepository repository)
    : IRequestHandler<GetProvisionParameterAuditQuery, IReadOnlyList<ProvisionParameterAuditResponse>>
{
    public async Task<IReadOnlyList<ProvisionParameterAuditResponse>> Handle(GetProvisionParameterAuditQuery request, CancellationToken ct)
        => (await repository.GetBySetIdAsync(request.Id, ct)).Select(x => x.ToResponse()).ToList();
}
```

## 7. API, authorization, middleware, and gateway

- [ ] 7.1 CREATE `Finnova.SystemAdminService/Controllers/ProvisionParameterController.cs`

```csharp
using System.Security.Claims;
using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.ProvisionParameters;
using Finnova.Service.ProvisionParameters;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finnova.SystemAdminService.Controllers;

[ApiController]
[Route("api/provision-parameters")]
[Authorize(Policy = "SystemAdmin")]
public sealed class ProvisionParameterController(IMediator mediator) : ControllerBase
{
    private string ActorId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub") ?? throw new InvalidOperationException("Authenticated actor identity is missing.");

    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<ProvisionParameterSummaryResponse>>> Search(
        [FromQuery] Guid? companyId, [FromQuery] Guid? lineOfBusinessId, [FromQuery] bool globalScope,
        [FromQuery] Finnova.Models.Domain.Enums.DelinquentType? delinquentType,
        [FromQuery] Finnova.Models.Domain.Enums.PeriodType? periodType, [FromQuery] bool? isActive,
        [FromQuery] DateOnly? evaluationDate, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(await mediator.Send(new SearchProvisionParametersQuery(new ProvisionParameterQuery(
            companyId, lineOfBusinessId, globalScope, delinquentType, periodType, isActive,
            evaluationDate, page, pageSize)), cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProvisionParameterResponse>> Get(Guid id, CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetProvisionParameterQuery(id), cancellationToken));

    [HttpGet("{id:guid}/audit")]
    public async Task<ActionResult<IReadOnlyList<ProvisionParameterAuditResponse>>> Audit(Guid id, CancellationToken cancellationToken)
        => Ok(await mediator.Send(new GetProvisionParameterAuditQuery(id), cancellationToken));

    [HttpPost]
    public async Task<ActionResult<ProvisionParameterResponse>> Create(
        [FromBody] CreateProvisionParameterRequest request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new CreateProvisionParameterCommand(request, ActorId,
            HttpContext.TraceIdentifier, idempotencyKey), cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProvisionParameterResponse>> Update(
        Guid id, [FromBody] UpdateProvisionParameterRequest request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken)
        => Ok(await mediator.Send(new UpdateProvisionParameterCommand(id, request, ActorId,
            HttpContext.TraceIdentifier, idempotencyKey), cancellationToken));
}
```

- [ ] 7.2 MODIFY `Finnova.SystemAdminService/Middleware/ExceptionHandlingMiddleware.cs` by inserting these cases before the shared `FluentValidation.ValidationException` case.

```csharp
                ProvisionParameterNotFoundException ex => (StatusCodes.Status404NotFound, ex.ErrorCode, ex.Message),
                ProvisionRateRangeOverlapException ex => (StatusCodes.Status409Conflict, ex.ErrorCode, ex.Message),
                ProvisionEffectiveScopeOverlapException ex => (StatusCodes.Status409Conflict, ex.ErrorCode, ex.Message),
                ProvisionConcurrencyException ex => (StatusCodes.Status409Conflict, ex.ErrorCode, ex.Message),
                ProvisionIdempotencyConflictException ex => (StatusCodes.Status409Conflict, ex.ErrorCode, ex.Message),
                ProvisionNoMatchException ex => (StatusCodes.Status404NotFound, ex.ErrorCode, ex.Message),
                ProvisionAmbiguousException ex => (StatusCodes.Status409Conflict, ex.ErrorCode, ex.Message),
                ProvisionAuditImmutableException ex => (StatusCodes.Status409Conflict, ex.ErrorCode, ex.Message),
                ProvisionParameterValidationException ex => (StatusCodes.Status400BadRequest, ex.ErrorCode, ex.Message),
```

Add this using with the existing exception using directives:

```csharp
using Finnova.Models.Domain.Exceptions;
```

- [ ] 7.3 MODIFY `Finnova.SystemAdminService/Program.cs` at the existing authorization policy block after `options.AddPolicy("SystemAdmin", p => p.RequireRole("SystemAdmin"));`; use the confirmed policy name from the compatibility gate.

```csharp
    options.AddPolicy("ProvisionParameterAdmin", policy => policy.RequireRole("SystemAdmin", "RISK_POLICY_ADMINISTRATOR"));
```

Change the controller attribute in task 7.1 to `[Authorize(Policy = "ProvisionParameterAdmin")]` only if the compatibility gate confirms the separate policy. Otherwise retain `SystemAdmin` and remove this extra policy.

- [ ] 7.4 MODIFY `Finnova.ApiGateway/appsettings.json` by inserting these route entries before `ua-route` in `ReverseProxy.Routes`.

```json
      "ua-provision-parameters-alias-route": {
        "ClusterId": "systemadmin-cluster",
        "Match": { "Path": "/api/ua/api/provision-parameters/{**catch-all}" },
        "Transforms": [
          { "PathRemovePrefix": "/api/ua/api" },
          { "PathPrefix": "/api" }
        ]
      },
      "ua-provision-parameters-alias-root": {
        "ClusterId": "systemadmin-cluster",
        "Match": { "Path": "/api/ua/api/provision-parameters" },
        "Transforms": [
          { "PathRemovePrefix": "/api/ua/api" },
          { "PathPrefix": "/api" }
        ]
      },
```

## 8. Backend automated tests

- [ ] 8.1 CREATE `Finnova.Tests/Properties/ProvisionParameterRuleProperties.cs`

```csharp
using FsCheck;
using FsCheck.Xunit;
using Finnova.Service.ProvisionParameters;

namespace Finnova.Tests.Properties;

public sealed class ProvisionParameterRuleProperties
{
    [Property(MaxTest = 100)]
    public Property AgeOverlapIsSymmetric(NonNegativeInt leftStart, NonNegativeInt rightStart)
    {
        var left = new ProvisionInterval(Math.Min(leftStart.Get, 1000), Math.Min(leftStart.Get + 30, 2000));
        var right = new ProvisionInterval(Math.Min(rightStart.Get, 1000), Math.Min(rightStart.Get + 30, 2000));
        return (ProvisionParameterRules.AgeOverlaps(left, right) == ProvisionParameterRules.AgeOverlaps(right, left)).ToProperty();
    }

    [Property(MaxTest = 100)]
    public Property DateOverlapIsSymmetric(NonNegativeInt offset)
    {
        var start = new DateOnly(2024, 1, 1).AddDays(offset.Get % 1000);
        var left = new ProvisionDateInterval(start, start.AddDays(10));
        var right = new ProvisionDateInterval(start.AddDays(5), start.AddDays(15));
        return (ProvisionParameterRules.DateOverlaps(left, right) == ProvisionParameterRules.DateOverlaps(right, left)).ToProperty();
    }
}
```

- [ ] 8.2 CREATE `Finnova.Tests/Unit/ProvisionParameterRulesTests.cs`

```csharp
using Finnova.Models.Contracts.ProvisionParameters;
using Finnova.Models.Domain.Exceptions;
using Finnova.Service.ProvisionParameters;

namespace Finnova.Tests.Unit;

public sealed class ProvisionParameterRulesTests
{
    [Fact]
    public void TicketOverlapReturnsExactEnglishMessage()
    {
        var buckets = new[]
        {
            new ProvisionBucketRequest("Standard", null, null, 0, 30, 10m, 5m),
            new ProvisionBucketRequest("Special Mention", null, null, 15, 45, 20m, 10m)
        };
        var exception = Assert.Throws<ProvisionRateRangeOverlapException>(() =>
            ProvisionParameterRules.ValidateBuckets(buckets, null, null));
        Assert.Equal("Provision rate ranges must not overlap", exception.Message);
    }

    [Fact]
    public void AdjacentInclusiveRangesAreValid()
    {
        var buckets = new[]
        {
            new ProvisionBucketRequest("Standard", null, null, 0, 30, 10m, 5m),
            new ProvisionBucketRequest("Special Mention", null, null, 31, null, 20m, 10m)
        };
        ProvisionParameterRules.ValidateBuckets(buckets, null, null);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    [InlineData(1.001)]
    public void InvalidRateIsRejected(decimal value)
    {
        Assert.Throws<ProvisionParameterValidationException>(() =>
            ProvisionParameterRules.ValidateRate(value, "rate"));
    }
}
```

- [ ] 8.3 CREATE `Finnova.Tests/Integration/ProvisionParameterApiTests.cs`

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Finnova.Models.Contracts.ProvisionParameters;
using Finnova.Models.Domain.Enums;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Finnova.Tests.Integration;

public sealed class ProvisionParameterApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task MissingTokenIsUnauthorized()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/provision-parameters?page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateModifyAndReplayAreIdempotent()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.SystemAdminToken());
        var request = new CreateProvisionParameterRequest(
            Guid.Parse("11111111-1111-1111-1111-111111111111"), null, DelinquentType.Statutory,
            PeriodType.Days, true, 31, new DateOnly(2024, 1, 1), null, 0,
            new[] { new ProvisionBucketRequest("Standard", null, null, 0, null, 10m, 5m) });
        using var firstRequest = new HttpRequestMessage(HttpMethod.Post, "/api/provision-parameters") { Content = JsonContent.Create(request) };
        firstRequest.Headers.Add("Idempotency-Key", "fin15-create-1");
        var first = await client.SendAsync(firstRequest);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        using var replayRequest = new HttpRequestMessage(HttpMethod.Post, "/api/provision-parameters") { Content = JsonContent.Create(request) };
        replayRequest.Headers.Add("Idempotency-Key", "fin15-create-1");
        var replay = await client.SendAsync(replayRequest);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
    }
}

internal static class TestJwt
{
    public static string SystemAdminToken() => "test-token-configured-by-SystemAdminAppFactory";
}
```

- [ ] 8.4 CREATE `Finnova.Tests/Unit/ProvisionParameterBoundaryTests.cs`

```csharp
using Finnova.Models.Domain.Exceptions;

namespace Finnova.Tests.Unit;

public sealed class ProvisionParameterBoundaryTests
{
    [Fact]
    public void FeatureDoesNotExposeAuditMutationExceptionAsAWritePath()
    {
        Assert.Equal("ERR-PROV-AUDIT-IMMUTABLE", new ProvisionAuditImmutableException().ErrorCode);
        Assert.DoesNotContain("Receipt", typeof(Finnova.Service.ProvisionParameters.IProvisionParameterResolver).Assembly.GetReferencedAssemblies().Select(x => x.Name), StringComparer.OrdinalIgnoreCase);
    }
}
```

## 9. Frontend model and service layer

- [ ] 9.1 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\models\provisionParameter.model.ts`

```typescript
export type DelinquentType = 'Statutory' | 'Company';
export type PeriodType = 'Month' | 'Days';

export interface ProvisionBucket {
  id?: string;
  sortOrder: number;
  category: string;
  startDate: string | null;
  endDate: string | null;
  ageFrom: number;
  ageTo: number | null;
  baseProvisionPercentage: number;
  cashProvisionPercentage: number;
}

export interface ProvisionParameter {
  id: string;
  companyId: string;
  lineOfBusinessId: string | null;
  delinquentType: DelinquentType;
  periodType: PeriodType;
  isActive: boolean;
  cutOffMonth: number | null;
  effectiveStartDate: string | null;
  effectiveEndDate: string | null;
  ageingDays: number;
  version: number;
  rowVersion: string;
  buckets: ProvisionBucket[];
}

export type ProvisionParameterForm = Omit<ProvisionParameter, 'id' | 'version' | 'rowVersion'>;
export interface ReferenceOption { id: string; name: string; }
export interface ProvisionParameterPage { data: ProvisionParameter[]; total: number; page: number; pageSize: number; totalPages: number; }
```

- [ ] 9.2 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\services\interfaces\provisionParameter.interface.ts`

```typescript
import type { DelinquentType, PeriodType, ProvisionParameter, ProvisionParameterForm, ProvisionParameterPage, ReferenceOption } from '../../models/provisionParameter.model';

export interface ProvisionParameterQuery {
  companyId?: string;
  lineOfBusinessId?: string;
  globalScope?: boolean;
  delinquentType?: DelinquentType;
  periodType?: PeriodType;
  isActive?: boolean;
  evaluationDate?: string;
  page?: number;
  pageSize?: number;
}

export interface ProvisionParameterService {
  getPage(query: ProvisionParameterQuery): Promise<ProvisionParameterPage>;
  getById(id: string): Promise<ProvisionParameter>;
  getAudit(id: string): Promise<unknown[]>;
  create(form: ProvisionParameterForm, idempotencyKey: string): Promise<ProvisionParameter>;
  update(id: string, form: ProvisionParameterForm, version: number, rowVersion: string, idempotencyKey: string): Promise<ProvisionParameter>;
  getCompanies(): Promise<ReferenceOption[]>;
  getLinesOfBusiness(): Promise<ReferenceOption[]>;
}
```

- [ ] 9.3 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\services\mock\provisionParameter.mock.ts`

```typescript
import type { ProvisionParameterService, ProvisionParameterQuery } from '../interfaces/provisionParameter.interface';
import type { ProvisionParameter, ProvisionParameterForm, ReferenceOption } from '../../models/provisionParameter.model';

const companies: ReferenceOption[] = [{ id: '11111111-1111-1111-1111-111111111111', name: 'India Retail Finance Company' }];
const lines: ReferenceOption[] = [{ id: '22222222-2222-2222-2222-222222222222', name: 'Retail Lending' }];
let rows: ProvisionParameter[] = [];

function clone<T>(value: T): T { return JSON.parse(JSON.stringify(value)) as T; }
function page(query: ProvisionParameterQuery) {
  let result = rows.filter(x => !query.companyId || x.companyId === query.companyId)
    .filter(x => query.globalScope ? x.lineOfBusinessId === null : !query.lineOfBusinessId || x.lineOfBusinessId === query.lineOfBusinessId)
    .filter(x => !query.delinquentType || x.delinquentType === query.delinquentType)
    .filter(x => !query.periodType || x.periodType === query.periodType)
    .filter(x => query.isActive === undefined || x.isActive === query.isActive);
  const p = query.page ?? 1;
  const s = query.pageSize ?? 20;
  return { data: clone(result.slice((p - 1) * s, p * s)), total: result.length, page: p, pageSize: s, totalPages: Math.ceil(result.length / s) };
}

export const provisionParameterMockService: ProvisionParameterService = {
  async getPage(query) { return page(query); },
  async getById(id) { const row = rows.find(x => x.id === id); if (!row) throw new Error('Provision parameter set was not found.'); return clone(row); },
  async getAudit() { return []; },
  async create(form: ProvisionParameterForm) {
    if (rows.some(x => x.companyId === form.companyId && x.lineOfBusinessId === form.lineOfBusinessId && x.delinquentType === form.delinquentType && x.isActive)) throw Object.assign(new Error('Provision rate ranges must not overlap'), { response: { data: { message: 'Provision rate ranges must not overlap', code: 'ERR-PROV-RANGE-OVERLAP' } } });
    const row: ProvisionParameter = { ...clone(form), id: crypto.randomUUID(), version: 1, rowVersion: 'mock-1' };
    rows = [...rows, row];
    return clone(row);
  },
  async update(id, form, version) {
    const index = rows.findIndex(x => x.id === id);
    if (index < 0) throw new Error('Provision parameter set was not found.');
    if (rows[index].version !== version) throw Object.assign(new Error('The provision parameter was changed by another administrator.'), { response: { data: { message: 'The provision parameter was changed by another administrator.', code: 'ERR-PROV-CONCURRENCY' } } });
    rows[index] = { ...clone(form), id, version: version + 1, rowVersion: `mock-${version + 1}` };
    return clone(rows[index]);
  },
  async getCompanies() { return clone(companies); },
  async getLinesOfBusiness() { return clone(lines); },
};
```

- [ ] 9.4 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\services\real\provisionParameter.real.ts`

```typescript
import api from '../api';
import type { ProvisionParameterService, ProvisionParameterQuery } from '../interfaces/provisionParameter.interface';
import type { ProvisionParameter, ProvisionParameterForm } from '../../models/provisionParameter.model';

const path = '/provision-parameters';
export const provisionParameterRealService: ProvisionParameterService = {
  async getPage(query: ProvisionParameterQuery) { return (await api.get(path, { params: query })).data; },
  async getById(id: string) { return (await api.get(`${path}/${id}`)).data as ProvisionParameter; },
  async getAudit(id: string) { return (await api.get(`${path}/${id}/audit`)).data; },
  async create(form: ProvisionParameterForm, idempotencyKey: string) { return (await api.post(path, form, { headers: { 'Idempotency-Key': idempotencyKey } })).data; },
  async update(id: string, form: ProvisionParameterForm, version: number, rowVersion: string, idempotencyKey: string) { return (await api.put(`${path}/${id}`, { ...form, version, rowVersion }, { headers: { 'Idempotency-Key': idempotencyKey } })).data; },
  async getCompanies() { return (await api.get(`${path}/references/companies`)).data; },
  async getLinesOfBusiness() { return (await api.get(`${path}/references/lines-of-business`)).data; },
};
```

- [ ] 9.5 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\services\provisionParameter.service.ts`

```typescript
import { provisionParameterMockService } from './mock/provisionParameter.mock';
import { provisionParameterRealService } from './real/provisionParameter.real';
import type { ProvisionParameterService } from './interfaces/provisionParameter.interface';

const useMock = import.meta.env.VITE_USE_MOCK_API === 'true';
export const provisionParameterService: ProvisionParameterService = useMock ? provisionParameterMockService : provisionParameterRealService;
```

- [ ] 9.6 MODIFY `E:\Finnova\Finnova-UI\Finnova-UI\src\models\index.ts` by adding the export after the existing model exports.

```typescript
export * from './provisionParameter.model';
```

- [ ] 9.7 MODIFY `E:\Finnova\Finnova-UI\Finnova-UI\src\services\index.ts` by adding the service export after the existing service exports.

```typescript
export { provisionParameterService } from './provisionParameter.service';
```

## 10. Screenshot-aligned frontend page and wiring

- [ ] 10.1 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\pages\DelinquentParameterPage.tsx`

```tsx
import { useEffect, useState } from 'react';
import { Box, Button, Checkbox, FormControl, FormControlLabel, FormLabel, Grid, MenuItem, Radio, RadioGroup, Select, Stack, TextField, Typography } from '@mui/material';
import { provisionParameterService } from '../services';
import type { ProvisionBucket, ProvisionParameterForm, ReferenceOption } from '../models/provisionParameter.model';

const emptyBucket = (sortOrder: number): ProvisionBucket => ({ sortOrder, category: 'Standard', startDate: null, endDate: null, ageFrom: 0, ageTo: null, baseProvisionPercentage: 0, cashProvisionPercentage: 0 });
const emptyForm = (): ProvisionParameterForm => ({ companyId: '', lineOfBusinessId: null, delinquentType: 'Statutory', periodType: 'Month', isActive: true, cutOffMonth: null, effectiveStartDate: null, effectiveEndDate: null, ageingDays: 0, buckets: [emptyBucket(1)] });

export default function DelinquentParameterPage() {
  const [form, setForm] = useState<ProvisionParameterForm>(emptyForm());
  const [companies, setCompanies] = useState<ReferenceOption[]>([]);
  const [lines, setLines] = useState<ReferenceOption[]>([]);
  const [message, setMessage] = useState('');
  useEffect(() => { void Promise.all([provisionParameterService.getCompanies(), provisionParameterService.getLinesOfBusiness()]).then(([c, l]) => { setCompanies(c); setLines(l); }); }, []);
  const set = <K extends keyof ProvisionParameterForm>(key: K, value: ProvisionParameterForm[K]) => setForm(current => ({ ...current, [key]: value }));
  const setBucket = <K extends keyof ProvisionBucket>(index: number, key: K, value: ProvisionBucket[K]) => set('buckets', form.buckets.map((bucket, i) => i === index ? { ...bucket, [key]: value } : bucket));
  const save = async () => { try { await provisionParameterService.create(form, crypto.randomUUID()); setMessage('Provision parameter saved.'); } catch (error) { const e = error as { response?: { data?: { message?: string } } }; setMessage(e.response?.data?.message ?? 'Unable to save provision parameter.'); } };
  return <Box>
    <Typography variant="h5" sx={{ mb: 2 }}>Delinquent Parameter</Typography>
    <Grid container spacing={2}>
      <Grid item xs={12} md={4}><FormControl><FormLabel>Delinquent Type</FormLabel><RadioGroup row value={form.delinquentType} onChange={e => set('delinquentType', e.target.value as ProvisionParameterForm['delinquentType'])}><FormControlLabel value="Statutory" control={<Radio />} label="Statutory" /><FormControlLabel value="Company" control={<Radio />} label="Company" /></RadioGroup></FormControl></Grid>
      <Grid item xs={12} md={4}><TextField select fullWidth label="Company" value={form.companyId} onChange={e => set('companyId', e.target.value)}>{companies.map(x => <MenuItem key={x.id} value={x.id}>{x.name}</MenuItem>)}</TextField></Grid>
      <Grid item xs={12} md={4}><TextField select fullWidth label="Line of Business" value={form.lineOfBusinessId ?? ''} onChange={e => set('lineOfBusinessId', e.target.value || null)}><MenuItem value="">All</MenuItem>{lines.map(x => <MenuItem key={x.id} value={x.id}>{x.name}</MenuItem>)}</TextField></Grid>
      <Grid item xs={12} md={3}><TextField select fullWidth label="Period Type" value={form.periodType} onChange={e => set('periodType', e.target.value as ProvisionParameterForm['periodType'])}><MenuItem value="Month">Month</MenuItem><MenuItem value="Days">Days</MenuItem></TextField></Grid>
      <Grid item xs={12} md={3}><FormControlLabel control={<Checkbox checked={form.isActive} onChange={e => set('isActive', e.target.checked)} />} label="Active" /></Grid>
      <Grid item xs={12} md={3}><TextField fullWidth type="number" label="Cut of Month" value={form.cutOffMonth ?? ''} onChange={e => set('cutOffMonth', e.target.value === '' ? null : Number(e.target.value))} /></Grid>
      <Grid item xs={12} md={3}><TextField fullWidth type="number" label="Ageing Days" value={form.ageingDays} onChange={e => set('ageingDays', Number(e.target.value))} /></Grid>
      <Grid item xs={12} md={6}><TextField fullWidth type="date" label="Effective Start Date" InputLabelProps={{ shrink: true }} value={form.effectiveStartDate ?? ''} onChange={e => set('effectiveStartDate', e.target.value || null)} /></Grid>
      <Grid item xs={12} md={6}><TextField fullWidth type="date" label="Effective End Date" InputLabelProps={{ shrink: true }} value={form.effectiveEndDate ?? ''} onChange={e => set('effectiveEndDate', e.target.value || null)} /></Grid>
    </Grid>
    <Typography variant="h6" sx={{ mt: 3, mb: 1 }}>Provision Buckets</Typography>
    <Stack spacing={1}>{form.buckets.map((bucket, index) => <Grid container spacing={1} key={index}>
      <Grid item xs={2}><TextField select fullWidth label="Category" value={bucket.category} onChange={e => setBucket(index, 'category', e.target.value)}>{['Standard', 'Special Mention', 'Sub-Standard', 'Doubtful', 'Loss'].map(x => <MenuItem key={x} value={x}>{x}</MenuItem>)}</TextField></Grid>
      <Grid item xs={1.5}><TextField fullWidth type="date" label="Start Date" InputLabelProps={{ shrink: true }} value={bucket.startDate ?? ''} onChange={e => setBucket(index, 'startDate', e.target.value || null)} /></Grid>
      <Grid item xs={1.5}><TextField fullWidth type="date" label="End Date" InputLabelProps={{ shrink: true }} value={bucket.endDate ?? ''} onChange={e => setBucket(index, 'endDate', e.target.value || null)} /></Grid>
      <Grid item xs={1.5}><TextField fullWidth type="number" label={form.periodType === 'Month' ? 'Age of arrears (in Months)' : 'Age of arrears (in Days)'} value={bucket.ageFrom} onChange={e => setBucket(index, 'ageFrom', Number(e.target.value))} /></Grid>
      <Grid item xs={1.5}><TextField fullWidth type="number" label="Age To" value={bucket.ageTo ?? ''} onChange={e => setBucket(index, 'ageTo', e.target.value === '' ? null : Number(e.target.value))} /></Grid>
      <Grid item xs={1.5}><TextField fullWidth type="number" label="Base Provision %" value={bucket.baseProvisionPercentage} onChange={e => setBucket(index, 'baseProvisionPercentage', Number(e.target.value))} /></Grid>
      <Grid item xs={1.5}><TextField fullWidth type="number" label="Cash Provision %" value={bucket.cashProvisionPercentage} onChange={e => setBucket(index, 'cashProvisionPercentage', Number(e.target.value))} /></Grid>
      <Grid item xs={1}><Button onClick={() => set('buckets', form.buckets.filter((_, i) => i !== index).map((x, i) => ({ ...x, sortOrder: i + 1 })))}>Remove</Button></Grid>
    </Grid>)}</Stack>
    <Button sx={{ mt: 2 }} onClick={() => set('buckets', [...form.buckets, emptyBucket(form.buckets.length + 1)])}>Add Bucket</Button>
    {message && <Typography role="alert" sx={{ mt: 2 }}>{message}</Typography>}
    <Stack direction="row" spacing={2} justifyContent="flex-end" sx={{ mt: 3 }}><Button onClick={() => setForm(emptyForm())}>Clear</Button><Button onClick={() => window.history.back()}>Cancel</Button><Button variant="contained" onClick={() => void save()}>Save</Button></Stack>
  </Box>;
}
```

- [ ] 10.2 MODIFY `E:\Finnova\Finnova-UI\Finnova-UI\src\App.tsx` by adding the import after the other page imports and the route inside the existing protected `Layout` route.

```tsx
import DelinquentParameterPage from './pages/DelinquentParameterPage';
```

```tsx
                <Route path="/delinquent-parameters" element={<DelinquentParameterPage />} />
```

- [ ] 10.3 MODIFY `E:\Finnova\Finnova-UI\Finnova-UI\src\components\Layout.tsx` by adding the navigation item beside the existing SystemAdmin master navigation items.

```tsx
<ListItemButton component={NavLink} to="/delinquent-parameters" selected={location.pathname.startsWith('/delinquent-parameters')}>
  <ListItemText primary="Delinquent Parameter" />
</ListItemButton>
```

## 11. Frontend automated tests

- [ ] 11.1 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\pages\DelinquentParameterPage.test.tsx`

```tsx
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import DelinquentParameterPage from './DelinquentParameterPage';
import { provisionParameterService } from '../services';

vi.mock('../services', () => ({
  provisionParameterService: {
    getCompanies: vi.fn().mockResolvedValue([{ id: 'company-1', name: 'India Retail Finance Company' }]),
    getLinesOfBusiness: vi.fn().mockResolvedValue([{ id: 'lob-1', name: 'Retail Lending' }]),
    create: vi.fn().mockRejectedValue({ response: { data: { message: 'Provision rate ranges must not overlap' } } }),
  },
}));

describe('DelinquentParameterPage', () => {
  it('renders the approved English screenshot shape', async () => {
    render(<DelinquentParameterPage />);
    expect(screen.getByText('Delinquent Parameter')).toBeInTheDocument();
    expect(screen.getByLabelText('Statutory')).toBeInTheDocument();
    expect(screen.getByLabelText('Company')).toBeInTheDocument();
    expect(screen.getByLabelText('Period Type')).toBeInTheDocument();
    expect(screen.getByLabelText('Cut of Month')).toBeInTheDocument();
    expect(screen.getByText('Save')).toBeInTheDocument();
    await waitFor(() => expect(screen.getByText('India Retail Finance Company')).toBeInTheDocument());
  });

  it('maps All to a null LineOfBusinessId and displays the server overlap message', async () => {
    const user = userEvent.setup();
    render(<DelinquentParameterPage />);
    await waitFor(() => screen.getByText('Retail Lending'));
    await user.click(screen.getByText('Save'));
    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('Provision rate ranges must not overlap'));
    expect(vi.mocked(provisionParameterService.create)).toHaveBeenCalledWith(expect.objectContaining({ lineOfBusinessId: null }), expect.any(String));
  });

  it('clears local values without submitting', async () => {
    const user = userEvent.setup();
    render(<DelinquentParameterPage />);
    const ageing = screen.getByLabelText('Ageing Days');
    await user.clear(ageing);
    await user.type(ageing, '90');
    await user.click(screen.getByText('Clear'));
    expect(ageing).toHaveValue(0);
    expect(provisionParameterService.create).not.toHaveBeenCalled();
  });
});
```

- [ ] 11.2 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\services\provisionParameter.service.test.ts`

```typescript
import { describe, expect, it, vi } from 'vitest';
import { provisionParameterRealService } from './real/provisionParameter.real';
import api from './api';

vi.mock('./api', () => ({ default: { get: vi.fn(), post: vi.fn(), put: vi.fn() } }));

describe('provisionParameterRealService', () => {
  it('uses the gateway provision-parameters path and idempotency header', async () => {
    vi.mocked(api.post).mockResolvedValue({ data: { id: 'set-1' } } as never);
    await provisionParameterRealService.create({
      companyId: 'company-1', lineOfBusinessId: null, delinquentType: 'Statutory', periodType: 'Days',
      isActive: true, cutOffMonth: 31, effectiveStartDate: null, effectiveEndDate: null, ageingDays: 0,
      buckets: [{ sortOrder: 1, category: 'Loss', startDate: null, endDate: null, ageFrom: 0, ageTo: null, baseProvisionPercentage: 100, cashProvisionPercentage: 50 }],
    }, 'key-1');
    expect(api.post).toHaveBeenCalledWith('/provision-parameters', expect.any(Object), { headers: { 'Idempotency-Key': 'key-1' } });
  });
});
```

## 12. Compatibility gate and boundary test

- [ ] 12.1 CREATE `Finnova.Tests/Unit/ProvisionParameterCompatibilityGateTests.cs`

```csharp
using Finnova.Models.Domain.Enums;
using Finnova.Service.ProvisionParameters;

namespace Finnova.Tests.Unit;

public sealed class ProvisionParameterCompatibilityGateTests
{
    [Fact]
    public void RequiredEnumValuesAndCategoriesAreIndiaSafe()
    {
        Assert.Equal(new[] { "Standard", "Special Mention", "Sub-Standard", "Doubtful", "Loss" }, ProvisionParameterRules.Categories);
        Assert.Equal(DelinquentType.Statutory, DelinquentType.Statutory);
        Assert.Equal(DelinquentType.Company, DelinquentType.Company);
        Assert.Equal(PeriodType.Month, PeriodType.Month);
        Assert.Equal(PeriodType.Days, PeriodType.Days);
    }

    [Fact]
    public void ResolverIsReadOnlyAndDoesNotOwnAccountingPosting()
    {
        var methods = typeof(IProvisionParameterResolver).GetMethods().Select(x => x.Name).ToArray();
        Assert.Equal(new[] { "ResolveAsync" }, methods);
        Assert.DoesNotContain(methods, x => x.Contains("Journal", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methods, x => x.Contains("Receipt", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methods, x => x.Contains("Appropriation", StringComparison.OrdinalIgnoreCase));
    }
}
```

- [ ] 12.2 MODIFY `Finnova.Tests/Integration/SystemAdminAppFactory.cs` by adding only the confirmed FINNOVA-15 authorization policy/claims fixture and Company/LOB reference seed identifiers. Do not seed duplicate Company or LOB rows; use the existing authoritative seed methods and add this exact assertion helper beside the existing token helpers.

```csharp
public static Guid ProvisionTestCompanyId => Guid.Parse("11111111-1111-1111-1111-111111111111");
public static Guid ProvisionTestLineOfBusinessId => Guid.Parse("22222222-2222-2222-2222-222222222222");
```

- [ ] 12.3 MODIFY `Finnova.ApiGateway/appsettings.json` and the UI service base path only if the compatibility gate confirms that the shared axios base path is not already `/api/ua/api`; preserve the existing alias route when it is already correct.

```json
"Path": "/api/ua/api/provision-parameters/{**catch-all}"
```

## Notes

- Do not run the migration command, build, test suite, development server, or application process while creating this spec.
- The generated migration belongs to the later implementation change and must not contain seed data for non-India or non-English values.
- FINNOVA-15 is a separate provision-parameter master. It does not modify Receipt Processing, receipt appropriation, canonical journals, or the existing JVE worker boundary.
- Tasks 12.1 through 12.3 are mandatory compatibility gates, not permission to invent source names or add duplicate masters.

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3", "2.1", "2.2", "9.1", "9.2"] },
    { "id": 1, "tasks": ["3.1", "3.2", "4.1", "4.2", "4.3", "5.1", "9.3", "9.4"] },
    { "id": 2, "tasks": ["3.3", "4.4", "6.1", "6.2", "6.3", "9.5", "9.6", "9.7"] },
    { "id": 3, "tasks": ["6.4", "7.1", "7.2", "7.3", "7.4"] },
    { "id": 4, "tasks": ["8.1", "8.2", "8.3", "8.4", "10.1", "10.2", "10.3"] },
    { "id": 5, "tasks": ["11.1", "11.2", "12.1", "12.2", "12.3"] }
  ]
}
```
