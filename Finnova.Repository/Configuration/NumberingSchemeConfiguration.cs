using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class NumberingSchemeConfiguration : IEntityTypeConfiguration<NumberingScheme>
{
    public void Configure(EntityTypeBuilder<NumberingScheme> builder)
    {
        builder.ToTable("numbering_schemes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(30);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.DocumentType).IsRequired().HasMaxLength(50);
        builder.Property(x => x.FormatTemplate).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Prefix).HasMaxLength(20);
        builder.Property(x => x.Suffix).HasMaxLength(20);
        builder.Property(x => x.ResetRule).IsRequired();   // stored as int
        builder.Property(x => x.Scope).IsRequired();       // stored as int
        builder.Property(x => x.PeriodKey).HasMaxLength(16);
        builder.Property(x => x.IsActive).IsRequired();

        // Code uniqueness (R2.1/2.2). Default CI collation backs the case-insensitive rule; codes
        // are trimmed by the handler so stored values are canonical.
        builder.HasIndex(x => x.Code).IsUnique();
        // At most one scheme per (DocumentType, Scope, ScopeId) (R2.3/2.4). ScopeId null (Global)
        // participates in the composite key.
        builder.HasIndex(x => new { x.DocumentType, x.Scope, x.ScopeId }).IsUnique();
        // Query/order by Name (R4.7).
        builder.HasIndex(x => x.Name);
    }
}
