using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class GLAccountConfiguration : IEntityTypeConfiguration<GLAccount>
{
    public void Configure(EntityTypeBuilder<GLAccount> builder)
    {
        builder.ToTable("gl_accounts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CompanyId).IsRequired();
        builder.Property(x => x.GLCode).IsRequired().HasMaxLength(9);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(150);
        builder.Property(x => x.Level).IsRequired();            // stored as int
        builder.Property(x => x.AccountNature).IsRequired();
        builder.Property(x => x.NormalBalance).IsRequired();
        builder.Property(x => x.GLType).IsRequired();
        builder.Property(x => x.SubLedgerType).HasMaxLength(100);
        builder.Property(x => x.TaxType).HasMaxLength(100);
        builder.Property(x => x.IsPostable).IsRequired();
        builder.Property(x => x.CurrencyId).IsRequired();
        builder.Property(x => x.AllowManualJV).IsRequired();
        builder.Property(x => x.CostCentreMandatory).IsRequired();
        builder.Property(x => x.ProfitCentreMandatory).IsRequired();
        builder.Property(x => x.ReportingLine).HasMaxLength(100);
        builder.Property(x => x.EffectiveFrom).IsRequired();
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.MakerId).HasMaxLength(100);
        builder.Property(x => x.CheckerId).HasMaxLength(100);
        builder.Property(x => x.AuthorizedVersionNumber).IsRequired();
        builder.Property(x => x.RejectionReason).HasMaxLength(500);

        // Code uniqueness scoped per company (R2.1/2.3). Default CI collation backs the
        // case-insensitive Name rule; codes are trimmed by the handler so stored values are canonical.
        builder.HasIndex(x => new { x.CompanyId, x.GLCode }).IsUnique();
        builder.HasIndex(x => new { x.CompanyId, x.Name });
        builder.HasIndex(x => x.ParentGLId);

        builder.HasMany(x => x.BranchApplicabilities)
               .WithOne()
               .HasForeignKey(b => b.GLAccountId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
