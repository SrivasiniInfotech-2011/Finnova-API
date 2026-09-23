using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    // Deterministic seed date (HasData requires static values).
    private static readonly DateTime SeedDate = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // Deterministic GUIDs for the seed tree so migrations stay stable.
    private static Guid Id(int n) => new($"00000000-0000-0000-0000-{n:D12}");

    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.ToTable("locations");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Code).IsRequired().HasMaxLength(20);
        builder.Property(l => l.Name).IsRequired().HasMaxLength(100);
        builder.Property(l => l.Level).IsRequired();
        builder.Property(l => l.Description).HasMaxLength(500);
        builder.Property(l => l.IsActive).IsRequired();

        builder.HasIndex(l => new { l.ParentId, l.Code }).IsUnique();

        // Self-referencing hierarchy
        builder.HasOne(l => l.Parent)
            .WithMany(l => l.Children)
            .HasForeignKey(l => l.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(SeedData());
    }

    private static IEnumerable<Location> SeedData()
    {
        Location L(int id, string code, string name, int level, int? parent, string desc, bool active = true) => new()
        {
            Id = Id(id),
            Code = code,
            Name = name,
            Level = level,
            ParentId = parent is null ? null : Id(parent.Value),
            Description = desc,
            IsActive = active,
            CreatedAt = SeedDate,
            UpdatedAt = SeedDate,
        };

        return new[]
        {
            // Country
            L(1, "IND", "India", 1, null, "Republic of India"),
            // States
            L(2, "MH", "Maharashtra", 2, 1, "State of Maharashtra"),
            L(3, "KA", "Karnataka", 2, 1, "State of Karnataka"),
            // Cities
            L(4, "MUM", "Mumbai", 3, 2, "Financial Capital"),
            L(5, "PUN", "Pune", 3, 2, "Cultural Capital of Maharashtra"),
            L(6, "BLR", "Bangalore", 3, 3, "Silicon Valley of India"),
            // Zones
            L(7, "MUM-S", "South Mumbai", 4, 4, "South Zone"),
            L(8, "MUM-W", "Western Mumbai", 4, 4, "Western Zone"),
            L(9, "PUN-C", "Central Pune", 4, 5, "Central Zone"),
            // Branches
            L(10, "MUM-S-01", "Fort Branch", 5, 7, "Fort Area Branch Office"),
            L(11, "MUM-S-02", "Colaba Branch", 5, 7, "Colaba Area Branch Office", active: false),
            L(12, "MUM-W-01", "Andheri Branch", 5, 8, "Andheri West Branch Office"),
            L(13, "PUN-C-01", "Shivajinagar Branch", 5, 9, "Shivajinagar Branch Office"),
        };
    }
}
