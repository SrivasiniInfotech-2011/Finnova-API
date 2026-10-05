using Microsoft.EntityFrameworkCore;
using Finnova.Models.Domain.Entities;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;

namespace Finnova.Repository.Repositories;

public class DraweeBankRepository : RepositoryBase<DraweeBank>, IDraweeBankRepository
{
    public DraweeBankRepository(FinnovaDbContext context) : base(context) { }

    public async Task<(List<DraweeBank> Items, int Total)> GetPagedAsync(
        string? searchTerm, int page, int pageSize, CancellationToken ct = default)
    {
        var query = DbSet
            .AsNoTracking()
            .Include(x => x.Branches)
            .Include(x => x.Restriction)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            // EF Core translates Contains to SQL LIKE; default CI collation => case-insensitive (R8.1).
            query = query.Where(x => x.BankCode.Contains(term) || x.BankName.Contains(term));
        }

        var total = await query.CountAsync(ct);                       // total before paging (R8.4)

        var items = await query
            .OrderBy(x => x.BankName).ThenBy(x => x.BankCode)         // deterministic order (R8.10)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        // Challan rules are decoupled from the aggregate — not loaded or attached to page items.
        return (items, total);
    }

    public async Task<DraweeBank?> GetAggregateByIdAsync(Guid id, CancellationToken ct = default)
    {
        // Bank aggregate = bank + owned branches + optional restriction. Challan rules are NOT
        // part of the aggregate and are read separately via GetChallanRulesByBankIdAsync.
        return await DbSet
            .Include(x => x.Branches)
            .Include(x => x.Restriction)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<bool> ExistsByBankCodeAsync(
        string bankCode, Guid? excludeId = null, CancellationToken ct = default)
    {
        var c = bankCode.Trim();
        return await DbSet.AsNoTracking().AnyAsync(
            x => x.BankCode == c && (excludeId == null || x.Id != excludeId), ct);   // CI via collation (R2.1)
    }

    public async Task<List<ChallanRule>> GetChallanRulesByBankIdAsync(Guid draweeBankId, CancellationToken ct = default)
        => await Context.Set<ChallanRule>().AsNoTracking()
            .Where(r => r.DraweeBankId == draweeBankId)
            .OrderBy(r => r.RuleCode)                 // deterministic order for the read endpoint
            .ToListAsync(ct);

    public async Task<ChallanRule?> GetChallanRuleByIdAsync(Guid ruleId, CancellationToken ct = default)
        => await Context.Set<ChallanRule>().FirstOrDefaultAsync(r => r.Id == ruleId, ct);

    public async Task<bool> ChallanRuleCodeExistsAsync(
        Guid draweeBankId, string ruleCode, Guid? excludeId = null, CancellationToken ct = default)
    {
        var code = ruleCode.Trim();
        return await Context.Set<ChallanRule>().AsNoTracking().AnyAsync(
            r => r.DraweeBankId == draweeBankId && r.RuleCode == code
                 && (excludeId == null || r.Id != excludeId), ct);   // CI via collation (R6.4)
    }

    public async Task AddChallanRuleAsync(ChallanRule rule, CancellationToken ct = default)
    {
        await Context.Set<ChallanRule>().AddAsync(rule, ct);
        await Context.SaveChangesAsync(ct);
    }

    public async Task UpdateChallanRuleAsync(ChallanRule rule, CancellationToken ct = default)
    {
        Context.Set<ChallanRule>().Update(rule);
        await Context.SaveChangesAsync(ct);
    }
}
