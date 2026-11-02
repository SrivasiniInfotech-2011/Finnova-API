using Finnova.Models.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finnova.Repository.Configuration;

public class ValidationRecordConfiguration : IEntityTypeConfiguration<ValidationRecord>
{
    public void Configure(EntityTypeBuilder<ValidationRecord> builder)
    {
        builder.ToTable("validation_records");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ActorReference).IsRequired().HasMaxLength(200);
        builder.Property(x => x.CorrelationId).IsRequired().HasMaxLength(100);
        builder.Property(x => x.SafeReasonCode).HasMaxLength(100);
        builder.Property(x => x.SafeMessage).HasMaxLength(500);
        builder.Property(x => x.MaskedSubjectReference).HasMaxLength(100);
        builder.Property(x => x.ProtectedSearchReference).HasMaxLength(200);
        builder.Property(x => x.DiagnosticReference).HasMaxLength(100);
        builder.Property(x => x.CreatedUtc).IsRequired();

        // Index for history ordering
        builder.HasIndex(x => new { x.CreatedUtc, x.Id });

        // Index for filters
        builder.HasIndex(x => new { x.OperationType, x.ValidationStatus, x.ComparisonOutcome, x.Method });

        // Index for workflow reconstruction
        builder.HasIndex(x => new { x.SessionId, x.CreatedUtc });

        // Index for exact search
        builder.HasIndex(x => x.ProtectedSearchReference);

        // Relationship to VerificationSession (many-to-one, restricted delete)
        builder.HasOne(x => x.Session)
            .WithMany(x => x.Records)
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Relationship to ProtectedDocumentReference (many-to-one, restricted delete)
        builder.HasOne(x => x.ProtectedDocumentReference)
            .WithMany(x => x.ValidationRecords)
            .HasForeignKey(x => x.ProtectedDocumentReferenceId)
            .OnDelete(DeleteBehavior.Restrict);

        // Owned type configuration for SourceSummary
        builder.OwnsOne(x => x.SourceSummary, owned =>
        {
            owned.Property(x => x.SourceType).HasColumnName("SourceType");
            owned.Property(x => x.MappingProfile).HasMaxLength(200);
            owned.Property(x => x.DocumentProvided).IsRequired();
            owned.Property(x => x.DocumentContentType).HasMaxLength(100);
            owned.Property(x => x.DocumentSizeBytes);
            owned.Property(x => x.ProtectedDocumentReferenceId);
            owned.Property(x => x.ExtractedValueKeysJson).HasMaxLength(2000);
            owned.Property(x => x.ExtractionCompleted).IsRequired();
        });
    }
}
