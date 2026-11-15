using Finnova.Models.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finnova.Repository.Configuration;

public sealed class DemandRunConfiguration : IEntityTypeConfiguration<DemandRun>
{
    public void Configure(EntityTypeBuilder<DemandRun> b)
    {
        b.ToTable("demand_runs"); b.HasKey(x => x.Id);
        b.Property(x => x.Frequency).HasMaxLength(40).IsRequired();
        b.Property(x => x.BusinessTimezone).HasMaxLength(64).IsRequired();
        b.Property(x => x.LogicalScopeKey).HasMaxLength(512).IsRequired();
        b.Property(x => x.PayloadFingerprint).HasMaxLength(128).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
        b.Property(x => x.BillingStatus).HasConversion<string>().HasMaxLength(40).IsRequired();
        b.Property(x => x.Source).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.ActorId).HasMaxLength(200).IsRequired();
        b.Property(x => x.CorrelationId).HasMaxLength(100).IsRequired();
        foreach (var p in new[] { nameof(DemandRun.CurrentDueTotal), nameof(DemandRun.ArrearsTotal), nameof(DemandRun.OdiTotal), nameof(DemandRun.OtherChargesTotal), nameof(DemandRun.TotalCharges), nameof(DemandRun.TotalDemand) }) b.Property<decimal>(p).HasPrecision(19, 2);
        b.Property(x => x.Version).IsRowVersion().IsConcurrencyToken();
        b.HasIndex(x => new { x.LogicalScopeKey, x.CurrentRevisionNumber }).IsUnique();
        b.HasIndex(x => new { x.PeriodYear, x.PeriodMonth, x.LineOfBusinessId, x.Status });
        b.HasMany(x => x.BranchScopes).WithOne(x => x.DemandRun).HasForeignKey(x => x.DemandRunId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Revisions).WithOne(x => x.DemandRun).HasForeignKey(x => x.DemandRunId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DemandRunBranchScopeConfiguration : IEntityTypeConfiguration<DemandRunBranchScope>
{
    public void Configure(EntityTypeBuilder<DemandRunBranchScope> b) { b.ToTable("demand_run_branch_scopes"); b.HasKey(x => x.Id); b.HasIndex(x => new { x.DemandRunId, x.RevisionNumber, x.BranchId }).IsUnique(); }
}

public sealed class DemandRunRevisionConfiguration : IEntityTypeConfiguration<DemandRunRevision>
{
    public void Configure(EntityTypeBuilder<DemandRunRevision> b)
    {
        b.ToTable("demand_run_revisions"); b.HasKey(x => x.Id); b.Property(x => x.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
        foreach (var p in new[] { nameof(DemandRunRevision.CurrentDueTotal), nameof(DemandRunRevision.ArrearsTotal), nameof(DemandRunRevision.OdiTotal), nameof(DemandRunRevision.OtherChargesTotal), nameof(DemandRunRevision.TotalCharges), nameof(DemandRunRevision.TotalDemand) }) b.Property<decimal>(p).HasPrecision(19, 2);
        b.Property(x => x.Version).IsRowVersion().IsConcurrencyToken(); b.HasIndex(x => new { x.DemandRunId, x.RevisionNumber }).IsUnique();
    }
}

public sealed class BillingStageRunConfiguration : IEntityTypeConfiguration<BillingStageRun>
{
    public void Configure(EntityTypeBuilder<BillingStageRun> b)
    {
        b.ToTable("demand_billing_stage_runs"); b.HasKey(x => x.Id); b.Property(x => x.LogicalBillingScopeKey).HasMaxLength(512).IsRequired(); b.Property(x => x.Status).HasConversion<string>().HasMaxLength(40).IsRequired(); b.Property(x => x.ChargePolicyKind).HasConversion<string>().HasMaxLength(40).IsRequired(); b.Property(x => x.Version).IsRowVersion().IsConcurrencyToken(); b.HasIndex(x => new { x.LogicalBillingScopeKey, x.RevisionNumber }).IsUnique();
    }
}

public sealed class BillingPeriodResultConfiguration : IEntityTypeConfiguration<BillingPeriodResult>
{
    public void Configure(EntityTypeBuilder<BillingPeriodResult> b)
    {
        b.ToTable("demand_billing_period_results"); b.HasKey(x => x.Id); b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        foreach (var p in new[] { nameof(BillingPeriodResult.ScheduledPrincipal), nameof(BillingPeriodResult.ScheduledInterestOrEmi), nameof(BillingPeriodResult.ScheduledInstallment), nameof(BillingPeriodResult.CurrentDue), nameof(BillingPeriodResult.Arrears), nameof(BillingPeriodResult.Odi), nameof(BillingPeriodResult.OtherCharges) }) b.Property<decimal>(p).HasPrecision(19, 2);
        b.HasIndex(x => new { x.BillingStageRunId, x.AccountId }).IsUnique(); b.HasOne(x => x.BillingStageRun).WithMany(x => x.Results).HasForeignKey(x => x.BillingStageRunId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class BillingAllocationEvidenceConfiguration : IEntityTypeConfiguration<BillingAllocationEvidence>
{
    public void Configure(EntityTypeBuilder<BillingAllocationEvidence> b) { b.ToTable("demand_billing_allocation_evidence"); b.HasKey(x => x.Id); b.Property(x => x.EventType).HasConversion<string>().HasMaxLength(30).IsRequired(); b.Property(x => x.Outcome).HasConversion<string>().HasMaxLength(30).IsRequired(); b.Property(x => x.ApplicationKey).HasMaxLength(300).IsRequired(); b.Property(x => x.AllocatedAmount).HasPrecision(19, 2); b.HasIndex(x => x.ApplicationKey).IsUnique(); b.HasOne(x => x.BillingPeriodResult).WithMany().HasForeignKey(x => x.BillingPeriodResultId).OnDelete(DeleteBehavior.Restrict); }
}

public sealed class BillingSnapshotConfiguration : IEntityTypeConfiguration<BillingSnapshot>
{
    public void Configure(EntityTypeBuilder<BillingSnapshot> b) { b.ToTable("demand_billing_snapshots"); b.HasKey(x => x.Id); b.Property(x => x.Version).HasMaxLength(128).IsRequired(); b.Property(x => x.LogicalScopeKey).HasMaxLength(512).IsRequired(); b.Property(x => x.ContentFingerprint).HasMaxLength(128).IsRequired(); foreach (var p in new[] { nameof(BillingSnapshot.CurrentDueTotal), nameof(BillingSnapshot.ArrearsTotal), nameof(BillingSnapshot.OdiTotal), nameof(BillingSnapshot.OtherChargesTotal) }) b.Property<decimal>(p).HasPrecision(19, 2); b.HasIndex(x => new { x.BillingStageRunId, x.RevisionNumber, x.IsPublished }).IsUnique(); }
}

public sealed class DemandSnapshotConfiguration : IEntityTypeConfiguration<DemandSnapshot>
{
    public void Configure(EntityTypeBuilder<DemandSnapshot> b) { b.ToTable("demand_snapshots"); b.HasKey(x => x.Id); b.Property(x => x.Version).HasMaxLength(128).IsRequired(); b.Property(x => x.TotalDemand).HasPrecision(19, 2); b.HasIndex(x => new { x.DemandRunId, x.RevisionNumber, x.IsPublished }).IsUnique(); }
}

public sealed class DemandRowConfiguration : IEntityTypeConfiguration<DemandRow>
{
    public void Configure(EntityTypeBuilder<DemandRow> b)
    {
        b.ToTable("demand_rows"); b.HasKey(x => x.Id); b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired(); b.Property(x => x.AccountDisplay).HasMaxLength(200).IsRequired(); b.Property(x => x.CustomerDisplay).HasMaxLength(200).IsRequired(); b.Property(x => x.OrderingKey).HasMaxLength(200).IsRequired();
        foreach (var p in new[] { nameof(DemandRow.CurrentDue), nameof(DemandRow.Arrears), nameof(DemandRow.Odi), nameof(DemandRow.OtherCharges), nameof(DemandRow.TotalCharges), nameof(DemandRow.TotalDemand) }) b.Property<decimal>(p).HasPrecision(19, 2);
        b.HasIndex(x => new { x.DemandSnapshotId, x.AccountId }).IsUnique(); b.HasIndex(x => new { x.BranchId, x.LineOfBusinessId, x.OrderingKey }); b.HasOne(x => x.Revision).WithMany(x => x.Rows).HasForeignKey(x => x.DemandRunRevisionId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.Batch).WithMany(x => x.Rows).HasForeignKey(x => x.DemandBatchId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DemandBatchConfiguration : IEntityTypeConfiguration<DemandBatch>
{
    public void Configure(EntityTypeBuilder<DemandBatch> b)
    {
        b.ToTable("demand_batches"); b.HasKey(x => x.Id); b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired(); b.Property(x => x.Frequency).HasMaxLength(40).IsRequired(); b.Property(x => x.SpoolIdempotencyKey).HasMaxLength(300).IsRequired(); b.Property(x => x.Version).IsRowVersion().IsConcurrencyToken();
        foreach (var p in new[] { nameof(DemandBatch.CurrentDueTotal), nameof(DemandBatch.ArrearsTotal), nameof(DemandBatch.OdiTotal), nameof(DemandBatch.OtherChargesTotal), nameof(DemandBatch.TotalCharges), nameof(DemandBatch.TotalDemand) }) b.Property<decimal>(p).HasPrecision(19, 2);
        b.HasIndex(x => new { x.DemandSnapshotId, x.BranchId }).IsUnique(); b.HasIndex(x => new { x.PeriodYear, x.PeriodMonth, x.BranchId, x.Status });
    }
}

public sealed class DemandFailureConfiguration : IEntityTypeConfiguration<DemandFailure>
{
    public void Configure(EntityTypeBuilder<DemandFailure> b) { b.ToTable("demand_failures"); b.HasKey(x => x.Id); b.Property(x => x.FailureClass).HasConversion<string>().HasMaxLength(30).IsRequired(); b.Property(x => x.Code).HasMaxLength(100).IsRequired(); b.Property(x => x.Message).HasMaxLength(1000).IsRequired(); b.Property(x => x.Scope).HasMaxLength(512).IsRequired(); b.HasIndex(x => new { x.DemandRunId, x.CreatedAtUtc }); }
}

public sealed class DemandProcessingLeaseConfiguration : IEntityTypeConfiguration<DemandProcessingLease>
{
    public void Configure(EntityTypeBuilder<DemandProcessingLease> b) { b.ToTable("demand_processing_leases"); b.HasKey(x => x.Id); b.Property(x => x.LogicalScopeKey).HasMaxLength(512).IsRequired(); b.Property(x => x.OwnerId).HasMaxLength(200).IsRequired(); b.Property(x => x.Version).IsRowVersion().IsConcurrencyToken(); b.HasIndex(x => x.LogicalScopeKey).IsUnique(); }
}

public sealed class DemandAuditEntryConfiguration : IEntityTypeConfiguration<DemandAuditEntry>
{
    public void Configure(EntityTypeBuilder<DemandAuditEntry> b) { b.ToTable("demand_audit_entries"); b.HasKey(x => x.Id); b.Property(x => x.Scope).HasMaxLength(512).IsRequired(); b.Property(x => x.ActorId).HasMaxLength(200).IsRequired(); b.Property(x => x.Source).HasConversion<string>().HasMaxLength(20).IsRequired(); b.Property(x => x.CorrelationId).HasMaxLength(100).IsRequired(); b.Property(x => x.OutcomeCode).HasMaxLength(100).IsRequired(); b.Property(x => x.SafeReferences).HasMaxLength(2000).IsRequired(); b.HasIndex(x => new { x.DemandRunId, x.OccurredAtUtc }); }
}
