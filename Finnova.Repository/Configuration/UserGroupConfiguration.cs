using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class UserGroupConfiguration : IEntityTypeConfiguration<UserGroup>
{
    public void Configure(EntityTypeBuilder<UserGroup> b)
    {
        b.ToTable("user_groups");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserGroupCode).IsRequired().HasMaxLength(6);
        b.Property(x => x.Name).IsRequired().HasMaxLength(30);
        b.Property(x => x.IsActive).IsRequired();

        b.HasIndex(x => x.UserGroupCode).IsUnique();           // company-wide uniqueness (R2.4)
        b.HasIndex(x => x.Name);

        b.HasMany(x => x.Members).WithOne()
            .HasForeignKey(m => m.UserGroupId).OnDelete(DeleteBehavior.Cascade);
    }
}
