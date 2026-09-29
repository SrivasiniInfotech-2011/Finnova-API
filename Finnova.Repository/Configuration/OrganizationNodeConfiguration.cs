using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class OrganizationNodeConfiguration : IEntityTypeConfiguration<OrganizationNode>
{
    public void Configure(EntityTypeBuilder<OrganizationNode> builder)
    {
        builder.ToTable("organization_nodes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(150);
        builder.Property(x => x.Level).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();
        // Uniqueness of Code (R2.1/2.2). SQL Server default collation (e.g. SQL_Latin1_General_CP1_CI_AS)
        // is case-insensitive, so the unique index backs the case-insensitive rule. Codes are trimmed
        // by the handler before persistence so stored values are canonical.
        builder.HasIndex(x => x.Code).IsUnique();
        // Query path: search/order by Name (R5.9).
        builder.HasIndex(x => x.Name);
        // Children lookup and cycle/descendant walks read by ParentId (R5.10, R3.2, R8.2).
        builder.HasIndex(x => x.ParentId);
        // Self-referencing parent relationship. NO cascade delete at the DB level — leaf-only delete is
        // enforced in the service (R8.2), so the DB must never silently remove a subtree.
        builder.HasOne<OrganizationNode>()
               .WithMany()
               .HasForeignKey(x => x.ParentId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}