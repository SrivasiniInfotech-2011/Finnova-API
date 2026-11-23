using Finnova.Data.Seeder.DemandSample;
using Finnova.Models.Contracts.DemandProcessing;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.ValueObjects;
using Finnova.Repository.Context;
using Finnova.Service.DemandProcessing.Ports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace Finnova.Tests.DemandProcessing.Support;

public sealed class SampleAccountSourcePort(FinnovaDbContext db) : IAccountSourcePort
{
    public async IAsyncEnumerable<AccountProjection> ReadAsync(DemandScope scope, string snapshotReference, [EnumeratorCancellation] CancellationToken ct)
    {
        var ids = SampleDataCatalog.Branches.Select(x => SampleDataCatalog.StableId("Location", x.Code)).ToArray();
        var rows = await db.LoanAccounts.AsNoTracking().Where(x => x.LineOfBusinessId == scope.LineOfBusinessId && x.Status == AccountStatus.Active && x.BranchId.HasValue && ids.Contains(x.BranchId.Value) && scope.BranchIds.Contains(x.BranchId.Value)).OrderBy(x => x.AccountNumber).ToListAsync(ct);
        foreach (var row in rows) { ct.ThrowIfCancellationRequested(); if (row.CustomerId is null || row.LineOfBusinessId is null || row.BranchId is null) continue; yield return new AccountProjection(row.Id, row.CustomerId.Value, row.LineOfBusinessId.Value, row.BranchId.Value, row.AccountNumber, row.CustomerNameSnapshot ?? row.AccountName, row.Status.ToString(), true, true, true, snapshotReference); }
    }

    public async Task<IReadOnlyList<Guid>> ResolveActiveBranchesAsync(Guid lineOfBusinessId, System.Security.Claims.ClaimsPrincipal principal, CancellationToken ct)
    {
        var validLob = await db.LinesOfBusiness.AsNoTracking().AnyAsync(x => x.Id == lineOfBusinessId && x.LOB_Description == SampleDataCatalog.LobKey && x.IsActive, ct); if (!validLob) return Array.Empty<Guid>();
        var claims = principal.Claims.Where(x => x.Type == "branch").Select(x => Guid.TryParse(x.Value, out var id) ? id : Guid.Empty).Where(x => x != Guid.Empty).ToHashSet();
        var ids = SampleDataCatalog.Branches.Select(x => SampleDataCatalog.StableId("Location", x.Code)).ToArray();
        return await db.Locations.AsNoTracking().Where(x => ids.Contains(x.Id) && x.Level == 5 && x.IsActive && claims.Contains(x.Id)).OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(ct);
    }

    public async Task<bool> IsActiveBranchAsync(Guid branchId, Guid lineOfBusinessId, CancellationToken ct)
    {
        var sampleBranch = SampleDataCatalog.Branches.Select(b => SampleDataCatalog.StableId("Location", b.Code)).Contains(branchId);
        var branchActive = sampleBranch && await db.Locations.AsNoTracking().AnyAsync(x => x.Id == branchId && x.Level == 5 && x.IsActive, ct);
        return branchActive && await db.LinesOfBusiness.AsNoTracking().AnyAsync(x => x.Id == lineOfBusinessId && x.LOB_Description == SampleDataCatalog.LobKey && x.IsActive, ct);
    }
}

public sealed class SampleFinalizedSchedulePort(FinnovaDbContext db) : IFinalizedSchedulePort
{
    public async Task<ScheduleProjection?> GetAsync(Guid accountId, DemandPeriod period, CancellationToken ct)
    {
        var account = await db.LoanAccounts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == accountId && x.Status == AccountStatus.Active && x.ApplicationId.HasValue, ct); if (account?.ApplicationId is null) return null;
        var schedule = await db.ApplicationSchedules.AsNoTracking().SingleOrDefaultAsync(x => x.ApplicationId == account.ApplicationId && x.IsFinalized, ct); if (schedule is null) return null;
        var entry = await db.ApplicationInstallments.AsNoTracking().SingleOrDefaultAsync(x => x.ScheduleId == schedule.Id && x.DueDate == new DateOnly(period.Year, period.Month, 15), ct); if (entry is null) return null;
        return new ScheduleProjection(accountId, schedule.Id, entry.Id, $"{SampleDataCatalog.DatasetKey}|SCHEDULE|{account.ApplicationId:N}|{period.CanonicalValue}", entry.PrincipalPortion, entry.InterestPortion);
    }
}

