using Finnova.Models.Contracts.DemandProcessing;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Context;
using Microsoft.EntityFrameworkCore;

namespace Finnova.Repository.Repositories;

public sealed class DemandRepository(FinnovaDbContext db) : Interfaces.IDemandRepository
{
    private IQueryable<DemandRun> Runs => db.DemandRuns.Include(x => x.BranchScopes).Include(x => x.Revisions);
    public Task<DemandRun?> FindByLogicalScopeAsync(string key, CancellationToken ct) => Runs.AsNoTracking().SingleOrDefaultAsync(x => x.LogicalScopeKey == key, ct);
    public Task<DemandRun?> GetRunAsync(Guid id, CancellationToken ct) => Runs.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<DemandBatch?> GetBatchAsync(Guid id, IReadOnlySet<Guid> branches, CancellationToken ct) => db.DemandBatches.Include(x => x.Rows).SingleOrDefaultAsync(x => x.Id == id && branches.Contains(x.BranchId), ct);
    public IQueryable<DemandRow> AuthorizedRows(IReadOnlySet<Guid> branches) => db.DemandRows.Where(x => branches.Contains(x.BranchId) && x.Status == DemandRowStatus.Completed);
    public IQueryable<DemandBatch> AuthorizedBatches(IReadOnlySet<Guid> branches) => db.DemandBatches.Where(x => branches.Contains(x.BranchId));

    public async Task<(IReadOnlyList<DemandRun> Items, int Total)> SearchRunsAsync(DemandHistoryQuery query, IReadOnlySet<Guid> branches, CancellationToken ct)
    {
        var q = Runs.AsNoTracking().Where(x => x.BranchScopes.Any(s => branches.Contains(s.BranchId)));
        if (query.PeriodYear.HasValue) q = q.Where(x => x.PeriodYear == query.PeriodYear.Value);
        if (query.PeriodMonth.HasValue) q = q.Where(x => x.PeriodMonth == query.PeriodMonth.Value);
        if (query.LineOfBusinessId.HasValue) q = q.Where(x => x.LineOfBusinessId == query.LineOfBusinessId.Value);
        if (query.BranchId.HasValue) q = q.Where(x => x.BranchScopes.Any(s => s.BranchId == query.BranchId.Value));
        if (query.Status.HasValue) q = q.Where(x => x.Status == query.Status.Value);
        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(x => x.PeriodYear).ThenBy(x => x.PeriodMonth).ThenBy(x => x.CompletedAtUtc).ThenBy(x => x.CurrentRevisionNumber).ThenBy(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        return (items, total);
    }

    public async Task<(IReadOnlyList<DemandRow> Items, int Total)> SearchRowsAsync(DemandRowsQuery query, IReadOnlySet<Guid> branches, CancellationToken ct)
    {
        var q = AuthorizedRows(branches).AsNoTracking().Include(x => x.Revision).ThenInclude(x => x.DemandRun).AsQueryable();
        if (query.RunId.HasValue) q = q.Where(x => x.Revision.DemandRunId == query.RunId.Value);
        if (query.RevisionNumber.HasValue) q = q.Where(x => x.Revision.RevisionNumber == query.RevisionNumber.Value);
        if (query.PeriodYear.HasValue) q = q.Where(x => x.Revision.DemandRun.PeriodYear == query.PeriodYear.Value);
        if (query.PeriodMonth.HasValue) q = q.Where(x => x.Revision.DemandRun.PeriodMonth == query.PeriodMonth.Value);
        if (query.LineOfBusinessId.HasValue) q = q.Where(x => x.LineOfBusinessId == query.LineOfBusinessId.Value);
        if (query.BranchId.HasValue) q = q.Where(x => x.BranchId == query.BranchId.Value);
        if (query.AccountId.HasValue) q = q.Where(x => x.AccountId == query.AccountId.Value);
        if (query.CustomerId.HasValue) q = q.Where(x => x.CustomerId == query.CustomerId.Value);
        if (query.RowStatus.HasValue) q = q.Where(x => x.Status == query.RowStatus.Value);
        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(x => x.OrderingKey).ThenBy(x => x.AccountId).ThenBy(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        return (items, total);
    }

    public async Task<(IReadOnlyList<DemandBatch> Items, int Total)> SearchBatchesAsync(DemandBatchesQuery query, IReadOnlySet<Guid> branches, CancellationToken ct)
    {
        var q = AuthorizedBatches(branches).AsNoTracking().AsQueryable();
        if (query.RunId.HasValue) q = q.Where(x => x.DemandRunId == query.RunId.Value);
        if (query.RevisionNumber.HasValue) q = q.Where(x => x.RevisionNumber == query.RevisionNumber.Value);
        if (query.PeriodYear.HasValue) q = q.Where(x => x.PeriodYear == query.PeriodYear.Value);
        if (query.PeriodMonth.HasValue) q = q.Where(x => x.PeriodMonth == query.PeriodMonth.Value);
        if (query.LineOfBusinessId.HasValue) q = q.Where(x => x.LineOfBusinessId == query.LineOfBusinessId.Value);
        if (query.BranchId.HasValue) q = q.Where(x => x.BranchId == query.BranchId.Value);
        if (query.Status.HasValue) q = q.Where(x => x.Status == query.Status.Value);
        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(x => x.BranchId).ThenBy(x => x.DemandRunId).ThenBy(x => x.RevisionNumber).ThenBy(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        return (items, total);
    }

    public Task AddAsync(DemandRun run, CancellationToken ct) => db.DemandRuns.AddAsync(run, ct).AsTask();
    public Task AddFailureAsync(DemandFailure failure, CancellationToken ct) => db.DemandFailures.AddAsync(failure, ct).AsTask();
    public Task AddAuditAsync(DemandAuditEntry entry, CancellationToken ct) => db.DemandAuditEntries.AddAsync(entry, ct).AsTask();
    public Task AddSnapshotAsync(DemandSnapshot snapshot, CancellationToken ct) => db.DemandSnapshots.AddAsync(snapshot, ct).AsTask();
    public async Task AddRowsAsync(IEnumerable<DemandRow> rows, CancellationToken ct) => await db.DemandRows.AddRangeAsync(rows, ct);
    public async Task AddBatchesAsync(IEnumerable<DemandBatch> batches, CancellationToken ct) => await db.DemandBatches.AddRangeAsync(batches, ct);
    public Task AddBillingStageAsync(BillingStageRun stage, CancellationToken ct) => db.BillingStageRuns.AddAsync(stage, ct).AsTask();
    public Task AddBillingResultAsync(BillingPeriodResult result, CancellationToken ct) => db.BillingPeriodResults.AddAsync(result, ct).AsTask();
    public Task AddAllocationEvidenceAsync(BillingAllocationEvidence evidence, CancellationToken ct) => db.BillingAllocationEvidence.AddAsync(evidence, ct).AsTask();
    public Task AddBillingSnapshotAsync(BillingSnapshot snapshot, CancellationToken ct) => db.BillingSnapshots.AddAsync(snapshot, ct).AsTask();
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
    public async Task<IDisposable> BeginTransactionAsync(CancellationToken ct) => await db.Database.BeginTransactionAsync(ct);
}
