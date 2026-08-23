using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("organizations");
        builder.HasKey(o => o.Id);

        // Basic Info
        builder.Property(o => o.Code).IsRequired().HasMaxLength(5);
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
    }
}
