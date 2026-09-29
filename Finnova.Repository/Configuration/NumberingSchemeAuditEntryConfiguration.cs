using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class NumberingSchemeAuditEntryConfiguration : IEntityTypeConfiguration<NumberingSchemeAuditEntry>
{
    public void Configure(EntityTypeBuilder<NumberingSchemeAuditEntry> builder)
    {
        builder.ToTable("numbering_scheme_audit_entries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SchemeId).IsRequired();
        builder.Property(x => x.Action).IsRequired();      // stored as int
        builder.Property(x => x.OldValues);                // nullable JSON text
        builder.Property(x => x.NewValues).IsRequired();
        builder.Property(x => x.Summary).IsRequired().HasMaxLength(400);
        builder.Property(x => x.ChangedBy).IsRequired().HasMaxLength(200);
        builder.Property(x => x.ChangedAtUtc).IsRequired();
        // Read path: entries for a scheme ordered by time desc then id desc (R6.6).
        builder.HasIndex(x => new { x.SchemeId, x.ChangedAtUtc });
    }
}
