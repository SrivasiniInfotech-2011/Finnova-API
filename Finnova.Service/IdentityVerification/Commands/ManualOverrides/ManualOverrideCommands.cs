using Finnova.Models.Contracts.IdentityVerification;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.IdentityVerification.Correlation;
using MediatR;

namespace Finnova.Service.IdentityVerification.Commands.ManualOverrides;

public sealed record ManualPanOverrideCommand(
    string Pan,
    string SubmittedName,
    DateOnly DateOfBirth,
    Guid ProtectedDocumentReferenceId,
    string DocumentContentType,
    long DocumentSizeBytes,
    string ActorReference,
    Guid? SourceRecordId = null) : IRequest<IdentifierValidationResult>;

public sealed record ManualAadhaarOverrideCommand(
    string AadhaarNumber,
    string SubmittedName,
    string SubmittedAddress,
    Guid ProtectedDocumentReferenceId,
    string DocumentContentType,
    long DocumentSizeBytes,
    string ActorReference,
    Guid? SourceRecordId = null) : IRequest<IdentifierValidationResult>;

public sealed record ManualComparisonDecisionCommand(
    VerificationOperationType OperationType,
    ComparisonOutcome Outcome,
    Guid SourceRecordId,
    string SafeReason,
    string ActorReference) : IRequest<ComparisonVerificationResult>;

/// <summary>
/// Command names used by the manual-review controller. They intentionally remain
/// separate from the legacy command names so existing callers continue to work.
/// </summary>
public sealed record SubmitManualComparisonCommand(
    ManualComparisonDecisionRequest Request,
    string ActorReference) : IRequest<ComparisonVerificationResult>;

public sealed record SubmitManualPanCommand(
    ManualPanOverrideRequest Request,
    string ActorReference) : IRequest<IdentifierValidationResult>;

public sealed record SubmitManualAadhaarCommand(
    ManualAadhaarOverrideRequest Request,
    string ActorReference) : IRequest<IdentifierValidationResult>;

public sealed class SubmitManualComparisonHandler(
    IIdentityVerificationRepository repository,
    IRequestContextAccessor context)
    : IRequestHandler<SubmitManualComparisonCommand, ComparisonVerificationResult>
{
    public Task<ComparisonVerificationResult> Handle(SubmitManualComparisonCommand request, CancellationToken ct) =>
        ManualOverrideHandlerSupport.AppendComparisonAsync(
            repository,
            context,
            request.Request.OperationType,
            request.Request.Outcome,
            request.Request.SourceRecordId,
            request.ActorReference,
            ct);
}

public sealed class ManualComparisonDecisionHandler(
    IIdentityVerificationRepository repository,
    IRequestContextAccessor context)
    : IRequestHandler<ManualComparisonDecisionCommand, ComparisonVerificationResult>
{
    public Task<ComparisonVerificationResult> Handle(ManualComparisonDecisionCommand request, CancellationToken ct) =>
        ManualOverrideHandlerSupport.AppendComparisonAsync(
            repository,
            context,
            request.OperationType,
            request.Outcome,
            request.SourceRecordId,
            request.ActorReference,
            ct);
}

public sealed class SubmitManualPanHandler(
    IIdentityVerificationRepository repository,
    IRequestContextAccessor context)
    : IRequestHandler<SubmitManualPanCommand, IdentifierValidationResult>
{
    public Task<IdentifierValidationResult> Handle(SubmitManualPanCommand request, CancellationToken ct) =>
        ManualOverrideHandlerSupport.AppendPanAsync(
            repository,
            context,
            request.Request.Pan,
            request.Request.CardholderName,
            request.Request.DateOfBirth,
            request.Request.ProtectedDocumentReferenceId,
            request.Request.DocumentContentType,
            request.Request.DocumentSizeBytes,
            request.Request.SourceRecordId,
            request.ActorReference,
            ct);
}

public sealed class ManualPanOverrideHandler(
    IIdentityVerificationRepository repository,
    IRequestContextAccessor context)
    : IRequestHandler<ManualPanOverrideCommand, IdentifierValidationResult>
{
    public Task<IdentifierValidationResult> Handle(ManualPanOverrideCommand request, CancellationToken ct) =>
        ManualOverrideHandlerSupport.AppendPanAsync(
            repository,
            context,
            request.Pan,
            request.SubmittedName,
            request.DateOfBirth,
            request.ProtectedDocumentReferenceId,
            request.DocumentContentType,
            request.DocumentSizeBytes,
            request.SourceRecordId,
            request.ActorReference,
            ct);
}

