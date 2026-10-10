using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;

namespace Finnova.Repository.Configuration;

public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    // Deterministic seed values (HasData requires static data), mirroring UserAccountConfiguration.
    private static readonly DateTime SeedDate = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // Fixed GUID so the single-organization seed stays stable across migrations.
    private static readonly Guid SeedId = new("00000000-0000-0000-0003-000000000001");

    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("organizations");
        builder.HasKey(o => o.Id);

        // Basic Info
        builder.Property(o => o.Code).IsRequired().HasMaxLength(10);
        builder.HasIndex(o => o.Code).IsUnique();
        builder.Property(o => o.Name).IsRequired().HasMaxLength(100);
        builder.Property(o => o.ConstitutionType).HasConversion<string>().HasMaxLength(50);
        builder.Property(o => o.Description).HasMaxLength(500);

        // Registration Info
        builder.Property(o => o.CeoName).HasMaxLength(200);
        builder.Property(o => o.RegistrationNumber).HasMaxLength(100);
        builder.Property(o => o.PanNumber).HasMaxLength(20);
        builder.Property(o => o.GstNumber).HasMaxLength(30);

        // Corporate Address
        builder.Property(o => o.CorporateAddress).HasMaxLength(500);
        builder.Property(o => o.CorporateCity).HasMaxLength(100);
        builder.Property(o => o.CorporateState).HasMaxLength(100);
        builder.Property(o => o.CorporateCountry).HasMaxLength(100);
        builder.Property(o => o.CorporatePincode).HasMaxLength(10);

        // Communication Address
        builder.Property(o => o.CommunicationAddress).HasMaxLength(500);
        builder.Property(o => o.CommunicationCity).HasMaxLength(100);
        builder.Property(o => o.CommunicationState).HasMaxLength(100);
        builder.Property(o => o.CommunicationCountry).HasMaxLength(100);
        builder.Property(o => o.CommunicationPincode).HasMaxLength(10);

        // Contact Details
        builder.Property(o => o.Telephone).HasMaxLength(20);
        builder.Property(o => o.Mobile).HasMaxLength(20);
        builder.Property(o => o.Email).HasMaxLength(255);
        builder.Property(o => o.Website).HasMaxLength(500);

        // Accounting
        builder.Property(o => o.AccountingCurrency).IsRequired().HasMaxLength(10).HasDefaultValue("INR");

        // Status
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(50);

        // The product supports exactly ONE organization. Seed a single India-only company so
        // GetCurrentAsync() returns a record (otherwise /organizations/current returns 404).
        builder.HasData(new Organization
        {
            Id = SeedId,
            Code = "FINNOVA",
            Name = "Finnova Financial Services Ltd",
            ConstitutionType = ConstitutionType.PrivateLtd,
            Description = "Default organization for this Finnova instance.",
            CeoName = "Managing Director",
            RegistrationDate = SeedDate,
            RegistrationNumber = "U65999MH2024PTC000001",
            PanNumber = "AAACF1234F",
            GstNumber = "27AAACF1234F1Z5",
            CorporateAddress = "1 Finnova Tower, Bandra Kurla Complex",
            CorporateCity = "Mumbai",
            CorporateState = "Maharashtra",
            CorporateCountry = "India",
            CorporatePincode = "400051",
            CommunicationAddress = "1 Finnova Tower, Bandra Kurla Complex",
            CommunicationCity = "Mumbai",
            CommunicationState = "Maharashtra",
            CommunicationCountry = "India",
            CommunicationPincode = "400051",
            Telephone = "02261001000",
            Mobile = "9820010000",
            Email = "info@finnova.com",
            Website = "https://www.finnova.com",
            AccountingCurrency = "INR",
            Status = OrganizationStatus.Active,
            CreatedAt = SeedDate,
            UpdatedAt = SeedDate,
        });
    }
}
