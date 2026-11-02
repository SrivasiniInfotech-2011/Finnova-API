using Finnova.Models.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Finnova.Repository.Configuration;
public sealed class ProtectedDocumentReferenceConfiguration : IEntityTypeConfiguration<ProtectedDocumentReference>
{
    public void Configure(EntityTypeBuilder<ProtectedDocumentReference> b)
    {
        b.ToTable("protected_document_references"); b.HasKey(x => x.Id);
        b.Property(x => x.StorageKey).IsRequired().HasMaxLength(500); b.Property(x => x.ContentType).IsRequired().HasMaxLength(100);
        b.Property(x => x.ContentHash).IsRequired().HasMaxLength(128); b.Property(x => x.SizeBytes).IsRequired(); b.Property(x => x.CreatedUtc).IsRequired(); b.HasIndex(x => x.ContentHash);
    }
}
