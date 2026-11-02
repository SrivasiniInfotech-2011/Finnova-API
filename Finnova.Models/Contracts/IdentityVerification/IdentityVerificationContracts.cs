using Finnova.Models.Contracts.Common;
using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Contracts.IdentityVerification;

public sealed record ValidatePanRequest(string Pan, Guid? SessionId = null);
public sealed record ValidateAadhaarRequest(string AadhaarNumber, Guid? SessionId = null);
public sealed record ComparisonSourceContract(string? DocumentContentType, long? DocumentSizeBytes, Guid? ProtectedDocumentReferenceId, IReadOnlyDictionary<string, string>? ExtractedValues, string? MappingProfile, Guid? SessionId = null);
public sealed record VerifyPanNameRequest(string SubmittedName, ComparisonSourceContract Source);
public sealed record VerifyAadharAddressRequest(string SubmittedAddress, ComparisonSourceContract Source);
public sealed record VerificationWorkflowRequest(VerificationWorkflowType WorkflowType, WorkflowInputMode InputMode, string? Pan, string? PanSubmittedName, ComparisonSourceContract? PanSource, string? AadhaarNumber, string? AadhaarSubmittedAddress, ComparisonSourceContract? AadhaarSource);
public sealed record PanAndNameWorkflowRequest(string Pan, string PanSubmittedName, ComparisonSourceContract PanSource, WorkflowInputMode InputMode = WorkflowInputMode.Partial);
public sealed record AadhaarAndAddressWorkflowRequest(string AadhaarNumber, string AadhaarSubmittedAddress, ComparisonSourceContract AadhaarSource, WorkflowInputMode InputMode = WorkflowInputMode.Partial);
public sealed record FullVerificationWorkflowRequest(string Pan, string PanSubmittedName, ComparisonSourceContract PanSource, string AadhaarNumber, string AadhaarSubmittedAddress, ComparisonSourceContract AadhaarSource, WorkflowInputMode InputMode = WorkflowInputMode.Partial);
public sealed record ManualComparisonDecisionRequest(VerificationOperationType OperationType, ComparisonOutcome Outcome, Guid SourceRecordId, string SafeReason);
public sealed record ManualPanOverrideRequest(string Pan, string CardholderName, DateOnly DateOfBirth, Guid ProtectedDocumentReferenceId, string DocumentContentType, long DocumentSizeBytes, Guid? SourceRecordId = null);
public sealed record ManualAadhaarOverrideRequest(string AadhaarNumber, string CardholderName, string Address, Guid ProtectedDocumentReferenceId, string DocumentContentType, long DocumentSizeBytes, Guid? SourceRecordId = null);
public sealed record VerificationRecordQuery(string? Search, VerificationOperationType? OperationType, ValidationStatus? ValidationStatus, ComparisonOutcome? ComparisonOutcome, VerificationMethod? Method, Guid? SessionId, DateTime? StartUtc, DateTime? EndUtc, int Page = 1, int PageSize = 20);
public sealed record IdentifierValidationResult(VerificationOperationType OperationType, ValidationStatus ValidationStatus, Guid? RecordId, Guid? SessionId, string CorrelationId, DateTime TimestampUtc, string SafeMessage, bool ManualOverrideAvailable, string? MaskedSubjectReference);
public sealed record ComparisonVerificationResult(VerificationOperationType OperationType, ComparisonOutcome ComparisonOutcome, Guid? RecordId, Guid? SessionId, string CorrelationId, DateTime TimestampUtc, string SafeMessage, bool ManualOverrideAvailable, string? MaskedSubjectReference);
public sealed record WorkflowFailureSummary(VerificationOperationType OperationType, string Code, string SafeMessage);
public sealed record VerificationWorkflowResponse(Guid SessionId, string CorrelationId, DateTime TimestampUtc, IdentifierValidationResult? PanIdentifierResult, ComparisonVerificationResult? PanNameResult, IdentifierValidationResult? AadhaarIdentifierResult, ComparisonVerificationResult? AadhaarAddressResult, IReadOnlyList<WorkflowFailureSummary> PartialFailures);
public sealed record ValidationRecordResponse(Guid Id, Guid? SessionId, VerificationOperationType OperationType, ValidationStatus? ValidationStatus, ComparisonOutcome? ComparisonOutcome, VerificationMethod Method, DateTime CreatedUtc, string ActorReference, string? SafeReasonCode, string? SafeMessage, string? MaskedSubjectReference, bool HasProtectedDocument);
public sealed record ProtectedDocumentResponse(Stream Content, string ContentType, string FileName);
