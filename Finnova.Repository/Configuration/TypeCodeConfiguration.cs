using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class TypeCodeConfiguration : IEntityTypeConfiguration<Finnova.Models.Domain.Entities.TypeCode>
{
    private static readonly DateTime SeedDate = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static Guid Id(int n) => new($"00000000-0000-0000-00A3-{n:D12}"); // A3 = type master

    public void Configure(EntityTypeBuilder<Finnova.Models.Domain.Entities.TypeCode> builder)
    {
        builder.ToTable("type_codes");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Description).IsRequired().HasMaxLength(100);
        builder.Property(x => x.IsActive).IsRequired();

        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => x.Code);

        builder.HasData(
            new Finnova.Models.Domain.Entities.TypeCode { Id = Id(1), Code = "HW", Description = "Hardware", IsActive = true, CreatedAt = SeedDate, UpdatedAt = SeedDate },
            new Finnova.Models.Domain.Entities.TypeCode { Id = Id(2), Code = "OFF", Description = "Office Equipment", IsActive = true, CreatedAt = SeedDate, UpdatedAt = SeedDate },
            new Finnova.Models.Domain.Entities.TypeCode { Id = Id(3), Code = "TRN", Description = "Transport", IsActive = true, CreatedAt = SeedDate, UpdatedAt = SeedDate });
    }
}
