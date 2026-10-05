using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class DraweeBankConfiguration : IEntityTypeConfiguration<DraweeBank>
{
    public void Configure(EntityTypeBuilder<DraweeBank> builder)
    {
        builder.ToTable("drawee_banks");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.BankCode).IsRequired().HasMaxLength(20);
        builder.Property(x => x.BankName).IsRequired().HasMaxLength(150);
        builder.Property(x => x.IsActive).IsRequired();

        // Case-insensitive uniqueness of BankCode (R2). SQL Server default CI collation backs the
        // index; codes are trimmed by the handler so stored values are canonical.
        builder.HasIndex(x => x.BankCode).IsUnique();
        builder.HasIndex(x => x.BankName);                       // search/order path (R8.10)

        // Owned branches + optional restriction (cascade so update-removal deletes the branch, R3.7).
        builder.HasMany(x => x.Branches)
               .WithOne()
               .HasForeignKey(b => b.DraweeBankId)
               .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Restriction)
               .WithOne()
               .HasForeignKey<RestrictionDetail>(r => r.DraweeBankId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
