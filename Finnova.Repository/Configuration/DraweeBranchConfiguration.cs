using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class DraweeBranchConfiguration : IEntityTypeConfiguration<DraweeBranch>
{
    public void Configure(EntityTypeBuilder<DraweeBranch> builder)
    {
        builder.ToTable("drawee_branches");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.PlaceCode).IsRequired().HasMaxLength(20);
        builder.Property(x => x.PlaceName).IsRequired().HasMaxLength(150);
        builder.Property(x => x.Address).HasMaxLength(300);
        builder.Property(x => x.PostalCode).IsRequired().HasMaxLength(6);   // six-digit PIN (R3.6)
        builder.Property(x => x.StartDate).IsRequired();
        builder.Property(x => x.EndDate);                                   // nullable (R3.5)

        // Place Code unique within the owning bank (R3.3).
        builder.HasIndex(x => new { x.DraweeBankId, x.PlaceCode }).IsUnique();
    }
}
