using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class LineOfBusinessConfiguration : IEntityTypeConfiguration<LineOfBusiness>
{
    public void Configure(EntityTypeBuilder<LineOfBusiness> builder)
    {
        builder.ToTable("lines_of_business");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.LOB_Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.LOB_Description).HasMaxLength(400);
        builder.Property(x => x.IsActive).IsRequired();

        // LOB_Name is the business-line key; it must be unique. SQL Server's default
        // case-insensitive collation backs the uniqueness rule.
        builder.HasIndex(x => x.LOB_Name).IsUnique();

        // Deterministic seed (GUIDs + fixed timestamp) so migrations are reproducible. The three
        // seeded LOBs mirror LineOfBusinessCatalog.WithRoleCodes (the Role-Code-linked lines).
        var seeded = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        builder.HasData(
            Seed(1, "Retail Lending", "Loans and credit products for individual retail customers.", seeded),
            Seed(2, "Corporate Lending", "Credit facilities for corporate and business customers.", seeded),
            Seed(3, "Leasing", "Asset leasing and hire-purchase finance.", seeded));
    }

    private static LineOfBusiness Seed(int n, string name, string description, DateTime at)
        => new()
        {
            Id = new Guid($"00000000-0000-0000-0002-0000000000{n:D2}"),
            LOB_Name = name,
            LOB_Description = description,
            IsActive = true,
            CreatedAt = at,
            UpdatedAt = at
        };
}
