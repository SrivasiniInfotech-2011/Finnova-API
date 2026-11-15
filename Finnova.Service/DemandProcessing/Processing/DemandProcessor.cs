using System.Security.Cryptography;
using System.Text;
using Finnova.Models.Contracts.DemandProcessing;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Models.Domain.ValueObjects;
using Finnova.Repository.Interfaces;
using Finnova.Service.DemandProcessing.Billing;
using Finnova.Service.DemandProcessing.Ports;
using Finnova.Service.DemandProcessing.Rules;

namespace Finnova.Service.DemandProcessing.Processing;

public sealed class DemandProcessor(IDemandRepository repository, IAccountSourcePort accounts, IFinalizedSchedulePort schedules, IAllocationEventPort events, IAllocationPolicy allocation, IChargePolicyAdapter charges, IJournalPort journal, IDemandLeaseStore lease, IClock clock) : IDemandProcessor
{
    public async Task ProcessAsync(Guid runId, CancellationToken ct)
    {
        var run = await repository.GetRunAsync(runId, ct) ?? throw new DemandNotFoundException(runId); var revision = run.Revisions.Single(x => x.RevisionNumber == run.CurrentRevisionNumber); if (!await lease.TryAcquireAsync(run.LogicalScopeKey, run.Id, ct)) throw new DemandConcurrencyException();
        try
        {
            run.Status = DemandRunStatus.BillingPending; run.BillingStatus = BillingStageStatus.Queued; run.StartedAtUtc = clock.UtcNow; await repository.SaveAsync(ct);
            var scope = DemandScope.Create(run.LineOfBusinessId, run.BranchScopes.OrderBy(x => x.SortOrder).Select(x => x.BranchId)); var period = DemandPeriod.Create(run.PeriodYear, run.PeriodMonth); var snapshotReference = run.ProcessingCutoffReference ?? $"run:{run.Id:N}:revision:{revision.RevisionNumber}";
            var stage = new BillingStageRun { DemandRunId = run.Id, RevisionNumber = revision.RevisionNumber, LogicalBillingScopeKey = run.LogicalScopeKey, Status = BillingStageStatus.Running, ProcessingCutoffReference = snapshotReference, AllocationPolicyReference = "ConfiguredAllocationPolicy", ChargePolicyReference = "ConfiguredChargePolicy", CreatedAtUtc = clock.UtcNow, StartedAtUtc = clock.UtcNow, CorrelationId = run.CorrelationId, DemandRun = run }; await repository.AddBillingStageAsync(stage, ct); run.BillingStageRunId = stage.Id; run.Status = DemandRunStatus.BillingRunning; run.BillingStatus = BillingStageStatus.Running; await repository.SaveAsync(ct);
            var results = new List<BillingPeriodResult>();
            await foreach (var account in accounts.ReadAsync(scope, snapshotReference, ct))
            {
                var eligibility = DemandRules.Check(account, scope); if (!eligibility.Eligible) { await repository.AddFailureAsync(new DemandFailure { DemandRunId = run.Id, AccountId = account.AccountId, FailureClass = DemandFailureClass.DataQuality, Code = eligibility.Code!, Message = eligibility.Message!, Scope = run.LogicalScopeKey, CorrelationId = run.CorrelationId, CreatedAtUtc = clock.UtcNow }, ct); continue; }
                var schedule = await schedules.GetAsync(account.AccountId, period, ct) ?? throw new DemandScheduleIncompleteException(); var installment = BillingRules.ScheduledInstallment(schedule); var sourceEvents = await events.GetAsync(account.AccountId, period, snapshotReference, ct); var allocationResult = allocation.Apply(installment, 0m, sourceEvents); var chargeInput = new ChargeInput(0m, 0m, 0, null, installment, sourceEvents, sourceEvents); var chargeResult = await charges.CalculateAsync(chargeInput, ct); var result = new BillingPeriodResult { BillingStageRunId = stage.Id, AccountId = account.AccountId, AccountDisplay = account.AccountDisplay, CustomerDisplay = account.CustomerDisplay, CustomerId = account.CustomerId, LineOfBusinessId = account.LineOfBusinessId, BranchId = account.BranchId, AccountStatusAtCutoff = account.Status, IsEligible = true, Status = BillingPeriodResultStatus.Succeeded, ScheduleId = schedule.ScheduleId, ScheduleEntryId = schedule.ScheduleEntryId, ScheduleReference = schedule.ScheduleReference, ScheduledPrincipal = BillingRules.Money(schedule.ScheduledPrincipal), ScheduledInterestOrEmi = BillingRules.Money(schedule.ScheduledInterestOrEmi), ScheduledInstallment = installment, CurrentDue = allocationResult.CurrentDue, Arrears = allocationResult.Arrears, Odi = chargeResult.Odi, OtherCharges = chargeResult.OtherCharges, AllocationPolicyReference = stage.AllocationPolicyReference, ChargePolicyReference = chargeResult.PolicyReference, ProcessingCutoffReference = snapshotReference, CreatedAtUtc = clock.UtcNow }; results.Add(result); await repository.AddBillingResultAsync(result, ct); foreach (var evidence in allocationResult.Evidence) await repository.AddAllocationEvidenceAsync(new BillingAllocationEvidence { BillingStageRunId = stage.Id, BillingPeriodResultId = result.Id, EventType = evidence.Event.EventType, SourceEventId = evidence.Event.Id, SourceEventVersion = evidence.Event.SourceVersion, EventEffectiveUtc = evidence.Event.EffectiveUtc, AllocationPolicyReference = stage.AllocationPolicyReference, AllocatedAmount = BillingRules.Money(evidence.AllocatedAmount), TargetComponent = evidence.TargetComponent, Outcome = AllocationOutcome.Applied, SafeReason = "Applied by configured allocation policy.", ApplicationKey = evidence.ApplicationKey }, ct);
            }
            stage.EligibleAccountCount = results.Count; stage.SuccessfulResultCount = results.Count; var journalResult = await journal.EnsurePostedAsync(new JournalRequest(stage.LogicalBillingScopeKey, stage.Id, BillingRules.Money(results.Sum(x => x.CurrentDue)), BillingRules.Money(results.Sum(x => x.Arrears)), BillingRules.Money(results.Sum(x => x.Odi)), BillingRules.Money(results.Sum(x => x.OtherCharges)), "INR"), ct); if (!BillingRules.JournalChecksPass(journalResult)) throw new DemandJournalIncompleteException(); stage.JournalBatchReference = journalResult.JournalReference; stage.JournalCheckSummary = journalResult.SafeSummary;
            var version = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{run.LogicalScopeKey}|{revision.RevisionNumber}|{results.Count}"))); var billingSnapshot = new BillingSnapshot { BillingStageRunId = stage.Id, RevisionNumber = revision.RevisionNumber, Version = version, LogicalScopeKey = run.LogicalScopeKey, ProcessingCutoffReference = snapshotReference, ResultCount = results.Count, CurrentDueTotal = BillingRules.Money(results.Sum(x => x.CurrentDue)), ArrearsTotal = BillingRules.Money(results.Sum(x => x.Arrears)), OdiTotal = BillingRules.Money(results.Sum(x => x.Odi)), OtherChargesTotal = BillingRules.Money(results.Sum(x => x.OtherCharges)), JournalBatchReference = journalResult.JournalReference, JournalCheckSummary = journalResult.SafeSummary, IsPublished = true, ContentFingerprint = version, ActorId = run.ActorId, Source = run.Source.ToString(), CorrelationId = run.CorrelationId, PublishedAtUtc = clock.UtcNow }; await repository.AddBillingSnapshotAsync(billingSnapshot, ct); stage.BillingSnapshotId = billingSnapshot.Id; stage.BillingSnapshotVersion = version; stage.Status = BillingStageStatus.Succeeded; stage.CompletedAtUtc = clock.UtcNow; run.BillingSnapshotId = billingSnapshot.Id; run.Status = DemandRunStatus.BillingSucceeded; run.BillingStatus = BillingStageStatus.Succeeded; await repository.SaveAsync(ct);
            run.Status = DemandRunStatus.DemandGenerating; await repository.SaveAsync(ct); await GenerateDemandAsync(run, revision, billingSnapshot, results, ct);
        }
        catch (DemandProcessingException) { run.Status = DemandRunStatus.BillingIncomplete; run.BillingStatus = BillingStageStatus.Incomplete; run.FailedAtUtc = clock.UtcNow; await repository.SaveAsync(ct); throw; }
        finally { await lease.ReleaseAsync(run.Id, ct); }
    }

