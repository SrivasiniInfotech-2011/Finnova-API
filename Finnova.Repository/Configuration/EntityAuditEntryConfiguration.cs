using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class EntityAuditEntryConfiguration : IEntityTypeConfiguration<EntityAuditEntry>
{
    public void Configure(EntityTypeBuilder<EntityAuditEntry> builder)
    {
        builder.ToTable("entity_audit_entries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EntityId).IsRequired();
        builder.Property(x => x.Action).IsRequired();          // stored as int
        builder.Property(x => x.OldValues);                    // nullable JSON text
        builder.Property(x => x.NewValues).IsRequired();
        builder.Property(x => x.Summary).IsRequired().HasMaxLength(400);
        builder.Property(x => x.ChangedBy).IsRequired().HasMaxLength(200);
        builder.Property(x => x.ChangedAtUtc).IsRequired();
        // Read path: entries for an entity ordered by time desc then id desc (R5.5). No FK to
        // entities (audit outlives edits; missing-id read returns empty).
        builder.HasIndex(x => new { x.EntityId, x.ChangedAtUtc });
    }
}