using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class MakeCodeConfiguration : IEntityTypeConfiguration<MakeCode>
{
    private static readonly DateTime SeedDate = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static Guid Id(int n) => new($"00000000-0000-0000-00A2-{n:D12}"); // A2 = make master

    public void Configure(EntityTypeBuilder<MakeCode> builder)
    {
        builder.ToTable("make_codes");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Description).IsRequired().HasMaxLength(100);
        builder.Property(x => x.IsActive).IsRequired();

        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => x.Code);

        builder.HasData(
            new MakeCode { Id = Id(1), Code = "DEL", Description = "Dell", IsActive = true, CreatedAt = SeedDate, UpdatedAt = SeedDate },
            new MakeCode { Id = Id(2), Code = "HP", Description = "HP", IsActive = true, CreatedAt = SeedDate, UpdatedAt = SeedDate },
            new MakeCode { Id = Id(3), Code = "TAT", Description = "Tata", IsActive = true, CreatedAt = SeedDate, UpdatedAt = SeedDate });
    }
}
