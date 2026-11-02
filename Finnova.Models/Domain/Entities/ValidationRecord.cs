using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

public sealed class ValidationRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? SessionId { get; set; }
    public VerificationSession? Session { get; set; }
    public Guid? SourceRecordId { get; set; }
    public VerificationOperationType OperationType { get; set; }
    public ValidationStatus? ValidationStatus { get; set; }
    public ComparisonOutcome? ComparisonOutcome { get; set; }
    public VerificationMethod Method { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public string ActorReference { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string? SafeReasonCode { get; set; }
    public string? SafeMessage { get; set; }
    public string? MaskedSubjectReference { get; set; }
    public string? ProtectedSearchReference { get; set; }
    public string? DiagnosticReference { get; set; }
    public SourceSummary SourceSummary { get; set; } = new();
    public Guid? ProtectedDocumentReferenceId { get; set; }
    public ProtectedDocumentReference? ProtectedDocumentReference { get; set; }
}
