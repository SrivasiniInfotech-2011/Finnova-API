using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class RestrictionDetailConfiguration : IEntityTypeConfiguration<RestrictionDetail>
{
    public void Configure(EntityTypeBuilder<RestrictionDetail> builder)
    {
        builder.ToTable("restriction_details");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ClearingDays).IsRequired();
        builder.Property(x => x.StartDate).IsRequired();
        builder.Property(x => x.EndDate).IsRequired();

        builder.HasIndex(x => x.DraweeBankId).IsUnique();   // at most one restriction per bank
    }
}