public sealed class SubmitManualAadhaarHandler(
    IIdentityVerificationRepository repository,
    IRequestContextAccessor context)
    : IRequestHandler<SubmitManualAadhaarCommand, IdentifierValidationResult>
{
    public Task<IdentifierValidationResult> Handle(SubmitManualAadhaarCommand request, CancellationToken ct) =>
        ManualOverrideHandlerSupport.AppendAadhaarAsync(
            repository,
            context,
            request.Request.AadhaarNumber,
            request.Request.CardholderName,
            request.Request.Address,
            request.Request.ProtectedDocumentReferenceId,
            request.Request.DocumentContentType,
            request.Request.DocumentSizeBytes,
            request.Request.SourceRecordId,
            request.ActorReference,
            ct);
}

public sealed class ManualAadhaarOverrideHandler(
    IIdentityVerificationRepository repository,
    IRequestContextAccessor context)
    : IRequestHandler<ManualAadhaarOverrideCommand, IdentifierValidationResult>
{
    public Task<IdentifierValidationResult> Handle(ManualAadhaarOverrideCommand request, CancellationToken ct) =>
        ManualOverrideHandlerSupport.AppendAadhaarAsync(
            repository,
            context,
            request.AadhaarNumber,
            request.SubmittedName,
            request.SubmittedAddress,
            request.ProtectedDocumentReferenceId,
            request.DocumentContentType,
            request.DocumentSizeBytes,
            request.SourceRecordId,
            request.ActorReference,
            ct);
}

internal static class ManualOverrideHandlerSupport
{
    private const string ManualReasonCode = "ManualOverride";
    private const string ManualIdentifierMessage = "Manual identity verification recorded.";
    private const string ManualComparisonMessage = "Manual comparison decision recorded.";

    public static async Task<ComparisonVerificationResult> AppendComparisonAsync(
        IIdentityVerificationRepository repository,
        IRequestContextAccessor context,
        VerificationOperationType operationType,
        ComparisonOutcome outcome,
        Guid sourceRecordId,
        string actorReference,
        CancellationToken ct)
    {
        if (operationType is not (VerificationOperationType.PanNameVerification or VerificationOperationType.AadhaarAddressVerification) ||
            outcome is not (ComparisonOutcome.Match or ComparisonOutcome.NonMatch))
        {
            throw new IdentityVerificationValidationException();
        }

        var source = await repository.GetRecordAsync(sourceRecordId, ct)
            ?? throw new IdentityVerificationRecordNotFoundException();

        if (source.OperationType != operationType || source.ComparisonOutcome is null)
        {
            throw new IdentityVerificationValidationException();
        }

        var correlation = context.Current.CorrelationId;
        var record = new ValidationRecord
        {
            SessionId = source.SessionId,
            SourceRecordId = source.Id,
            CorrelationId = correlation,
            ActorReference = actorReference,
            OperationType = operationType,
            ComparisonOutcome = outcome,
            Method = VerificationMethod.ManualOverride,
            SafeReasonCode = ManualReasonCode,
            SafeMessage = ManualComparisonMessage,
            MaskedSubjectReference = source.MaskedSubjectReference,
            ProtectedDocumentReferenceId = source.ProtectedDocumentReferenceId,
            SourceSummary = CopySourceSummary(source.SourceSummary)
        };

        await repository.AppendRecordAsync(record, ct);
        return ToComparisonResult(record);
    }

    public static async Task<IdentifierValidationResult> AppendPanAsync(
        IIdentityVerificationRepository repository,
        IRequestContextAccessor context,
        string pan,
        string submittedName,
        DateOnly dateOfBirth,
        Guid protectedDocumentReferenceId,
        string documentContentType,
        long documentSizeBytes,
        Guid? sourceRecordId,
        string actorReference,
        CancellationToken ct)
    {
        var normalizedPan = IdentityVerificationRules.NormalizePan(pan);
        ValidateManualIdentityInput(submittedName, dateOfBirth);
        var source = await GetIdentifierSourceAsync(
            repository,
            sourceRecordId,
            VerificationOperationType.PanIdentifierValidation,
            ct);
        await ValidateProtectedDocumentAsync(
            repository,
            protectedDocumentReferenceId,
            documentContentType,
            documentSizeBytes,
            ct);

        var record = CreateIdentifierRecord(
            VerificationOperationType.PanIdentifierValidation,
            normalizedPan,
            protectedDocumentReferenceId,
            documentContentType,
            documentSizeBytes,
            source,
            context.Current,
            actorReference);

        await repository.AppendRecordAsync(record, ct);
        return ToIdentifierResult(record);
    }

