using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class GLAuditEntryConfiguration : IEntityTypeConfiguration<GLAuditEntry>
{
    public void Configure(EntityTypeBuilder<GLAuditEntry> builder)
    {
        builder.ToTable("gl_audit_entries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.GLAccountId).IsRequired();
        builder.Property(x => x.Action).IsRequired();
        builder.Property(x => x.NewValues).IsRequired();
        builder.Property(x => x.Summary).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Remarks).HasMaxLength(1000);
        builder.Property(x => x.ChangedBy).IsRequired().HasMaxLength(100);
        builder.Property(x => x.ChangedByRole).HasMaxLength(100);
        builder.Property(x => x.ChangedAtUtc).IsRequired();
        builder.HasIndex(x => x.GLAccountId);
        builder.HasIndex(x => x.ChangedAtUtc);
    }
}
