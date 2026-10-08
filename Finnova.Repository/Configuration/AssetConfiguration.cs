using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    private static readonly DateTime SeedDate = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static Guid Id(int n) => new($"00000000-0000-0000-00A5-{n:D12}");
    private static Guid ClassId(int n) => new($"00000000-0000-0000-00A1-{n:D12}");
    private static Guid TypeId(int n) => new($"00000000-0000-0000-00A3-{n:D12}");

    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        builder.ToTable("assets");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.AssetCode).IsRequired().HasMaxLength(30);
        builder.Property(x => x.Description).IsRequired().HasMaxLength(200);

        builder.Property(x => x.BookDepreciationCategory).IsRequired().HasMaxLength(50);
        builder.Property(x => x.StockDepreciationCategory).IsRequired().HasMaxLength(50);

        // Rates are percentages 0..100 with <=2 decimal places (R4.5).
        builder.Property(x => x.BookDepreciationRate).HasColumnType("decimal(5,2)");
        builder.Property(x => x.StockDepreciationRate).HasColumnType("decimal(5,2)");
        // Guideline limit is a non-negative monetary value (R4.6).
        builder.Property(x => x.GuidelineLimit).HasColumnType("decimal(18,2)");

        builder.Property(x => x.IsActive).IsRequired();

        // Generated Asset Code is globally unique (R3.3, R3.6).
        builder.HasIndex(x => x.AssetCode).IsUnique();
        // Query/order path (R5.7).
        builder.HasIndex(x => x.AssetCode);

        // Taxonomy FKs. Restrict delete so an in-use code cannot be hard-deleted at the DB
        // level (the service also guards this with CodeInUseException — R6.4).
        builder.HasOne(x => x.ClassCode).WithMany().HasForeignKey(x => x.ClassCodeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.TypeCode).WithMany().HasForeignKey(x => x.TypeCodeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.MakeCode).WithMany().HasForeignKey(x => x.MakeCodeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ModelCode).WithMany().HasForeignKey(x => x.ModelCodeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasData(new Asset
        {
            Id = Id(1),
            AssetCode = "LAP-000001",
            Description = "Standard Issue Laptop",
            ClassCodeId = ClassId(1),
            TypeCodeId = TypeId(1),
            BookDepreciationCategory = "Straight Line",
            BookDepreciationRate = 25.00m,
            StockDepreciationCategory = "WDV",
            StockDepreciationRate = 15.00m,
            GuidelineLimit = 60000.00m,
            IsActive = true,
            CreatedAt = SeedDate,
            UpdatedAt = SeedDate,
        });
    }
}
