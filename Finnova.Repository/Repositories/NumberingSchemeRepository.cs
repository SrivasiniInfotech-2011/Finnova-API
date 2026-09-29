using System.Data;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Models.Domain.Numbering;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Finnova.Repository.Repositories;

/// <summary>
/// Repository for numbering schemes: search/paging, uniqueness checks, and atomic issuance.
/// </summary>
public class NumberingSchemeRepository(FinnovaDbContext finnovaDbContext)
    : RepositoryBase<NumberingScheme>(finnovaDbContext), INumberingSchemeRepository
{
    public async Task<(List<NumberingScheme> Items, int Total)> GetPagedAsync(
        string? searchTerm, int page, int pageSize, CancellationToken ct = default)
    {
        var query = DbSet.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(x =>
                x.Code.Contains(term) || x.Name.Contains(term) || x.DocumentType.Contains(term));
        }
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(x => x.Name).ThenBy(x => x.Code)   // R4.7
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public async Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default)
    {
        var c = code.Trim();
        return await DbSet.AsNoTracking().AnyAsync(
            x => x.Code == c && (excludeId == null || x.Id != excludeId), ct);
    }

    public async Task<bool> ExistsByScopeAsync(
        string documentType, NumberScope scope, Guid? scopeId, Guid? excludeId = null, CancellationToken ct = default)
    {
        var dt = documentType.Trim();
        return await DbSet.AsNoTracking().AnyAsync(
            x => x.DocumentType == dt && x.Scope == scope && x.ScopeId == scopeId
                 && (excludeId == null || x.Id != excludeId), ct);
    }

    public async Task<(NumberingScheme Scheme, long SequenceValue)?> IssueNextAsync(
        string documentType, NumberScope scope, Guid? scopeId, DateTime utcNow, CancellationToken ct = default)
    {
        async Task<(NumberingScheme, long)?> Body()
        {
            // TRACKED load (not AsNoTracking) so the sequence update is persisted.
            var scheme = await DbSet.FirstOrDefaultAsync(
                x => x.DocumentType == documentType && x.Scope == scope && x.ScopeId == scopeId, ct);
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

        // Relational providers get a Serializable transaction (with the provider execution strategy
        // handling transient serialization retries) so concurrent issuance for the same scheme is
        // serialized and never yields duplicate/gap sequence values. The InMemory provider (tests)
        // does not support transactions, so it takes the direct path.
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
}
