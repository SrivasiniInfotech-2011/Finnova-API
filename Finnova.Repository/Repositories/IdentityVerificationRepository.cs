using Finnova.Models.Contracts.IdentityVerification;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Finnova.Repository.Repositories;

public sealed class IdentityVerificationRepository : RepositoryBase<ValidationRecord>, IIdentityVerificationRepository
{
    public IdentityVerificationRepository(FinnovaDbContext context) : base(context)
    {
    }

    public async Task AddSessionAsync(VerificationSession session, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        session.CreatedUtc = DateTime.UtcNow;

        try
        {
            await Context.VerificationSessions.AddAsync(session, ct);
            await Context.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new IdentityVerificationPersistenceException(ex);
        }
    }

    public async Task AppendRecordAsync(ValidationRecord record, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        record.CreatedUtc = DateTime.UtcNow;
        IdentityVerificationInvariants.EnsureResultDomain(record);

        try
        {
            await DbSet.AddAsync(record, ct);
            await Context.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new IdentityVerificationPersistenceException(ex);
        }
    }

    public async Task AppendRecordsAsync(IReadOnlyCollection<ValidationRecord> records, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(records);

        var createdUtc = DateTime.UtcNow;
        foreach (var record in records)
        {
            ArgumentNullException.ThrowIfNull(record);
            record.CreatedUtc = createdUtc;
            IdentityVerificationInvariants.EnsureResultDomain(record);
        }

        try
        {
            await DbSet.AddRangeAsync(records, ct);
            await Context.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new IdentityVerificationPersistenceException(ex);
        }
    }

    public Task<VerificationSession?> GetSessionAsync(Guid id, CancellationToken ct = default) =>
        Context.VerificationSessions
            .AsNoTracking()
            .Include(x => x.Records)
            .SingleOrDefaultAsync(x => x.Id == id, ct);

    public Task<ValidationRecord?> GetRecordAsync(Guid id, CancellationToken ct = default) =>
        DbSet
            .AsNoTracking()
            .Include(x => x.ProtectedDocumentReference)
            .SingleOrDefaultAsync(x => x.Id == id, ct);

    public async Task<(List<ValidationRecord> Items, int Total)> GetPagedAsync(
        VerificationRecordQuery query,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var records = DbSet.AsNoTracking().AsQueryable();

        if (query.OperationType is not null)
        {
            records = records.Where(x => x.OperationType == query.OperationType);
        }

        if (query.ValidationStatus is not null)
        {
            records = records.Where(x => x.ValidationStatus == query.ValidationStatus);
        }

        if (query.ComparisonOutcome is not null)
        {
            records = records.Where(x => x.ComparisonOutcome == query.ComparisonOutcome);
        }

        if (query.Method is not null)
        {
            records = records.Where(x => x.Method == query.Method);
        }

        if (query.SessionId is not null)
        {
            records = records.Where(x => x.SessionId == query.SessionId);
        }

        if (query.StartUtc is not null)
        {
            records = records.Where(x => x.CreatedUtc >= query.StartUtc);
        }

        if (query.EndUtc is not null)
        {
            records = records.Where(x => x.CreatedUtc < query.EndUtc);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            records = records.Where(x =>
                x.ProtectedSearchReference == search ||
                x.CorrelationId == search ||
                x.Id.ToString() == search);
        }

        var total = await records.CountAsync(ct);
        var items = await records
            .OrderByDescending(x => x.CreatedUtc)
            .ThenByDescending(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task<List<ValidationRecord>> GetBySessionAsync(Guid id, CancellationToken ct = default) =>
        DbSet
            .AsNoTracking()
            .Where(x => x.SessionId == id)
            .OrderByDescending(x => x.CreatedUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync(ct);

    public Task<ProtectedDocumentReference?> GetProtectedDocumentReferenceAsync(Guid id, CancellationToken ct = default) =>
        Context.ProtectedDocumentReferences
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, ct);

    public override Task AddAsync(ValidationRecord entity, CancellationToken cancellationToken = default) =>
        throw new IdentityVerificationImmutableException();

    public override Task UpdateAsync(ValidationRecord entity, CancellationToken cancellationToken = default) =>
        throw new IdentityVerificationImmutableException();

    public override Task DeleteAsync(ValidationRecord entity, CancellationToken cancellationToken = default) =>
        throw new IdentityVerificationImmutableException();
}
