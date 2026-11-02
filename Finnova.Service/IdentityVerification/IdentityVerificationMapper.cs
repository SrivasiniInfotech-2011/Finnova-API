using Finnova.Models.Contracts.IdentityVerification;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Service.IdentityVerification.Adapters;

namespace Finnova.Service.IdentityVerification;

/// <summary>
/// Maps persisted identity-verification state to safe public contracts.
/// Provider payloads, document bytes, storage keys, and unmasked values are never
/// included in mapper output.
/// </summary>
public static class IdentityVerificationMapper
{
    public static IdentifierValidationResult ToIdentifierResult(ValidationRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new(
            record.OperationType,
            record.ValidationStatus
                ?? throw new InvalidOperationException("Identifier records require a validation status."),
            record.Id,
            record.SessionId,
            record.CorrelationId,
            record.CreatedUtc,
            record.SafeMessage ?? "Verification completed.",
            record.ValidationStatus == ValidationStatus.Unavailable,
            record.MaskedSubjectReference);
    }

    public static ComparisonVerificationResult ToComparisonResult(ValidationRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new(
            record.OperationType,
            record.ComparisonOutcome
                ?? throw new InvalidOperationException("Comparison records require a comparison outcome."),
            record.Id,
            record.SessionId,
            record.CorrelationId,
            record.CreatedUtc,
            record.SafeMessage ?? "Comparison completed.",
            record.ComparisonOutcome == ComparisonOutcome.NoUsableMatch,
            record.MaskedSubjectReference);
    }

    public static ValidationRecordResponse ToValidationRecordResponse(ValidationRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new(
            record.Id,
            record.SessionId,
            record.OperationType,
            record.ValidationStatus,
            record.ComparisonOutcome,
            record.Method,
            record.CreatedUtc,
            record.ActorReference,
            record.SafeReasonCode,
            record.SafeMessage,
            record.MaskedSubjectReference,
            HasProtectedDocument(record));
    }

    /// <summary>
    /// Returns only whether a protected document is available. It intentionally does
    /// not expose the document reference, storage key, content hash, or document bytes.
    /// </summary>
    public static bool HasProtectedDocument(ValidationRecord? record) =>
        record?.ProtectedDocumentReferenceId is not null;

    public static WorkflowFailureSummary ToWorkflowFailureSummary(ValidationRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new(
            record.OperationType,
            record.SafeReasonCode ?? DefaultFailureCode(record),
            record.SafeMessage ?? DefaultFailureMessage(record));
    }

    public static WorkflowFailureSummary ToWorkflowFailureSummary(
        VerificationOperationType operationType,
        string? code,
        string? safeMessage)
    {
        return new(
            operationType,
            string.IsNullOrWhiteSpace(code) ? "ERR-IVF-503" : code,
            string.IsNullOrWhiteSpace(safeMessage) ? "The verification operation could not be completed." : safeMessage);
    }

    /// <summary>
    /// Reconstructs a workflow response from the latest record for each operation in a
    /// session. A manual override is the latest result for its operation when present.
    /// </summary>
    public static VerificationWorkflowResponse ToWorkflowResponse(
        VerificationSession session,
        IReadOnlyList<WorkflowFailureSummary>? partialFailures = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        var latestRecords = (session.Records ?? Array.Empty<ValidationRecord>())
            .GroupBy(record => record.OperationType)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(record => record.CreatedUtc)
                    .ThenByDescending(record => record.Id)
                    .First());

        latestRecords.TryGetValue(VerificationOperationType.PanIdentifierValidation, out var panRecord);
        latestRecords.TryGetValue(VerificationOperationType.PanNameVerification, out var panNameRecord);
        latestRecords.TryGetValue(VerificationOperationType.AadhaarIdentifierValidation, out var aadhaarRecord);
        latestRecords.TryGetValue(VerificationOperationType.AadhaarAddressVerification, out var addressRecord);

