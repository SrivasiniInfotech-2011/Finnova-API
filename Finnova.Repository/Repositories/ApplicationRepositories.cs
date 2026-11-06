using Finnova.Models.Domain.Entities;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Finnova.Repository.Repositories;

public sealed class ApplicationRepository : RepositoryBase<Application>, IApplicationRepository
{
    public ApplicationRepository(FinnovaDbContext context) : base(context) { }
    public async Task<(List<Application> Items, int Total)> SearchAsync(ApplicationFilters f, CancellationToken ct)
    {
        var q = DbSet.AsNoTracking().AsQueryable(); var term = f.Search?.Trim();
        if (!string.IsNullOrEmpty(term)) q = q.Where(x => x.ApplicationNumber.Contains(term) || x.CustomerId.ToString().Contains(term));
        if (f.LineOfBusinessId.HasValue) q = q.Where(x => x.LineOfBusinessId == f.LineOfBusinessId);
        if (f.CustomerId.HasValue) q = q.Where(x => x.CustomerId == f.CustomerId);
        if (!string.IsNullOrWhiteSpace(f.ProductCode)) q = q.Where(x => x.ProductCode == f.ProductCode.Trim());
        if (f.BranchId.HasValue) q = q.Where(x => x.BranchId == f.BranchId);
        if (!string.IsNullOrWhiteSpace(f.SchemeCode)) q = q.Where(x => x.SchemeCode == f.SchemeCode.Trim());
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.ApplicationNumber)
            .Skip((f.Page - 1) * f.PageSize).Take(f.PageSize).ToListAsync(ct);
        return (items, total);
    }
    public Task<Application?> GetTrackedAsync(Guid id, CancellationToken ct) => DbSet.FirstOrDefaultAsync(x => x.Id == id, ct);
}

public sealed class ApplicationScheduleRepository : IApplicationScheduleRepository
{
    private readonly FinnovaDbContext _db;
    public ApplicationScheduleRepository(FinnovaDbContext db) => _db = db;
    public Task<ApplicationSchedule?> GetByApplicationIdAsync(Guid id, CancellationToken ct) =>
        _db.ApplicationSchedules.AsNoTracking().Include(x => x.Installments).FirstOrDefaultAsync(x => x.ApplicationId == id, ct);
    public Task AddAsync(ApplicationSchedule schedule, CancellationToken ct) => _db.ApplicationSchedules.AddAsync(schedule, ct).AsTask();
}

public sealed class ApplicationStatusAuditRepository : IApplicationStatusAuditRepository
{
    private readonly FinnovaDbContext _db;
    public ApplicationStatusAuditRepository(FinnovaDbContext db) => _db = db;
    public Task AddAsync(ApplicationStatusAuditEntry entry, CancellationToken ct) => _db.ApplicationStatusAuditEntries.AddAsync(entry, ct).AsTask();
}
