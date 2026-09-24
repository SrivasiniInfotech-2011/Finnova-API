using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class NationalityAuditEntryConfiguration : IEntityTypeConfiguration<NationalityAuditEntry>
{
    public void Configure(EntityTypeBuilder<NationalityAuditEntry> builder)
    {
        builder.ToTable("nationality_audit_entries");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.NationalityId).IsRequired();
        builder.Property(x => x.Action).IsRequired();          // stored as int
        builder.Property(x => x.OldName).HasMaxLength(100);    // nullable
        builder.Property(x => x.NewName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.ChangedBy).IsRequired().HasMaxLength(200);
        builder.Property(x => x.ChangedAtUtc).IsRequired();

        // Read path: entries for a nationality ordered by time desc then id desc (R4.4).
        // No FK to Nationality so audit entries survive independently (R4.5).
        builder.HasIndex(x => new { x.NationalityId, x.ChangedAtUtc });
    }
}
