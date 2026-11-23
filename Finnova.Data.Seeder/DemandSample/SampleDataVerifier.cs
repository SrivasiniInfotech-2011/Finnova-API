using Finnova.Repository.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Finnova.Data.Seeder.DemandSample;

/// <summary>Read-only checks. Returns human-readable problems; an empty list means the data is intact.</summary>
public sealed class SampleDataVerifier(FinnovaDbContext db)
{
    public async Task<IReadOnlyList<string>> VerifyAsync(CancellationToken ct = default)
    {
        var problems = new List<string>();
        var manifestCode = $"Manifest:{SampleDataCatalog.DatasetKey}";
        var manifest = await db.LookupValues.AsNoTracking().SingleOrDefaultAsync(x => x.Module == SampleDataCatalog.OwnershipModule && x.LookupType == SampleDataCatalog.OwnershipType && x.Code == manifestCode, ct);
        if (manifest is null) { problems.Add("The manifest is missing. Run the seed command."); return problems; }

        var values = await db.LookupValues.AsNoTracking().Where(x => x.Module == SampleDataCatalog.OwnershipModule && x.LookupType == SampleDataCatalog.OwnershipType && x.Code != manifestCode).OrderBy(x => x.Code).Select(x => x.Code + "=" + x.Value).ToListAsync(ct);
        var parts = manifest.Value.Split('|');
        if (parts.Length != 4 || !string.Equals(parts[3], SampleDataCatalog.Hash(string.Join("\n", values)), StringComparison.Ordinal)) problems.Add("The manifest fingerprint does not match the ownership records.");

        var loanIds = SampleDataCatalog.Loans.Select(x => SampleDataCatalog.StableId("LoanAccount", x.AccountNumber)).ToArray();
        if (await db.LoanAccounts.CountAsync(x => loanIds.Contains(x.Id), ct) != SampleDataCatalog.Loans.Count) problems.Add("One or more sample loan accounts are missing.");

        foreach (var period in SampleDataCatalog.HistoryPeriods)
        {
            var runId = SampleDataCatalog.StableId("DemandRun", period.Key);
            var run = await db.DemandRuns.AsNoTracking().SingleOrDefaultAsync(x => x.Id == runId, ct);
            if (run is null) { problems.Add($"History run {period.Key} is missing."); continue; }
            if (await db.DemandRunBranchScopes.CountAsync(x => x.DemandRunId == runId, ct) != SampleDataCatalog.Branches.Count) problems.Add($"History run {period.Key} does not have all branch scopes.");
            if (await db.DemandBatches.CountAsync(x => x.DemandRunId == runId, ct) != SampleDataCatalog.Branches.Count) problems.Add($"History run {period.Key} does not have all batches.");
            if (await db.DemandRows.CountAsync(x => x.DemandSnapshotId == run.DemandSnapshotId, ct) != SampleDataCatalog.Loans.Count) problems.Add($"History run {period.Key} does not have all demand rows.");
            if (!await db.BillingSnapshots.AnyAsync(x => x.Id == run.BillingSnapshotId && x.IsPublished, ct) || !await db.DemandSnapshots.AnyAsync(x => x.Id == run.DemandSnapshotId && x.IsPublished, ct)) problems.Add($"History run {period.Key} is not published.");
        }
        return problems;
    }
}