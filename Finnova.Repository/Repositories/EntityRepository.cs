using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Finnova.Repository.Repositories;

public class EntityRepository(FinnovaDbContext finnovaDbContext)
    : RepositoryBase<EntityMaster>(finnovaDbContext), IEntityRepository
{
    public async Task<(List<EntityMaster> Items, int Total)> GetPagedAsync(
        string? searchTerm, EntityType? entityType, int page, int pageSize, CancellationToken ct = default)
    {
        var query = DbSet.AsNoTracking().AsQueryable();

        if (entityType is not null)
            query = query.Where(x => x.EntityType == entityType);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(x =>
                x.Code.Contains(term)
                || x.Name.Contains(term)
                || (x.RegistrationIdentifier != null && x.RegistrationIdentifier.Contains(term)));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(x => x.Name).ThenBy(x => x.Code)   // R3.10
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public async Task<bool> ExistsByCodeAsync(
        string code, EntityType entityType, Guid? excludeId = null, CancellationToken ct = default)
    {
        var c = code.Trim();
        return await DbSet.AsNoTracking().AnyAsync(
            x => x.EntityType == entityType && x.Code == c && (excludeId == null || x.Id != excludeId), ct);
    }
}