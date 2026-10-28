using Finnova.Models.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finnova.Repository.Configuration;

public sealed class CustomerDocumentReferenceConfiguration : IEntityTypeConfiguration<CustomerDocumentReference>
{
    public void Configure(EntityTypeBuilder<CustomerDocumentReference> b)
    {
        b.ToTable("CustomerDocumentReferences"); b.HasKey(x => x.Id); b.Property(x => x.ConstitutionCode).HasMaxLength(50).IsRequired();
        b.Property(x => x.DocumentCode).HasMaxLength(20).IsRequired(); b.Property(x => x.DocumentCategory).HasMaxLength(100);
        b.Property(x => x.DocumentDescription).HasMaxLength(500); b.Property(x => x.Remarks).HasMaxLength(1000);
        b.Property(x => x.Value).HasMaxLength(200); b.Property(x => x.ValueSource).HasMaxLength(50);
        b.Property(x => x.DmsReference).HasMaxLength(500); b.Property(x => x.ViewMetadata).HasMaxLength(2000);
        b.HasIndex(x => new { x.CustomerId, x.DocumentDefinitionId }).IsUnique(); b.HasIndex(x => x.CustomerId);
        // No byte[], varbinary, raw image, or duplicated Document definition columns.
    }
}
