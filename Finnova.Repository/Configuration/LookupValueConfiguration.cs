using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class LookupValueConfiguration : IEntityTypeConfiguration<LookupValue>
{
    // Deterministic seed date (HasData requires static values).
    private static readonly DateTime SeedDate = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // Deterministic GUIDs for the seed rows so migrations stay stable.
    private static Guid Id(int n) => new($"00000000-0000-0000-0001-{n:D12}");

    public void Configure(EntityTypeBuilder<LookupValue> builder)
    {
        builder.ToTable("lookup_values");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Module).IsRequired().HasMaxLength(50);
        builder.Property(x => x.LookupType).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Value).IsRequired().HasMaxLength(200);
        builder.Property(x => x.DisplayOrder).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.IsSystemLocked).IsRequired();

        // Uniqueness of Code within Module + Lookup Type scope (R2.8, R5.3).
        builder.HasIndex(x => new { x.Module, x.LookupType, x.Code }).IsUnique();

        // Query path: filter by module/type/active then order by DisplayOrder (R4, R6).
        builder.HasIndex(x => new { x.Module, x.LookupType, x.IsActive, x.DisplayOrder });

        builder.HasData(SeedData());
    }

    private static IEnumerable<LookupValue> SeedData()
    {
        LookupValue V(int id, string module, string type, string code, string value,
            int order, bool active = true, bool locked = false) => new()
        {
            Id = Id(id),
            Module = module,
            LookupType = type,
            Code = code,
            Value = value,
            DisplayOrder = order,
            IsActive = active,
            IsSystemLocked = locked,
            CreatedAt = SeedDate,
            UpdatedAt = SeedDate,
        };

        return new[]
        {
            // A System-Locked set demonstrating R3 protection (TC-LKP-02).
            V(1, "SystemAdmin", "SYS_TXN_TYPE", "DEBIT", "Debit", 1, locked: true),
            V(2, "SystemAdmin", "SYS_TXN_TYPE", "CREDIT", "Credit", 2, locked: true),

            // MARITAL_STATUS set under Origination (TC-LKP-01 scope). "WID" is added by the test.
            V(3, "Origination", "MARITAL_STATUS", "SIN", "Single", 1),
            V(4, "Origination", "MARITAL_STATUS", "MAR", "Married", 2),
            V(5, "Origination", "MARITAL_STATUS", "DIV", "Divorced", 3),
        };
    }
}