public sealed class SampleAllocationEventPort(FinnovaDbContext db, ILogger<SampleAllocationEventPort> logger) : IAllocationEventPort
{
    public async Task<IReadOnlyList<AllocationEvent>> GetAsync(Guid accountId, DemandPeriod period, string snapshotReference, CancellationToken ct)
    {
        var loan = await db.LoanAccounts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == accountId && x.Status == AccountStatus.Active, ct); if (loan is null) return Array.Empty<AllocationEvent>();
        var generic = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(x => x.AccountNumber == loan.AccountNumber && x.Type == AccountType.Loan && x.Status == AccountStatus.Active, ct); if (generic is null) return Array.Empty<AllocationEvent>();
        var cutoff = SampleDataCatalog.PeriodCutoffUtc(new SamplePeriod(period.Year, period.Month)); var rows = await db.Transactions.AsNoTracking().Where(x => x.AccountId == generic.Id && x.TransactionDate <= cutoff).OrderBy(x => x.TransactionDate).ThenBy(x => x.Id).ToListAsync(ct); var events = new List<AllocationEvent>();
        foreach (var row in rows) { var parsed = SampleReceiptParser.Parse(generic.Id, loan.AccountNumber, row, new DateOnly(period.Year, period.Month, 1), cutoff); if (parsed.Event is not null) events.Add(parsed.Event); else logger.LogWarning("Demand sample receipt rejected {Reference} for {ReasonCode}.", parsed.SafeReference, parsed.ReasonCode); }
        return events;
    }
}

public sealed class SamplePreviousDemandPort(FinnovaDbContext db, SampleOwnershipStore ownership) : IPreviousDemandPort
{
    public async Task<PreviousDemandResult?> GetAsync(Guid accountId, DemandScope scope, DemandPeriod targetPeriod, string snapshotReference, CancellationToken ct)
    {
        if (snapshotReference != SampleDataCatalog.SnapshotReference) return null;
        var previous = targetPeriod.Month == 1 ? new SamplePeriod(targetPeriod.Year - 1, 12) : new SamplePeriod(targetPeriod.Year, targetPeriod.Month - 1); var expectedBranches = SampleDataCatalog.Branches.Select(x => SampleDataCatalog.StableId("Location", x.Code)).OrderBy(x => x).ToArray(); if (!scope.BranchIds.OrderBy(x => x).SequenceEqual(expectedBranches)) return null;
        var runId = SampleDataCatalog.StableId("DemandRun", previous.Key); var run = await db.DemandRuns.AsNoTracking().SingleOrDefaultAsync(x => x.Id == runId && x.LineOfBusinessId == scope.LineOfBusinessId && x.Frequency == SampleDataCatalog.Frequency && x.PeriodYear == previous.Year && x.PeriodMonth == previous.Month && x.ProcessingCutoffReference == snapshotReference && x.CurrentRevisionNumber == 1, ct); if (run is null || !run.BillingSnapshotId.HasValue || !run.DemandSnapshotId.HasValue) return null;
        var revision = await db.DemandRunRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.DemandRunId == run.Id && x.Id == SampleDataCatalog.StableId("DemandRevision", previous.Key) && x.RevisionNumber == 1, ct); if (revision is null || revision.BillingSnapshotId != run.BillingSnapshotId || revision.DemandSnapshotId != run.DemandSnapshotId) return null;
        var stage = await db.BillingStageRuns.AsNoTracking().SingleOrDefaultAsync(x => x.DemandRunId == run.Id && x.RevisionNumber == 1 && x.BillingSnapshotId == run.BillingSnapshotId && x.Status == BillingStageStatus.Succeeded, ct); if (stage is null) return null;
        var billing = await db.BillingSnapshots.AsNoTracking().SingleOrDefaultAsync(x => x.Id == run.BillingSnapshotId.Value && x.BillingStageRunId == stage.Id && x.RevisionNumber == 1 && x.IsPublished && x.ProcessingCutoffReference == snapshotReference, ct); if (billing is null) return null;
        var demand = await db.DemandSnapshots.AsNoTracking().SingleOrDefaultAsync(x => x.Id == run.DemandSnapshotId.Value && x.DemandRunId == run.Id && x.BillingSnapshotId == billing.Id && x.RevisionNumber == 1 && x.IsPublished, ct); if (demand is null) return null;
        var loan = SampleDataCatalog.Loans.SingleOrDefault(x => SampleDataCatalog.StableId("LoanAccount", x.AccountNumber) == accountId); if (loan is null) return null; var rowId = SampleDataCatalog.StableId("DemandRow", $"{previous.Key}:{loan.Key}"); var row = await db.DemandRows.AsNoTracking().SingleOrDefaultAsync(x => x.Id == rowId && x.DemandSnapshotId == demand.Id && x.DemandRunRevisionId == revision.Id && x.AccountId == accountId && x.LineOfBusinessId == scope.LineOfBusinessId && x.Status == DemandRowStatus.Completed, ct); if (row is null || !scope.BranchIds.Contains(row.BranchId)) return null;
        try { await ownership.RequireAsync("DemandRow", $"{previous.Key}:{loan.Key}", row.Id, ct); } catch (SampleDataCollisionException) { return null; }
        return new PreviousDemandResult(row.Arrears, $"{SampleDataCatalog.DatasetKey}|ROW|{previous.Key}|{accountId:N}");
    }
}