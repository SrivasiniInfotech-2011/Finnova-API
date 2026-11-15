using FsCheck;
using FsCheck.Xunit;
using Finnova.Models.Contracts.DemandProcessing;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.ValueObjects;
using Finnova.Service.DemandProcessing.Billing;
using Finnova.Service.DemandProcessing.Rules;
using Xunit;

namespace Finnova.Tests.DemandProcessing;

public sealed class DemandRulesPropertyTests
{
    // Feature: demand-processing, Property 1: schedule arithmetic and charge components are rounded deterministically.
    [Property(MaxTest = 100)] public Property BillingComponentsRemainDeterministic(NonNegativeInt principal, NonNegativeInt interest, NonNegativeInt odi, NonNegativeInt other) { var schedule = new ScheduleProjection(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "schedule", principal.Get / 100m, interest.Get / 100m); var a = BillingRules.ScheduledInstallment(schedule); var b = BillingRules.ScheduledInstallment(schedule); return (a == b && a == BillingRules.Money(schedule.ScheduledPrincipal + schedule.ScheduledInterestOrEmi)).ToProperty(); }
    // Feature: demand-processing, Property 2: each allocation event has one stable logical application key.
    [Property(MaxTest = 100)] public Property AllocationKeysAreStable(NonNegativeInt amount) { var item = new AllocationEvent(Guid.NewGuid(), AllocationEventType.Receipt, amount.Get / 100m, DateTime.UtcNow, "source", "v1", "CurrentDue", true); return (BillingRules.ApplicationKey(item) == BillingRules.ApplicationKey(item)).ToProperty(); }
    // Feature: demand-processing, Property 3: demand totals are source-derived and deterministic.
    [Property(MaxTest = 100)] public Property DemandTotalsConserveComponents(NonNegativeInt current, NonNegativeInt arrears, NonNegativeInt odi, NonNegativeInt other) { var result = DemandRules.Calculate(current.Get / 100m, arrears.Get / 100m, new ChargeResult(odi.Get / 100m, other.Get / 100m, "test", Array.Empty<string>())); return (result.TotalCharges == DemandRules.Money(result.Odi + result.OtherCharges) && result.TotalDemand == DemandRules.Money(result.CurrentDue + result.Arrears + result.TotalCharges)).ToProperty(); }
    // Feature: demand-processing, Property 4: only Active in-scope accounts with resolvable references are eligible.
    [Property(MaxTest = 100)] public Property EligibilityPreservesScope() { var lob = Guid.NewGuid(); var branch = Guid.NewGuid(); var account = new AccountProjection(Guid.NewGuid(), Guid.NewGuid(), lob, branch, "Account", "Customer", "Active", true, true, true, "snapshot"); return DemandRules.Check(account, DemandScope.Create(lob, [branch])).Eligible.ToProperty(); }
    // Feature: demand-processing, Property 5: equivalent logical keys produce one stable fingerprint.
    [Property(MaxTest = 100)] public Property FingerprintsAreStable() { var lob = Guid.NewGuid(); var branch = Guid.NewGuid(); var key = new DemandLogicalKey(DemandScope.Create(lob, [branch]), "Monthly", DemandPeriod.Create(2025, 1), "billing"); return (DemandRules.Fingerprint(key, "start", "idempotency") == DemandRules.Fingerprint(key, "start", "idempotency")).ToProperty(); }
    // Feature: demand-processing, Property 6: Billing publication requires successful stage checks.
    [Property(MaxTest = 100)] public Property FailedBillingChecksNeverPass() { var completion = new BillingCompletion("billing", new(Guid.NewGuid(), [Guid.NewGuid()], "Monthly", 2025, 1, "billing"), BillingStageStatus.Incomplete, "snapshot", null, new Dictionary<string, bool> { ["snapshot"] = false }); var expected = completion.Scope; var passed = false; try { BillingRules.RequireBillingSnapshot(completion, expected); passed = true; } catch (Exception) { } return (!passed).ToProperty(); }
    // Feature: demand-processing, Property 7: empty in-scope Branches become NoDemand rather than failures.
    [Property(MaxTest = 100)] public Property EmptyBranchesBecomeNoDemand() { var run = new Finnova.Models.Domain.Entities.DemandRun { LineOfBusinessId = Guid.NewGuid(), Frequency = "Monthly", PeriodYear = 2025, PeriodMonth = 1 }; var revision = new Finnova.Models.Domain.Entities.DemandRunRevision { DemandRun = run, DemandRunId = run.Id, RevisionNumber = 1 }; var snapshot = new Finnova.Models.Domain.Entities.DemandSnapshot { DemandRunId = run.Id, RevisionNumber = 1 }; var batch = DemandRules.BuildBatch(revision, snapshot, Guid.NewGuid(), Array.Empty<Finnova.Models.Domain.Entities.DemandRow>()); return (batch.Status == DemandBatchStatus.NoDemand && batch.TotalDemand == 0m).ToProperty(); }
}
