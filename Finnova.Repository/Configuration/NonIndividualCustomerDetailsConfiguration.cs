using Finnova.Models.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finnova.Repository.Configuration;

public sealed class NonIndividualCustomerDetailsConfiguration : IEntityTypeConfiguration<NonIndividualCustomerDetails>
{
    public void Configure(EntityTypeBuilder<NonIndividualCustomerDetails> b)
    {
        b.ToTable("NonIndividualCustomerDetails"); b.HasKey(x => x.Id); b.HasIndex(x => x.CustomerId).IsUnique();
        b.Property(x => x.PublicOrCloselyHeldCode).HasMaxLength(50); b.Property(x => x.BusinessProfile).HasMaxLength(240);
        b.Property(x => x.GeographicalCoverage).HasMaxLength(120); b.Property(x => x.JVPartnerName).HasMaxLength(120);
        b.Property(x => x.CEOOrMDName).HasMaxLength(50); b.Property(x => x.ResidentialAddress).HasMaxLength(300);
        b.Property(x => x.CustomerBusinessTypeCode).HasMaxLength(50); b.Property(x => x.CustomerBusinessSubTypeCode).HasMaxLength(50);
        b.Property(x => x.IndustrySectorCode).HasMaxLength(50); b.Property(x => x.SubSectorCode).HasMaxLength(50);
        b.Property(x => x.BusinessActivities).HasMaxLength(100); b.Property(x => x.FactoringTypeCode).HasMaxLength(50);
    }
}
