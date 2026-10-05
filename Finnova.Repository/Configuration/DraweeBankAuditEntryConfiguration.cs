using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class DraweeBankAuditEntryConfiguration : IEntityTypeConfiguration<DraweeBankAuditEntry>
{
    public void Configure(EntityTypeBuilder<DraweeBankAuditEntry> builder)
    {
        builder.ToTable("drawee_bank_audit_entries");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.DraweeBankId).IsRequired();
        builder.Property(x => x.Action).IsRequired();            // stored as int
        builder.Property(x => x.BeforeSnapshot);                 // nullable nvarchar(max)
        builder.Property(x => x.AfterSnapshot);                  // nullable nvarchar(max)
        builder.Property(x => x.ChangedBy).IsRequired().HasMaxLength(200);
        builder.Property(x => x.ChangedAtUtc).IsRequired();

        // Read path: entries for a bank ordered by time desc then id desc (R7.4).
        builder.HasIndex(x => new { x.DraweeBankId, x.ChangedAtUtc });
        // No FK to DraweeBank so entries survive independently and a missing-id read is [] (R7.5).
    }
}
