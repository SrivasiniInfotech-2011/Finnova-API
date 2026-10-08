using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class ClassCodeConfiguration : IEntityTypeConfiguration<ClassCode>
{
    private static readonly DateTime SeedDate = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static Guid Id(int n) => new($"00000000-0000-0000-00A1-{n:D12}"); // A1 = class master

    public void Configure(EntityTypeBuilder<ClassCode> builder)
    {
        builder.ToTable("class_codes");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Description).IsRequired().HasMaxLength(100);
        builder.Property(x => x.IsActive).IsRequired();

        // Code unique within this master only (R1.2). SQL Server CI collation backs case-insensitivity.
        builder.HasIndex(x => x.Code).IsUnique();
        // Query path: list ordered by Code (R2.11).
        builder.HasIndex(x => x.Code);

        builder.HasData(
            new ClassCode { Id = Id(1), Code = "LAP", Description = "Laptops & Computers", IsActive = true, CreatedAt = SeedDate, UpdatedAt = SeedDate },
            new ClassCode { Id = Id(2), Code = "FUR", Description = "Furniture & Fixtures", IsActive = true, CreatedAt = SeedDate, UpdatedAt = SeedDate },
            new ClassCode { Id = Id(3), Code = "VEH", Description = "Vehicles", IsActive = true, CreatedAt = SeedDate, UpdatedAt = SeedDate });
    }
}
