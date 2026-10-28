using Finnova.Models.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finnova.Repository.Configuration;

public sealed class CustomerTrackEntryConfiguration : IEntityTypeConfiguration<CustomerTrackEntry>
{
    public void Configure(EntityTypeBuilder<CustomerTrackEntry> b)
    {
        b.ToTable("CustomerTrackEntries"); b.HasKey(x => x.Id); b.Property(x => x.TrackTypeCode).HasMaxLength(50).IsRequired();
        b.Property(x => x.LineOfBusinessCode).HasMaxLength(50); b.Property(x => x.AccountNumber).HasMaxLength(16);
        b.Property(x => x.Reason).HasMaxLength(500); b.Property(x => x.ByLogin).HasMaxLength(100); b.Property(x => x.ReleasedBy).HasMaxLength(100);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.HasIndex(x => new { x.CustomerId, x.AccountNumber, x.TrackTypeCode }).IsUnique(); b.HasIndex(x => x.CustomerId);
    }
}
