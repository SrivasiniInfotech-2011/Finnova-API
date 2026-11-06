using Finnova.Models.Contracts.Applications;
using Finnova.Models.Contracts.UserManagement;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Finnova.Repository.Repositories;

public sealed class LineOfBusinessReferenceAdapter : ILineOfBusinessReferencePort
{
    private readonly FinnovaDbContext _db;
    public LineOfBusinessReferenceAdapter(FinnovaDbContext db) => _db = db;
    public Task<List<LineOfBusinessRefResponse>> GetActiveAsync(CancellationToken ct) =>
        _db.LinesOfBusiness.AsNoTracking().Where(x => x.IsActive)
            .OrderBy(x => x.LOB_Name).Select(x => new LineOfBusinessRefResponse(x.Id, x.LOB_Name, x.LOB_Description)).ToListAsync(ct);
    public Task<bool> ExistsActiveAsync(Guid id, CancellationToken ct) =>
        _db.LinesOfBusiness.AsNoTracking().AnyAsync(x => x.Id == id && x.IsActive, ct);
}

public sealed class CustomerReferenceAdapter : ICustomerReferencePort
{
    private readonly FinnovaDbContext _db;
    public CustomerReferenceAdapter(FinnovaDbContext db) => _db = db;
    public async Task<(List<CustomerReferenceOptionResponse> Items, int Total)> SearchActiveAsync(string? search, int page, int pageSize, CancellationToken ct)
    {
        var q = _db.Customers.AsNoTracking().Where(x => x.Status == CustomerLifecycleStatus.Active);
        var term = search?.Trim();
        if (!string.IsNullOrEmpty(term)) q = q.Where(x => x.CustomerCode.Contains(term));
        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(x => x.CustomerCode).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new CustomerReferenceOptionResponse(x.Id, x.CustomerCode, x.CustomerCode)).ToListAsync(ct);
        return (items, total);
    }
    public Task<bool> ExistsActiveAsync(Guid id, CancellationToken ct) =>
        _db.Customers.AsNoTracking().AnyAsync(x => x.Id == id && x.Status == CustomerLifecycleStatus.Active, ct);
}

public sealed class BranchReferenceAdapter : IBranchReferencePort
{
    private readonly FinnovaDbContext _db;
    public BranchReferenceAdapter(FinnovaDbContext db) => _db = db;
    public Task<List<ApplicationReferenceOptionResponse>> GetActiveAsync(Guid? lineOfBusinessId, CancellationToken ct) =>
        _db.Locations.AsNoTracking().Where(x => x.IsActive && x.Level == 5).OrderBy(x => x.Name)
            .Select(x => new ApplicationReferenceOptionResponse(x.Id, x.Code, x.Name)).ToListAsync(ct);
    public Task<bool> ExistsActiveAsync(Guid id, CancellationToken ct) =>
        _db.Locations.AsNoTracking().AnyAsync(x => x.Id == id && x.IsActive && x.Level == 5, ct);
}
