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

    public void Configure(EntityTypeBuilder<UserAccount> b)
    {
        b.ToTable("user_accounts");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserCode).IsRequired().HasMaxLength(6);
        b.Property(x => x.Name).IsRequired().HasMaxLength(50);
        b.Property(x => x.PasswordHash).IsRequired().HasMaxLength(400);
        b.Property(x => x.Designation).IsRequired().HasMaxLength(40);
        b.Property(x => x.Department).IsRequired().HasMaxLength(40);
        b.Property(x => x.MobileNumber).HasMaxLength(12);
        b.Property(x => x.Email).HasMaxLength(60);
        b.Property(x => x.UserType).IsRequired();              // int
        b.Property(x => x.IsActive).IsRequired();

        b.HasIndex(x => x.UserCode).IsUnique();                // company-wide uniqueness (R2.4)
        b.HasIndex(x => x.Name);                               // search/order (R13.2/13.9)

        b.HasMany(x => x.AccessAssignments).WithOne()
            .HasForeignKey(a => a.UserAccountId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.BranchAssociations).WithOne()
            .HasForeignKey(a => a.UserAccountId).OnDelete(DeleteBehavior.Cascade);

        b.HasData(SeedData());
    }

    private static IEnumerable<UserAccount> SeedData()
    {
        UserAccount U(int id, string code, string name, string designation, string department,
            string? mobile, string? email, UserType type, bool active = true) => new()
        {
            Id = Id(id),
            UserCode = code,
            Name = name,
            PasswordHash = SeedPasswordHash,
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
            U(1, "USR001", "Rohan Mehta", "Manager", "Operations",
                "9820012345", "rohan.mehta@finnova.com", UserType.Corporate),
            U(2, "USR002", "Priya Nair", "Officer", "Finance",
                "9845023456", "priya.nair@finnova.com", UserType.Corporate),
            U(3, "USR003", "Arjun Rao", "Teller", "Retail Banking",
                "9731034567", "arjun.rao@finnova.com", UserType.Branch),
            U(4, "USR004", "Sneha Gupta", "Branch Head", "Retail Banking",
                "9920045678", "sneha.gupta@finnova.com", UserType.Branch),
            U(5, "USR005", "Vikram Shah", "Analyst", "Risk",
                null, null, UserType.Corporate, active: false),
        };
    }
}
