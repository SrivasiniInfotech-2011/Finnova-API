using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class EntityConfiguration : IEntityTypeConfiguration<EntityMaster>
{
    public void Configure(EntityTypeBuilder<EntityMaster> builder)
    {
        builder.ToTable("entities");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.EntityType).IsRequired();               // stored as int
        builder.Property(x => x.RegistrationIdentifier).HasMaxLength(50);
        builder.Property(x => x.ContactPerson).HasMaxLength(100);
        builder.Property(x => x.Email).HasMaxLength(100);
        builder.Property(x => x.Phone).HasMaxLength(20);
        builder.Property(x => x.AddressLine).HasMaxLength(200);
        builder.Property(x => x.Attributes).HasMaxLength(2000);        // nullable JSON bag
        builder.Property(x => x.IsActive).IsRequired();

        // Code uniqueness scoped per Entity Type (R2.1/2.2): the same code may recur across types
        // but is unique within a type. Default CI collation backs the case-insensitive rule; codes
        // are trimmed by the handler so stored values are canonical.
        builder.HasIndex(x => new { x.EntityType, x.Code }).IsUnique();
        // Query/order by Name (R3.10).
        builder.HasIndex(x => x.Name);
    }
}