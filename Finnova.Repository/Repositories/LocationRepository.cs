using Microsoft.EntityFrameworkCore;
using Finnova.Models.Domain.Entities;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;

namespace Finnova.Repository.Repositories;

public class LocationRepository : RepositoryBase<Location>, ILocationRepository
{
    public LocationRepository(FinnovaDbContext context) : base(context) { }

    public async Task<List<Location>> GetAllFlatAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet.AsNoTracking()
            .OrderBy(l => l.Level)
            .ThenBy(l => l.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Location>> GetByLevelAsync(int level, CancellationToken cancellationToken = default)
    {
        return await DbSet.AsNoTracking()
            .Where(l => l.Level == level)
            .OrderBy(l => l.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Location>> GetChildrenAsync(Guid parentId, CancellationToken cancellationToken = default)
    {
        return await DbSet.AsNoTracking()
            .Where(l => l.ParentId == parentId)
            .OrderBy(l => l.Name)
            .ToListAsync(cancellationToken);
    }
}
