using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class ModelCodeConfiguration : IEntityTypeConfiguration<ModelCode>
{
    private static readonly DateTime SeedDate = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static Guid Id(int n) => new($"00000000-0000-0000-00A4-{n:D12}"); // A4 = model master

    public void Configure(EntityTypeBuilder<ModelCode> builder)
    {
        builder.ToTable("model_codes");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Description).IsRequired().HasMaxLength(100);
        builder.Property(x => x.IsActive).IsRequired();

        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => x.Code);

        builder.HasData(
            new ModelCode { Id = Id(1), Code = "MDL1", Description = "Model 2024 Series", IsActive = true, CreatedAt = SeedDate, UpdatedAt = SeedDate },
            new ModelCode { Id = Id(2), Code = "MDL2", Description = "Model 2023 Series", IsActive = true, CreatedAt = SeedDate, UpdatedAt = SeedDate });
    }
}
