using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;

namespace Finnova.Repository.Configuration;

public class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    // Deterministic seed values (HasData requires static data).
    private static readonly DateTime SeedDate = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // Deterministic GUIDs for the seed accounts so migrations stay stable.
    private static Guid Id(int n) => new($"00000000-0000-0000-0002-{n:D12}");

    // PBKDF2 "salt.hash" for the default seed password "User@123" (HMACSHA256, 100k iterations,
    // fixed salt). Matches Finnova.Service.Auth.PasswordHasher's format so seeded users can log
    // in; verified against the real hasher. Change the password after first login.
    private const string SeedPasswordHash =
        "AQIDBAUGBwgJCgsMDQ4PEA==.65aXkOppO1PIxiPNP45E/WmbZbtGN3dbC+lchI1ZTh8=";

    // PBKDF2 "salt.hash" for the admin password "Admin@123" (same format), migrated from the old
    // users-table admin seed so the admin login survives the users -> user_accounts consolidation.
    private const string AdminPasswordHash =
        "AQIDBAUGBwgJCgsMDQ4PEA==.7gQDaNbD2TJ9Tv/U3z+oOOw+byXCRpvOoV5EbjMrc1w=";

    public void Configure(EntityTypeBuilder<UserAccount> b)
    {
        b.ToTable("user_accounts");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserCode).IsRequired().HasMaxLength(6);
        b.Property(x => x.UserName).IsRequired().HasMaxLength(30);     // login credential
        b.Property(x => x.Name).IsRequired().HasMaxLength(50);
        b.Property(x => x.PasswordHash).IsRequired().HasMaxLength(400);
        b.Property(x => x.Role).HasConversion<string>().HasMaxLength(50); // UserRole stored by name
        b.Property(x => x.Designation).IsRequired().HasMaxLength(40);
        b.Property(x => x.Department).IsRequired().HasMaxLength(40);
        b.Property(x => x.MobileNumber).HasMaxLength(12);
        b.Property(x => x.Email).HasMaxLength(60);
        b.Property(x => x.UserType).IsRequired();              // int
        b.Property(x => x.IsActive).IsRequired();

        b.HasIndex(x => x.UserCode).IsUnique();                // company-wide uniqueness (R2.4)
        b.HasIndex(x => x.UserName).IsUnique();                // login credential uniqueness
        b.HasIndex(x => x.Name);                               // search/order (R13.2/13.9)

        b.HasMany(x => x.AccessAssignments).WithOne()
            .HasForeignKey(a => a.UserAccountId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.BranchAssociations).WithOne()
            .HasForeignKey(a => a.UserAccountId).OnDelete(DeleteBehavior.Cascade);

        b.HasData(SeedData());
    }

    private static IEnumerable<UserAccount> SeedData()
    {
        UserAccount U(int id, string code, string userName, string name, string designation,
            string department, string? mobile, string? email, UserType type, UserRole role,
            string passwordHash, bool active = true) => new()
        {
            Id = Id(id),
            UserCode = code,
            UserName = userName,
            Name = name,
            PasswordHash = passwordHash,
            Role = role,
            DateOfJoining = SeedDate,
            Designation = designation,
            Department = department,
            MobileNumber = mobile,
            Email = email,
            UserType = type,
            IsActive = active,
            CreatedAt = SeedDate,
            UpdatedAt = SeedDate,
        };

        return new[]
        {
            // Admin login, migrated from the retired users table (login: admin / Admin@123).
            U(0, "ADMIN", "admin", "Admin User", "Administrator", "Administration",
                null, "admin@finnova.com", UserType.Corporate, UserRole.Admin, AdminPasswordHash),
            U(1, "USR001", "usr001", "Rohan Mehta", "Manager", "Operations",
                "9820012345", "rohan.mehta@finnova.com", UserType.Corporate, UserRole.User, SeedPasswordHash),
            U(2, "USR002", "usr002", "Priya Nair", "Officer", "Finance",
                "9845023456", "priya.nair@finnova.com", UserType.Corporate, UserRole.User, SeedPasswordHash),
            U(3, "USR003", "usr003", "Arjun Rao", "Teller", "Retail Banking",
                "9731034567", "arjun.rao@finnova.com", UserType.Branch, UserRole.User, SeedPasswordHash),
            U(4, "USR004", "usr004", "Sneha Gupta", "Branch Head", "Retail Banking",
                "9920045678", "sneha.gupta@finnova.com", UserType.Branch, UserRole.User, SeedPasswordHash),
            U(5, "USR005", "usr005", "Vikram Shah", "Analyst", "Risk",
                null, null, UserType.Corporate, UserRole.User, SeedPasswordHash, active: false),
        };
    }
}
