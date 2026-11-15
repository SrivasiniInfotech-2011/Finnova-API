using System.Security.Cryptography;
using System.Text;
using Finnova.Models.Contracts.DemandProcessing;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.ValueObjects;

namespace Finnova.Service.DemandProcessing.Rules;

public sealed record CalculatedDemand(decimal CurrentDue, decimal Arrears, decimal Odi, decimal OtherCharges, decimal TotalCharges, decimal TotalDemand, bool IsZeroDemand);
public sealed record EligibilityResult(bool Eligible, string? Code, string? Message);

public static class DemandRules
{
    public static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    public static DemandPeriod NormalizePeriod(int year, int month, string timezone)
    {
        if (!string.Equals(timezone, "Asia/Kolkata", StringComparison.Ordinal)) throw new ArgumentException("Demand periods use Asia/Kolkata.", nameof(timezone));
        return DemandPeriod.Create(year, month);
    }

    public static EligibilityResult Check(AccountProjection account, DemandScope scope)
    {
        if (account.AccountId == Guid.Empty || account.CustomerId == Guid.Empty || !account.CustomerResolvable || !account.BranchResolvable || !account.LobResolvable) return new(false, "ERR-DEMAND-DATA-QUALITY", "The account has an unresolved source reference.");
        if (!string.Equals(account.Status, "Active", StringComparison.Ordinal)) return new(false, "ERR-DEMAND-ACCOUNT-INELIGIBLE", "The account is not Active at the run snapshot.");
        if (account.LineOfBusinessId != scope.LineOfBusinessId || !scope.BranchIds.Contains(account.BranchId)) return new(false, "ERR-DEMAND-SCOPE", "The account is outside the requested scope.");
        return new(true, null, null);
    }

    public static CalculatedDemand Calculate(decimal currentDue, decimal arrears, ChargeResult charges)
    {
        if (currentDue < 0m || arrears < 0m || charges.Odi < 0m || charges.OtherCharges < 0m) throw new InvalidOperationException("Demand components cannot be negative.");
        var current = Money(currentDue); var prior = Money(arrears); var odi = Money(charges.Odi); var other = Money(charges.OtherCharges); var totalCharges = Money(odi + other); var total = Money(current + prior + totalCharges);
        return new CalculatedDemand(current, prior, odi, other, totalCharges, total, total == 0m);
    }

    public static string Fingerprint(DemandLogicalKey key, string action, string? idempotencyKey)
    {
        var raw = $"{key.CanonicalValue}|{action}|{idempotencyKey?.Trim() ?? string.Empty}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }

    public static DemandBatch BuildBatch(DemandRunRevision revision, DemandSnapshot snapshot, Guid branchId, IEnumerable<DemandRow> sourceRows)
    {
        var rows = sourceRows.Where(x => x.BranchId == branchId).OrderBy(x => x.OrderingKey).ThenBy(x => x.AccountId).ToList();
        var batch = new DemandBatch { DemandRunRevisionId = revision.Id, DemandSnapshotId = snapshot.Id, DemandRunId = revision.DemandRunId, RevisionNumber = revision.RevisionNumber, BranchId = branchId, LineOfBusinessId = revision.DemandRun.LineOfBusinessId, Frequency = revision.DemandRun.Frequency, PeriodYear = revision.DemandRun.PeriodYear, PeriodMonth = revision.DemandRun.PeriodMonth, Status = rows.Count == 0 ? DemandBatchStatus.NoDemand : DemandBatchStatus.ReadyToSpool, RowCount = rows.Count, SpoolIdempotencyKey = $"{revision.DemandRunId:N}:{revision.RevisionNumber}:{branchId:N}" };
        batch.CurrentDueTotal = Money(rows.Sum(x => x.CurrentDue)); batch.ArrearsTotal = Money(rows.Sum(x => x.Arrears)); batch.OdiTotal = Money(rows.Sum(x => x.Odi)); batch.OtherChargesTotal = Money(rows.Sum(x => x.OtherCharges)); batch.TotalCharges = Money(rows.Sum(x => x.TotalCharges)); batch.TotalDemand = Money(rows.Sum(x => x.TotalDemand));
        return batch;
    }

    public static void Reconcile(DemandBatch batch, IEnumerable<DemandRow> sourceRows)
    {
        var rows = sourceRows.Where(x => x.DemandBatchId == batch.Id).ToList();
        if (rows.Any(x => x.BranchId != batch.BranchId || x.LineOfBusinessId != batch.LineOfBusinessId) || batch.RowCount != rows.Count || batch.CurrentDueTotal != Money(rows.Sum(x => x.CurrentDue)) || batch.ArrearsTotal != Money(rows.Sum(x => x.Arrears)) || batch.OdiTotal != Money(rows.Sum(x => x.Odi)) || batch.OtherChargesTotal != Money(rows.Sum(x => x.OtherCharges)) || batch.TotalCharges != Money(rows.Sum(x => x.TotalCharges)) || batch.TotalDemand != Money(rows.Sum(x => x.TotalDemand))) throw new InvalidOperationException("Demand Batch totals or dimensions do not reconcile.");
    }
}
