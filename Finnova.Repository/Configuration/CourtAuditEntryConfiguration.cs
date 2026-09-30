using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class CourtAuditEntryConfiguration : IEntityTypeConfiguration<CourtAuditEntry>
{
    public void Configure(EntityTypeBuilder<CourtAuditEntry> builder)
    {
        builder.ToTable("court_audit_entries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CourtId).IsRequired();
        builder.Property(x => x.Action).IsRequired();          // stored as int
        builder.Property(x => x.OldValues);                    // nullable JSON text
        builder.Property(x => x.NewValues).IsRequired();
        builder.Property(x => x.Summary).IsRequired().HasMaxLength(400);
        builder.Property(x => x.ChangedBy).IsRequired().HasMaxLength(200);
        builder.Property(x => x.ChangedAtUtc).IsRequired();
        // Read path: entries for a court ordered by time desc then id desc (R5.5).
        builder.HasIndex(x => new { x.CourtId, x.ChangedAtUtc });
    }
}