using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class OrganizationNodeAuditEntryConfiguration : IEntityTypeConfiguration<OrganizationNodeAuditEntry>
{
    public void Configure(EntityTypeBuilder<OrganizationNodeAuditEntry> builder)
    {
       // OrganizationNodeAuditEntryConfiguration
        builder.ToTable("organization_node_audit_entries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.NodeId).IsRequired();
        builder.Property(x => x.Action).IsRequired();          // stored as int
        builder.Property(x => x.OldName).HasMaxLength(150);    // nullable
        builder.Property(x => x.NewName).HasMaxLength(150);    // nullable (null on Delete)
        builder.Property(x => x.OldParentId);                  // nullable
        builder.Property(x => x.NewParentId);                  // nullable
        builder.Property(x => x.ChangedBy).IsRequired().HasMaxLength(200);
        builder.Property(x => x.ChangedAtUtc).IsRequired();
        // Read path: entries for a node ordered by time desc then id desc (R6.4).
        builder.HasIndex(x => new { x.NodeId, x.ChangedAtUtc });
    }
}