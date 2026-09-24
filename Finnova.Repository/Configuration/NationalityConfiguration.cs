using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class NationalityConfiguration : IEntityTypeConfiguration<Nationality>
{
    public void Configure(EntityTypeBuilder<Nationality> builder)
    {
        builder.ToTable("nationalities");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).IsRequired().HasMaxLength(10);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.IsActive).IsRequired();

        // Uniqueness of Code (R2.1/2.2). SQL Server default collation (e.g.
        // SQL_Latin1_General_CP1_CI_AS) is case-insensitive, so the unique index
        // backs the case-insensitive rule. Codes are trimmed by the handler before
        // persistence so stored values are canonical.
        builder.HasIndex(x => x.Code).IsUnique();

        // Query path: search/order by Name (R5.10).
        builder.HasIndex(x => x.Name);
    }
}
