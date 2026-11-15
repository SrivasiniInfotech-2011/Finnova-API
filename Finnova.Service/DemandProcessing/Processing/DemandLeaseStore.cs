using Finnova.Models.Domain.Entities;
using Finnova.Repository.Context;
using Finnova.Service.DemandProcessing.Ports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Finnova.Service.DemandProcessing.Configuration;

namespace Finnova.Service.DemandProcessing.Processing;

public sealed class DemandLeaseStore(FinnovaDbContext db, IOptions<DemandProcessingOptions> options, IClock clock) : IDemandLeaseStore
{
    private readonly string _owner = $"accounts-service:{Environment.ProcessId}";
    public async Task<bool> TryAcquireAsync(string logicalScopeKey, Guid runId, CancellationToken ct)
    {
        var lease = await db.DemandProcessingLeases.SingleOrDefaultAsync(x => x.LogicalScopeKey == logicalScopeKey, ct); var now = clock.UtcNow; if (lease is not null && lease.ExpiresAtUtc > now && lease.DemandRunId != runId) return false;
        if (lease is null) db.DemandProcessingLeases.Add(new DemandProcessingLease { LogicalScopeKey = logicalScopeKey, DemandRunId = runId, OwnerId = _owner, AcquiredAtUtc = now, ExpiresAtUtc = now.AddMinutes(10) }); else { lease.DemandRunId = runId; lease.OwnerId = _owner; lease.AcquiredAtUtc = now; lease.ExpiresAtUtc = now.AddMinutes(10); }
        await db.SaveChangesAsync(ct); return true;
    }
    public async Task<bool> RenewAsync(Guid runId, CancellationToken ct) { var lease = await db.DemandProcessingLeases.SingleOrDefaultAsync(x => x.DemandRunId == runId && x.OwnerId == _owner, ct); if (lease is null || lease.ExpiresAtUtc <= clock.UtcNow) return false; lease.ExpiresAtUtc = clock.UtcNow.AddMinutes(10); await db.SaveChangesAsync(ct); return true; }
    public async Task ReleaseAsync(Guid runId, CancellationToken ct) { var lease = await db.DemandProcessingLeases.SingleOrDefaultAsync(x => x.DemandRunId == runId && x.OwnerId == _owner, ct); if (lease is not null) { lease.ExpiresAtUtc = clock.UtcNow; await db.SaveChangesAsync(ct); } }
}
