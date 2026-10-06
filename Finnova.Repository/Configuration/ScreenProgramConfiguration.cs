using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class ScreenProgramConfiguration : IEntityTypeConfiguration<ScreenProgram>
{
    public void Configure(EntityTypeBuilder<ScreenProgram> builder)
    {
        builder.ToTable("programs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProgramName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(150);
        builder.Property(x => x.Module).HasMaxLength(100);
        builder.Property(x => x.IsActive).IsRequired();

        // ProgramName is the stable screen key the UI resolves permissions against; it must be
        // unique. SQL Server's default case-insensitive collation backs the uniqueness rule.
        builder.HasIndex(x => x.ProgramName).IsUnique();

        // Deterministic seed (GUIDs + fixed timestamp) so migrations are reproducible.
        var seeded = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        builder.HasData(
            Seed(1, "LookupMaster", "Lookup Master", seeded),
            Seed(2, "NationalityMaster", "Nationality Master", seeded),
            Seed(3, "OrganizationHierarchy", "Organization Hierarchy", seeded),
            Seed(4, "DocumentNumberControl", "Document Number Control", seeded),
            Seed(5, "CourtMaster", "Court Master", seeded),
            Seed(6, "EntityMaster", "Entity Master", seeded),
            Seed(7, "UserManagement", "User Management", seeded),
            Seed(8, "LocationMaster", "Location Master", seeded),
            Seed(9, "Organization", "Organization", seeded));
    }

    private static ScreenProgram Seed(int n, string programName, string displayName, DateTime at)
        => new()
        {
            Id = new Guid($"00000000-0000-0000-0001-0000000000{n:D2}"),
            ProgramName = programName,
            DisplayName = displayName,
            Module = "SystemAdmin",
            IsActive = true,
            CreatedAt = at,
            UpdatedAt = at
        };
}
