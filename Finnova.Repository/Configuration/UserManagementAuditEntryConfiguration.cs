using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class UserManagementAuditEntryConfiguration : IEntityTypeConfiguration<UserManagementAuditEntry>
{
    public void Configure(EntityTypeBuilder<UserManagementAuditEntry> b)
    {
        b.ToTable("user_management_audit");
        b.HasKey(x => x.Id);
        b.Property(x => x.RecordId).IsRequired();
        b.Property(x => x.RecordKind).IsRequired();            // int
        b.Property(x => x.Action).IsRequired();               // int
        b.Property(x => x.OldValues);
        b.Property(x => x.NewValues).IsRequired();
        b.Property(x => x.Summary).IsRequired().HasMaxLength(1000);
        b.Property(x => x.ChangedBy).IsRequired().HasMaxLength(200);
        b.Property(x => x.ChangedAtUtc).IsRequired();

        b.HasIndex(x => new { x.RecordId, x.ChangedAtUtc });   // newest-first lookups (R14)
    }
}
