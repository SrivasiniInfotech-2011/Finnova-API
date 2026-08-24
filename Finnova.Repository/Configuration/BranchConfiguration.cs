using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("branches");
        builder.HasKey(b => b.Id);

        // Identification
        builder.Property(b => b.BranchType).HasConversion<string>().IsRequired().HasMaxLength(20);
        builder.Property(b => b.CorporateCode).IsRequired().HasMaxLength(3);
        builder.Property(b => b.StateCode).IsRequired().HasMaxLength(3);
        builder.Property(b => b.BranchCode).IsRequired().HasMaxLength(3);
        builder.Property(b => b.BranchName).IsRequired().HasMaxLength(50);

        // Composite unique index: CorporateCode + StateCode + BranchCode
        builder.HasIndex(b => new { b.CorporateCode, b.StateCode, b.BranchCode }).IsUnique();

        // Address
        builder.Property(b => b.Address).IsRequired().HasMaxLength(100);
        builder.Property(b => b.Landmark).IsRequired().HasMaxLength(100);
        builder.Property(b => b.State).IsRequired().HasMaxLength(50);
        builder.Property(b => b.Country).IsRequired().HasMaxLength(50).HasDefaultValue("India");
        builder.Property(b => b.Pincode).HasMaxLength(10);

        // Contact
        builder.Property(b => b.Telephone).HasMaxLength(20);
        builder.Property(b => b.Mobile).HasMaxLength(20);

        // Status
        builder.Property(b => b.IsActive).IsRequired();
        builder.Property(b => b.IsOperational).IsRequired();

        // Navigation to Organization
        builder.HasOne(b => b.Organization)
            .WithMany()
            .HasForeignKey(b => b.OrganizationId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
