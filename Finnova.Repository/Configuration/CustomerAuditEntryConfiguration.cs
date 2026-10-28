using Finnova.Models.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finnova.Repository.Configuration;

public sealed class CustomerAuditEntryConfiguration : IEntityTypeConfiguration<CustomerAuditEntry>
{
    public void Configure(EntityTypeBuilder<CustomerAuditEntry> b)
    {
        b.ToTable("CustomerAuditEntries"); b.HasKey(x => x.Id); b.Property(x => x.ActorId).HasMaxLength(100).IsRequired();
        b.Property(x => x.ChangedFieldsJson).HasMaxLength(8000).IsRequired(); b.Property(x => x.Action).HasConversion<string>().HasMaxLength(40);
        b.HasIndex(x => new { x.CustomerId, x.CreatedAtUtc }); b.HasIndex(x => x.ActorId);
    }
}
