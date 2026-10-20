using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> b)
    {
        b.ToTable("document_master");
        b.HasKey(x => x.Id);

        b.Property(x => x.DocCode).IsRequired().HasMaxLength(20);
        b.Property(x => x.DocName).IsRequired().HasMaxLength(150);
        b.Property(x => x.DocShortName).HasMaxLength(100);
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.DocCategory).IsRequired().HasMaxLength(50);
        b.Property(x => x.ApplicableEntity).IsRequired().HasMaxLength(50);

        b.Property(x => x.ValidationEngine).IsRequired().HasMaxLength(50);
        b.Property(x => x.FormatRegex).HasMaxLength(500);
        b.Property(x => x.MaskPattern).HasMaxLength(200);
        b.Property(x => x.MaxFileSizeMb).IsRequired();
        b.Property(x => x.AllowedMimeTypes).IsRequired().HasMaxLength(100);

        b.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.MakerId).HasMaxLength(100);
        b.Property(x => x.CheckerId).HasMaxLength(100);
        b.Property(x => x.RejectionReason).HasMaxLength(500);
        b.Property(x => x.PendingValuesJson);   // nvarchar(max)

        // DocCode uniqueness (R2.1/2.2). Default CI collation backs the case-insensitive rule;
        // the handler trims so stored values are canonical.
        b.HasIndex(x => x.DocCode).IsUnique();
        // Query/filter paths (R10): status + category filters, DocName substring order tie-break.
        b.HasIndex(x => new { x.Status, x.DocCategory });
        b.HasIndex(x => x.DocName);

        b.HasMany(x => x.ApplicabilityRules)
            .WithOne(r => r.Document!)
            .HasForeignKey(r => r.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class DocumentApplicabilityRuleConfiguration : IEntityTypeConfiguration<DocumentApplicabilityRule>
{
    public void Configure(EntityTypeBuilder<DocumentApplicabilityRule> b)
    {
        b.ToTable("document_applicability_rules");
        b.HasKey(x => x.Id);
        b.Property(x => x.Constitution).IsRequired().HasMaxLength(50);
        b.Property(x => x.EntityRole).IsRequired().HasMaxLength(50);
        b.Property(x => x.IsMandatory).IsRequired();

        // Unique applicability combination per document (R4.4).
        b.HasIndex(x => new { x.DocumentId, x.Constitution, x.EntityRole }).IsUnique();
    }
}

public class DocumentAuditEntryConfiguration : IEntityTypeConfiguration<DocumentAuditEntry>
{
    public void Configure(EntityTypeBuilder<DocumentAuditEntry> b)
    {
        b.ToTable("document_audit_entries");
        b.HasKey(x => x.Id);
        b.Property(x => x.Action).IsRequired().HasConversion<string>().HasMaxLength(40);
        b.Property(x => x.Summary).IsRequired().HasMaxLength(400);
        b.Property(x => x.Remarks).HasMaxLength(500);
        b.Property(x => x.ChangedBy).IsRequired().HasMaxLength(100);
        b.Property(x => x.ChangedAtUtc).IsRequired();

        // Newest-first reads with a deterministic id tie-break (R8.4).
        b.HasIndex(x => new { x.DocumentId, x.ChangedAtUtc });
    }
}