    public static async Task<IdentifierValidationResult> AppendAadhaarAsync(
        IIdentityVerificationRepository repository,
        IRequestContextAccessor context,
        string aadhaarNumber,
        string submittedName,
        string submittedAddress,
        Guid protectedDocumentReferenceId,
        string documentContentType,
        long documentSizeBytes,
        Guid? sourceRecordId,
        string actorReference,
        CancellationToken ct)
    {
        var normalizedAadhaar = IdentityVerificationRules.NormalizeAadhaar(aadhaarNumber);
        IdentityVerificationRules.RequireText(submittedName, "CardholderName");
        IdentityVerificationRules.RequireText(submittedAddress, "Address");
        var source = await GetIdentifierSourceAsync(
            repository,
            sourceRecordId,
            VerificationOperationType.AadhaarIdentifierValidation,
            ct);
        await ValidateProtectedDocumentAsync(
            repository,
            protectedDocumentReferenceId,
            documentContentType,
            documentSizeBytes,
            ct);

        var record = CreateIdentifierRecord(
            VerificationOperationType.AadhaarIdentifierValidation,
            normalizedAadhaar,
            protectedDocumentReferenceId,
            documentContentType,
            documentSizeBytes,
            source,
            context.Current,
            actorReference);

        await repository.AppendRecordAsync(record, ct);
        return ToIdentifierResult(record);
    }

    private static async Task<ValidationRecord?> GetIdentifierSourceAsync(
        IIdentityVerificationRepository repository,
        Guid? sourceRecordId,
        VerificationOperationType operationType,
        CancellationToken ct)
    {
        if (sourceRecordId is null)
        {
            return null;
        }

        var source = await repository.GetRecordAsync(sourceRecordId.Value, ct)
            ?? throw new IdentityVerificationRecordNotFoundException();

        if (source.OperationType != operationType || source.ValidationStatus is null)
        {
            throw new IdentityVerificationValidationException();
        }

        return source;
    }

    private static async Task ValidateProtectedDocumentAsync(
        IIdentityVerificationRepository repository,
        Guid referenceId,
        string contentType,
        long sizeBytes,
        CancellationToken ct)
    {
        var reference = await repository.GetProtectedDocumentReferenceAsync(referenceId, ct)
            ?? throw new IdentityVerificationRecordNotFoundException();

        if (!IdentityVerificationRules.IsSupportedContentType(contentType) ||
            !string.Equals(reference.ContentType, contentType, StringComparison.OrdinalIgnoreCase) ||
            reference.SizeBytes != sizeBytes ||
            sizeBytes is < 1 or > 5 * 1024 * 1024)
        {
            throw new IdentityVerificationValidationException();
        }
    }

    private static void ValidateManualIdentityInput(string submittedName, DateOnly dateOfBirth)
    {
        IdentityVerificationRules.RequireText(submittedName, "CardholderName");
        if (dateOfBirth == DateOnly.MinValue || dateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new IdentityVerificationValidationException();
        }
    }

    private static ValidationRecord CreateIdentifierRecord(
        VerificationOperationType operationType,
        string normalizedIdentifier,
        Guid protectedDocumentReferenceId,
        string documentContentType,
        long documentSizeBytes,
        ValidationRecord? source,
        RequestContext context,
        string actorReference) => new()
        {
            SessionId = source?.SessionId,
            SourceRecordId = source?.Id,
            CorrelationId = context.CorrelationId,
            ActorReference = actorReference,
            OperationType = operationType,
            ValidationStatus = ValidationStatus.Success,
            Method = VerificationMethod.ManualOverride,
            SafeReasonCode = ManualReasonCode,
            SafeMessage = ManualIdentifierMessage,
            MaskedSubjectReference = IdentityVerificationRules.MaskIdentifier(normalizedIdentifier),
            ProtectedDocumentReferenceId = protectedDocumentReferenceId,
            SourceSummary = new SourceSummary
            {
                SourceType = VerificationSourceType.DocumentFile,
                DocumentProvided = true,
                DocumentContentType = documentContentType,
                DocumentSizeBytes = documentSizeBytes,
                ProtectedDocumentReferenceId = protectedDocumentReferenceId
            }
        };

    private static SourceSummary CopySourceSummary(SourceSummary source) => new()
    {
        SourceType = source.SourceType,
        MappingProfile = source.MappingProfile,
        DocumentProvided = source.DocumentProvided,
        DocumentContentType = source.DocumentContentType,
        DocumentSizeBytes = source.DocumentSizeBytes,
        ProtectedDocumentReferenceId = source.ProtectedDocumentReferenceId,
        ExtractedValueKeysJson = source.ExtractedValueKeysJson,
        ExtractionCompleted = source.ExtractionCompleted
    };

    private static IdentifierValidationResult ToIdentifierResult(ValidationRecord record) =>
        new(
            record.OperationType,
            record.ValidationStatus!.Value,
            record.Id,
            record.SessionId,
            record.CorrelationId,
            record.CreatedUtc,
            record.SafeMessage ?? ManualIdentifierMessage,
            false,
            record.MaskedSubjectReference);

    private static ComparisonVerificationResult ToComparisonResult(ValidationRecord record) =>
        new(
            record.OperationType,
            record.ComparisonOutcome!.Value,
            record.Id,
            record.SessionId,
            record.CorrelationId,
            record.CreatedUtc,
            record.SafeMessage ?? ManualComparisonMessage,
            false,
            record.MaskedSubjectReference);
}
