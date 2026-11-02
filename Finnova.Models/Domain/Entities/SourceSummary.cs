using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

public sealed class SourceSummary
{
    public VerificationSourceType SourceType { get; set; }
    public string? MappingProfile { get; set; }
    public bool DocumentProvided { get; set; }
    public string? DocumentContentType { get; set; }
    public long? DocumentSizeBytes { get; set; }
    public Guid? ProtectedDocumentReferenceId { get; set; }
    public string ExtractedValueKeysJson { get; set; } = "[]";
    public bool ExtractionCompleted { get; set; }
}