    private async Task GenerateDemandAsync(DemandRun run, DemandRunRevision revision, BillingSnapshot snapshot, IReadOnlyList<BillingPeriodResult> results, CancellationToken ct)
    {
        if (!snapshot.IsPublished) throw new DemandBillingIncompleteException("Demand Generation requires a Published Billing Snapshot.");
        var demandSnapshot = new DemandSnapshot { DemandRunId = run.Id, RevisionNumber = revision.RevisionNumber, BillingSnapshotId = snapshot.Id, Version = $"demand:{snapshot.Version}", IsPublished = false };
        var rows = new List<DemandRow>();
        foreach (var result in results.OrderBy(x => x.AccountId))
        {
            if (result.Status != BillingPeriodResultStatus.Succeeded) continue;
            var totalCharges = BillingRules.Money(result.Odi + result.OtherCharges); var totalDemand = BillingRules.Money(result.CurrentDue + result.Arrears + totalCharges);
            rows.Add(new DemandRow { DemandRunRevisionId = revision.Id, DemandSnapshotId = demandSnapshot.Id, BillingPeriodResultId = result.Id, AccountId = result.AccountId, CustomerId = result.CustomerId, LineOfBusinessId = result.LineOfBusinessId, BranchId = result.BranchId, CurrentDue = BillingRules.Money(result.CurrentDue), Arrears = BillingRules.Money(result.Arrears), Odi = BillingRules.Money(result.Odi), OtherCharges = BillingRules.Money(result.OtherCharges), TotalCharges = totalCharges, TotalDemand = totalDemand, IsZeroDemand = totalDemand == 0m, ScheduleReference = result.ScheduleReference, ProcessingCutoffReference = result.ProcessingCutoffReference, OrderingKey = result.AccountId.ToString("N") });
        }
        var batches = new List<DemandBatch>();
        foreach (var branch in run.BranchScopes.OrderBy(x => x.SortOrder).Select(x => x.BranchId))
        {
            var batch = DemandRules.BuildBatch(revision, demandSnapshot, branch, rows); batches.Add(batch);
            foreach (var row in rows.Where(x => x.BranchId == branch)) { row.DemandBatchId = batch.Id; batch.Rows.Add(row); }
            try { DemandRules.Reconcile(batch, batch.Rows); }
            catch (Exception error) { batch.Status = DemandBatchStatus.Failed; batch.FailureClass = DemandFailureClass.Reconciliation; batch.FailureCode = "ERR-DEMAND-RECONCILIATION"; batch.FailureReason = error.Message; await repository.AddFailureAsync(new DemandFailure { DemandRunId = run.Id, DemandBatchId = batch.Id, FailureClass = DemandFailureClass.Reconciliation, Code = batch.FailureCode, Message = error.Message, Scope = run.LogicalScopeKey, CorrelationId = run.CorrelationId, CreatedAtUtc = clock.UtcNow }, ct); }
        }
        var failed = batches.Any(x => x.Status == DemandBatchStatus.Failed); var successful = batches.Any(x => x.Status is DemandBatchStatus.ReadyToSpool or DemandBatchStatus.NoDemand); demandSnapshot.RowCount = rows.Count; demandSnapshot.BatchCount = batches.Count; demandSnapshot.TotalDemand = BillingRules.Money(rows.Sum(x => x.TotalDemand)); demandSnapshot.IsPublished = successful && !failed; demandSnapshot.PublishedAtUtc = demandSnapshot.IsPublished ? clock.UtcNow : null;
        await repository.AddSnapshotAsync(demandSnapshot, ct); await repository.AddRowsAsync(rows, ct); await repository.AddBatchesAsync(batches, ct);
        revision.DemandSnapshotId = demandSnapshot.Id; revision.RowCount = rows.Count; revision.BatchCount = batches.Count; revision.TotalDemand = demandSnapshot.TotalDemand; revision.TotalCharges = BillingRules.Money(rows.Sum(x => x.TotalCharges)); revision.Status = failed && successful ? DemandRunStatus.PartiallyFailed : failed ? DemandRunStatus.Failed : DemandRunStatus.Completed; revision.CompletedAtUtc = clock.UtcNow; run.DemandSnapshotId = demandSnapshot.Id; run.RowCount = rows.Count; run.BatchCount = batches.Count; run.TotalDemand = demandSnapshot.TotalDemand; run.TotalCharges = revision.TotalCharges; run.Status = revision.Status; run.CompletedAtUtc = clock.UtcNow;
        await repository.AddAuditAsync(new DemandAuditEntry { DemandRunId = run.Id, RevisionNumber = revision.RevisionNumber, Scope = run.LogicalScopeKey, ActorId = run.ActorId, Source = run.Source, CorrelationId = run.CorrelationId, NewStatus = run.Status.ToString(), OccurredAtUtc = clock.UtcNow, OutcomeCode = run.Status == DemandRunStatus.PartiallyFailed ? "DEMAND_PARTIALLY_FAILED" : run.Status == DemandRunStatus.Failed ? "DEMAND_FAILED" : "DEMAND_COMPLETED", SafeReferences = $"billingSnapshot={snapshot.Id:N};demandSnapshot={demandSnapshot.Id:N}" }, ct);
        await repository.SaveAsync(ct);
    }
}
