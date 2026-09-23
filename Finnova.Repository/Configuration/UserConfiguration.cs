using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;

namespace Finnova.Repository.Configuration;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    // Deterministic seed values (HasData requires static data).
    private static readonly Guid AdminId = new("00000000-0000-0000-0001-000000000001");
    private static readonly DateTime SeedDate = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // PBKDF2 hash of "Admin@123" (HMACSHA256, 100k iterations, fixed salt).
    // Format: "{saltBase64}.{hashBase64}".
    private const string AdminPasswordHash =
        "AQIDBAUGBwgJCgsMDQ4PEA==.7gQDaNbD2TJ9Tv/U3z+oOOw+byXCRpvOoV5EbjMrc1w=";

    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(u => u.MiddleName).HasMaxLength(100);
        builder.Property(u => u.LastName).IsRequired().HasMaxLength(100);
        builder.Property(u => u.Email).IsRequired().HasMaxLength(255);
        builder.HasIndex(u => u.Email).IsUnique();
        builder.Property(u => u.Phone).HasMaxLength(20);
        builder.Property(u => u.PasswordHash).HasMaxLength(200);
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(50);
        builder.Property(u => u.Status).HasConversion<string>().HasMaxLength(50);

        builder.HasOne(u => u.Organization)
            .WithMany(o => o.Users)
            .HasForeignKey(u => u.OrganizationId)
            .OnDelete(DeleteBehavior.SetNull);

        // Seed a default admin user (email: admin@finnova.com / password: Admin@123).
        builder.HasData(new User
        {
            Id = AdminId,
            FirstName = "Admin",
            MiddleName = null,
            LastName = "User",
            Email = "admin@finnova.com",
            Phone = null,
            PasswordHash = AdminPasswordHash,
            Role = UserRole.Admin,
            Status = UserStatus.Active,
            OrganizationId = null,
            CreatedAt = SeedDate,
            UpdatedAt = SeedDate,
        });
    }
}
