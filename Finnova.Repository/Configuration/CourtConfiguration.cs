using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class CourtConfiguration : IEntityTypeConfiguration<Court>
{
    public void Configure(EntityTypeBuilder<Court> builder)
    {
        builder.ToTable("courts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(150);
        builder.Property(x => x.CourtType).IsRequired();      // stored as int
        builder.Property(x => x.Jurisdiction).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Location).IsRequired().HasMaxLength(100);
        builder.Property(x => x.IsActive).IsRequired();

        // Code uniqueness (R2.1/2.2). Default CI collation backs the case-insensitive rule; codes
        // are trimmed by the handler so stored values are canonical.
        builder.HasIndex(x => x.Code).IsUnique();
        // Query/order by Name (R3.7).
        builder.HasIndex(x => x.Name);
    }
}