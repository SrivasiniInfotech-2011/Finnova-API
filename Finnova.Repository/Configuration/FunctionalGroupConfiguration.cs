using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class FunctionalGroupConfiguration : IEntityTypeConfiguration<FunctionalGroup>
{
    public void Configure(EntityTypeBuilder<FunctionalGroup> b)
    {
        b.ToTable("functional_groups");
        b.HasKey(x => x.Id);
        b.Property(x => x.FunctionalGroupCode).IsRequired().HasMaxLength(6);
        b.Property(x => x.RoleCenterName).IsRequired().HasMaxLength(100);
        b.Property(x => x.IsActive).IsRequired();

        b.HasIndex(x => x.FunctionalGroupCode).IsUnique();     // company-wide uniqueness (R2.4)

        b.HasMany(x => x.Functions).WithOne()
            .HasForeignKey(f => f.FunctionalGroupId).OnDelete(DeleteBehavior.Cascade);
    }
}
