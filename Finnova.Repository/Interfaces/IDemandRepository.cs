using Finnova.Models.Contracts.DemandProcessing;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Interfaces;

public interface IDemandRepository
{
    Task<DemandRun?> FindByLogicalScopeAsync(string logicalScopeKey, CancellationToken ct);
    Task<DemandRun?> GetRunAsync(Guid runId, CancellationToken ct);
    Task<DemandBatch?> GetBatchAsync(Guid batchId, IReadOnlySet<Guid> branchScope, CancellationToken ct);
    IQueryable<DemandRow> AuthorizedRows(IReadOnlySet<Guid> branches);
    IQueryable<DemandBatch> AuthorizedBatches(IReadOnlySet<Guid> branches);
    Task<(IReadOnlyList<DemandRun> Items, int Total)> SearchRunsAsync(DemandHistoryQuery query, IReadOnlySet<Guid> branches, CancellationToken ct);
    Task<(IReadOnlyList<DemandRow> Items, int Total)> SearchRowsAsync(DemandRowsQuery query, IReadOnlySet<Guid> branches, CancellationToken ct);
    Task<(IReadOnlyList<DemandBatch> Items, int Total)> SearchBatchesAsync(DemandBatchesQuery query, IReadOnlySet<Guid> branches, CancellationToken ct);
    Task AddAsync(DemandRun run, CancellationToken ct);
    Task AddFailureAsync(DemandFailure failure, CancellationToken ct);
    Task AddAuditAsync(DemandAuditEntry entry, CancellationToken ct);
    Task AddSnapshotAsync(DemandSnapshot snapshot, CancellationToken ct);
    Task AddRowsAsync(IEnumerable<DemandRow> rows, CancellationToken ct);
    Task AddBatchesAsync(IEnumerable<DemandBatch> batches, CancellationToken ct);
    Task AddBillingStageAsync(BillingStageRun stage, CancellationToken ct);
    Task AddBillingResultAsync(BillingPeriodResult result, CancellationToken ct);
    Task AddAllocationEvidenceAsync(BillingAllocationEvidence evidence, CancellationToken ct);
    Task AddBillingSnapshotAsync(BillingSnapshot snapshot, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
    Task<IDisposable> BeginTransactionAsync(CancellationToken ct);
}
