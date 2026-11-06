using Finnova.Models.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finnova.Repository.Configuration;

public sealed class ApplicationConfiguration : IEntityTypeConfiguration<Application>
{
    public void Configure(EntityTypeBuilder<Application> b)
    {
        b.ToTable("applications"); b.HasKey(x => x.Id);
        b.Property(x => x.ApplicationNumber).IsRequired().HasMaxLength(40);
        b.Property(x => x.ProductCode).IsRequired().HasMaxLength(50);
        b.Property(x => x.SchemeCode).IsRequired().HasMaxLength(50);
        b.Property(x => x.FinanceAmount).HasPrecision(18, 2).IsRequired();
        b.Property(x => x.IrrPercent).HasPrecision(9, 4).IsRequired();
        b.Property(x => x.ProcessingCharges).HasPrecision(18, 2).IsRequired();
        b.Property(x => x.RepaymentFrequency).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.DisbursementDate).HasColumnType("date").IsRequired();
        b.Property(x => x.Version).IsRowVersion().IsConcurrencyToken();
        b.HasIndex(x => x.ApplicationNumber).IsUnique();
        b.HasIndex(x => new { x.CreatedAtUtc, x.ApplicationNumber });
        b.HasIndex(x => x.LineOfBusinessId); b.HasIndex(x => x.CustomerId); b.HasIndex(x => x.BranchId);
        b.HasIndex(x => x.ProductCode); b.HasIndex(x => x.SchemeCode);
    }
}

public sealed class ApplicationScheduleConfiguration : IEntityTypeConfiguration<ApplicationSchedule>
{
    public void Configure(EntityTypeBuilder<ApplicationSchedule> b)
    {
        b.ToTable("application_schedules"); b.HasKey(x => x.Id);
        b.Property(x => x.FinanceAmount).HasPrecision(18, 2).IsRequired();
        b.Property(x => x.IrrPercent).HasPrecision(9, 4).IsRequired();
        b.Property(x => x.PeriodicInterestRate).HasPrecision(18, 12).IsRequired();
        b.Property(x => x.EmiAmount).HasPrecision(18, 2).IsRequired();
        b.Property(x => x.TotalInstallmentAmount).HasPrecision(18, 2).IsRequired();
        b.Property(x => x.TotalInterestAmount).HasPrecision(18, 2).IsRequired();
        b.Property(x => x.TotalPrincipalAmount).HasPrecision(18, 2).IsRequired();
        b.Property(x => x.RepaymentFrequency).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.DisbursementDate).HasColumnType("date").IsRequired();
        b.HasIndex(x => x.ApplicationId).IsUnique();
        b.HasMany(x => x.Installments).WithOne().HasForeignKey(x => x.ScheduleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ApplicationInstallmentConfiguration : IEntityTypeConfiguration<ApplicationInstallment>
{
    public void Configure(EntityTypeBuilder<ApplicationInstallment> b)
    {
        b.ToTable("application_installments"); b.HasKey(x => x.Id);
        b.Property(x => x.DueDate).HasColumnType("date").IsRequired();
        foreach (var p in new[] { nameof(ApplicationInstallment.OpeningBalance), nameof(ApplicationInstallment.InterestPortion),
            nameof(ApplicationInstallment.PrincipalPortion), nameof(ApplicationInstallment.InstallmentAmount),
            nameof(ApplicationInstallment.ClosingBalance) }) b.Property<decimal>(p).HasPrecision(18, 2).IsRequired();
        b.HasIndex(x => new { x.ScheduleId, x.InstallmentNumber }).IsUnique();
    }
}

public sealed class ApplicationStatusAuditEntryConfiguration : IEntityTypeConfiguration<ApplicationStatusAuditEntry>
{
    public void Configure(EntityTypeBuilder<ApplicationStatusAuditEntry> b)
    {
        b.ToTable("application_status_audit_entries"); b.HasKey(x => x.Id);
        b.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.ToStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.ChangedBy).IsRequired().HasMaxLength(200);
        b.Property(x => x.ChangedAtUtc).IsRequired();
        b.HasIndex(x => new { x.ApplicationId, x.ChangedAtUtc });
    }
}
