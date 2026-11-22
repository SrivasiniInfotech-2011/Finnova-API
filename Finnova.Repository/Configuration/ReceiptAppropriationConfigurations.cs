using Finnova.Models.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finnova.Repository.Configuration;

public sealed class ReceiptAppropriationConfiguration : IEntityTypeConfiguration<ReceiptAppropriation>
{
    public void Configure(EntityTypeBuilder<ReceiptAppropriation> b)
    {
        b.ToTable("receipt_appropriations"); b.HasKey(x => x.Id);
        b.Property(x => x.Mode).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        foreach (var p in new[] { nameof(ReceiptAppropriation.ReceiptAmount), nameof(ReceiptAppropriation.DueAllocationTotal), nameof(ReceiptAppropriation.AdvanceAccountAmount), nameof(ReceiptAppropriation.ResidualAmount) }) b.Property<decimal>(p).HasPrecision(18, 2);
        b.Property(x => x.RuleDirection).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        b.HasIndex(x => x.ReceiptId).IsUnique(); b.HasIndex(x => x.ClientRequestId).IsUnique();
        b.HasMany(x => x.Allocations).WithOne(x => x.ReceiptAppropriation).HasForeignKey(x => x.ReceiptAppropriationId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.AdvanceAccountAllocation).WithOne(x => x.ReceiptAppropriation).HasForeignKey<AdvanceAccountAllocation>(x => x.ReceiptAppropriationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ReceiptAllocationConfiguration : IEntityTypeConfiguration<ReceiptAllocation>
{
    public void Configure(EntityTypeBuilder<ReceiptAllocation> b)
    {
        b.ToTable("receipt_allocations"); b.HasKey(x => x.Id); b.HasIndex(x => new { x.ReceiptAppropriationId, x.PendingDueId }).IsUnique();
        b.Property(x => x.CashFlowDueType).HasConversion<string>().HasMaxLength(30).IsRequired(); b.Property(x => x.AccountNumber).HasMaxLength(50).IsRequired(); b.Property(x => x.DueRowVersionAtConfirm).HasMaxLength(200).IsRequired();
        b.Property(x => x.AllocatedAmount).HasPrecision(18, 2);
        b.Property(x => x.OutstandingAmountAtConfirm).HasPrecision(18, 2);
        b.Property(x => x.TargetAmount).HasPrecision(18, 2);
    }
}

public sealed class AdvanceAccountAllocationConfiguration : IEntityTypeConfiguration<AdvanceAccountAllocation>
{
    public void Configure(EntityTypeBuilder<AdvanceAccountAllocation> b)
    {
        b.ToTable("advance_account_allocations"); b.HasKey(x => x.Id); b.HasIndex(x => x.ReceiptAppropriationId).IsUnique(); b.Property(x => x.Currency).HasMaxLength(3).IsRequired(); b.Property(x => x.Amount).HasPrecision(18, 2);
    }
}

public sealed class AppropriationAuditEntryConfiguration : IEntityTypeConfiguration<AppropriationAuditEntry>
{
    public void Configure(EntityTypeBuilder<AppropriationAuditEntry> b)
    {
        b.ToTable("appropriation_audit_entries"); b.HasKey(x => x.Id); b.HasIndex(x => x.ReceiptAppropriationId).IsUnique(); b.HasIndex(x => x.ClientRequestId).IsUnique();
        b.Property(x => x.Mode).HasMaxLength(20).IsRequired(); b.Property(x => x.Actor).HasMaxLength(200).IsRequired(); b.Property(x => x.CorrelationId).HasMaxLength(100).IsRequired();
        foreach (var p in new[] { nameof(AppropriationAuditEntry.DueAllocationTotal), nameof(AppropriationAuditEntry.AdvanceAccountAmount), nameof(AppropriationAuditEntry.ResidualAmount) }) b.Property<decimal>(p).HasPrecision(18, 2);
    }
}