        var failures = (partialFailures ?? Array.Empty<WorkflowFailureSummary>())
            .Concat(latestRecords.Values
                .Where(IsPartialFailure)
                .Select(ToWorkflowFailureSummary))
            .GroupBy(failure => new { failure.OperationType, failure.Code, failure.SafeMessage })
            .Select(group => group.First())
            .ToArray();

        return new(
            session.Id,
            session.CorrelationId,
            session.CreatedUtc,
            ToIdentifierResultOrNull(panRecord),
            ToComparisonResultOrNull(panNameRecord),
            ToIdentifierResultOrNull(aadhaarRecord),
            ToComparisonResultOrNull(addressRecord),
            failures);
    }

    public static VerificationWorkflowResponse ToWorkflowResponse(
        VerificationSession session,
        IdentifierValidationResult? panIdentifierResult,
        ComparisonVerificationResult? panNameResult,
        IdentifierValidationResult? aadhaarIdentifierResult,
        ComparisonVerificationResult? aadhaarAddressResult,
        IReadOnlyList<WorkflowFailureSummary>? partialFailures = null,
        DateTime? timestampUtc = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        return new(
            session.Id,
            session.CorrelationId,
            timestampUtc ?? session.CreatedUtc,
            panIdentifierResult,
            panNameResult,
            aadhaarIdentifierResult,
            aadhaarAddressResult,
            partialFailures ?? Array.Empty<WorkflowFailureSummary>());
    }

    public static ValidationRecord CreateIdentifierRecord(
        VerificationOperationType operationType,
        IdentifierAdapterResult result,
        string actor,
        string correlation,
        Guid? session,
        string? maskedSubjectReference)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new()
        {
            OperationType = operationType,
            ValidationStatus = result.Status,
            Method = VerificationMethod.Auto,
            ActorReference = actor,
            CorrelationId = correlation,
            SessionId = session,
            SafeReasonCode = result.SafeReasonCode,
            SafeMessage = result.SafeMessage,
            MaskedSubjectReference = maskedSubjectReference
        };
    }

    public static ValidationRecord CreateComparisonRecord(
        VerificationOperationType operationType,
        ComparisonAdapterResult result,
        string actor,
        string correlation,
        Guid? session,
        SourceSummary source,
        string? maskedSubjectReference)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(source);

        return new()
        {
            OperationType = operationType,
            ComparisonOutcome = result.Outcome,
            Method = VerificationMethod.Auto,
            ActorReference = actor,
            CorrelationId = correlation,
            SessionId = session,
            SafeReasonCode = result.SafeReasonCode,
            SafeMessage = result.SafeMessage,
            SourceSummary = source,
            MaskedSubjectReference = maskedSubjectReference
        };
    }

    private static IdentifierValidationResult? ToIdentifierResultOrNull(ValidationRecord? record) =>
        record?.ValidationStatus is not null ? ToIdentifierResult(record) : null;

    private static ComparisonVerificationResult? ToComparisonResultOrNull(ValidationRecord? record) =>
        record?.ComparisonOutcome is not null ? ToComparisonResult(record) : null;

    private static bool IsPartialFailure(ValidationRecord record) =>
        record.ValidationStatus is ValidationStatus.Failure or ValidationStatus.Unavailable ||
        record.ComparisonOutcome == ComparisonOutcome.NoUsableMatch;

    private static string DefaultFailureCode(ValidationRecord record) =>
        record.ValidationStatus == ValidationStatus.Failure
            ? "ERR-IVF-500"
            : "ERR-IVF-503";

    private static string DefaultFailureMessage(ValidationRecord record) =>
        record.ValidationStatus == ValidationStatus.Failure
            ? "The verification operation failed."
            : record.ComparisonOutcome == ComparisonOutcome.NoUsableMatch
                ? "The comparison could not produce a usable result."
                : "The verification provider is temporarily unavailable.";
}
