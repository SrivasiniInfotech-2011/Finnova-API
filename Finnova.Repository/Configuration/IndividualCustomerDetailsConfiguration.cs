using Finnova.Models.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finnova.Repository.Configuration;

public sealed class IndividualCustomerDetailsConfiguration : IEntityTypeConfiguration<IndividualCustomerDetails>
{
    public void Configure(EntityTypeBuilder<IndividualCustomerDetails> b)
    {
        b.ToTable("IndividualCustomerDetails"); b.HasKey(x => x.Id); b.HasIndex(x => x.CustomerId).IsUnique();
        b.Property(x => x.GenderCode).HasMaxLength(50); b.Property(x => x.SpouseName).HasMaxLength(150);
        b.Property(x => x.Qualification).HasMaxLength(150); b.Property(x => x.Profession).HasMaxLength(150);
        b.Property(x => x.IndiaIdentityType).HasMaxLength(50); b.Property(x => x.IndiaIdentityReference).HasMaxLength(200);
    }
}
