using Finnova.Models.Domain.Enums;

namespace Finnova.Service.IdentityVerification.Adapters;

/// <summary>
/// Provider-neutral input for identifier validation. The actor and cancellation token
/// are execution context only; provider payload fields are owned by the adapter.
/// </summary>
public sealed record IdentifierValidationInput
{
    public IdentifierValidationInput()
    {
    }

    public IdentifierValidationInput(
        VerificationOperationType operationType,
        string identifier,
        string correlationId)
    {
        OperationType = operationType;
        Identifier = identifier;
        CorrelationId = correlationId;
    }

    public VerificationOperationType OperationType { get; init; }
    public string Identifier { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;
    public string ActorReference { get; init; } = string.Empty;
    public CancellationToken CancellationToken { get; init; }
}

/// <summary>
/// Normalized result returned by an identifier adapter.
/// </summary>
public abstract class IdentifierAdapterResult
{
    protected IdentifierAdapterResult(
        ValidationStatus status,
        string? safeReasonCode,
        string? safeMessage,
        string? providerReference = null)
    {
        Status = status;
        SafeReasonCode = safeReasonCode;
        SafeMessage = safeMessage;
        ProviderReference = providerReference;
    }

    public ValidationStatus Status { get; }
    public string? SafeReasonCode { get; }
    public string? SafeMessage { get; }

    /// <summary>
    /// An opaque, safe reference only. Raw provider payloads and provider credentials
    /// must never be placed in this result.
    /// </summary>
    public string? ProviderReference { get; }

    public sealed class Success : IdentifierAdapterResult
    {
        public Success(string? providerReference = null, string? safeMessage = null)
            : base(ValidationStatus.Success, null, safeMessage, providerReference)
        {
        }
    }

    public sealed class Failure : IdentifierAdapterResult
    {
        public Failure(string safeReasonCode, string safeMessage)
            : base(ValidationStatus.Failure, safeReasonCode, safeMessage)
        {
        }
    }

    public sealed class Unavailable : IdentifierAdapterResult
    {
        public Unavailable(string safeReasonCode, string safeMessage)
            : base(ValidationStatus.Unavailable, safeReasonCode, safeMessage)
        {
        }
    }
}

/// <summary>
/// Provider-neutral input for comparison operations.
/// The document reference is an opaque domain reference, not a storage key or URL.
/// The stream is borrowed from the caller for the duration of the adapter call.
/// Adapters must not dispose, replace, or retain it after VerifyAsync returns.
/// </summary>
public sealed record ProviderNeutralComparisonInput
{
    public ProviderNeutralComparisonInput()
    {
    }

    public ProviderNeutralComparisonInput(
        VerificationOperationType operationType,
        string submittedValue,
        Guid? protectedDocumentReferenceId,
        Stream? protectedDocumentStream,
        IReadOnlyDictionary<string, string> extractedValues,
        string correlationId,
        string mappingProfile)
    {
        OperationType = operationType;
        SubmittedValue = submittedValue;
        ProtectedDocumentReferenceId = protectedDocumentReferenceId;
        ProtectedDocumentStream = protectedDocumentStream;
        ExtractedValues = extractedValues ?? new Dictionary<string, string>();
        CorrelationId = correlationId;
        MappingProfile = mappingProfile;
    }

    public VerificationOperationType OperationType { get; init; }
    public string SubmittedValue { get; init; } = string.Empty;
    public Guid? ProtectedDocumentReferenceId { get; init; }
    public Stream? ProtectedDocumentStream { get; init; }
    public IReadOnlyDictionary<string, string> ExtractedValues { get; init; } = new Dictionary<string, string>();
    public string CorrelationId { get; init; } = string.Empty;
    public string MappingProfile { get; init; } = string.Empty;

    /// <summary>
    /// Execution context retained for existing adapter implementations. It is not a
    /// provider field and must not be serialized into a provider payload unless the
    /// selected integration explicitly requires a safe actor header.
    /// </summary>
    public string ActorReference { get; init; } = string.Empty;
}

/// <summary>
/// Normalized result returned by a comparison adapter.
/// Comparison outages and unusable source data are represented as NoUsableMatch,
/// because the domain comparison enum has no provider-availability state.
/// </summary>
public abstract class ComparisonAdapterResult
{
    protected ComparisonAdapterResult(
        ComparisonOutcome outcome,
        string? safeReasonCode,
        string? safeMessage)
    {
        Outcome = outcome;
        SafeReasonCode = safeReasonCode;
        SafeMessage = safeMessage;
    }

    public ComparisonOutcome Outcome { get; }
    public string? SafeReasonCode { get; }
    public string? SafeMessage { get; }

    public sealed class Match : ComparisonAdapterResult
    {
        public Match(string? safeMessage = null)
            : base(ComparisonOutcome.Match, null, safeMessage)
        {
        }
    }

    public sealed class NonMatch : ComparisonAdapterResult
    {
        public NonMatch(string? safeMessage = null)
            : base(ComparisonOutcome.NonMatch, null, safeMessage)
        {
        }

        public NonMatch(string safeReasonCode, string safeMessage)
            : base(ComparisonOutcome.NonMatch, safeReasonCode, safeMessage)
        {
        }
    }

    public sealed class NoUsableMatch : ComparisonAdapterResult
    {
        public NoUsableMatch(string safeReasonCode, string safeMessage)
            : base(ComparisonOutcome.NoUsableMatch, safeReasonCode, safeMessage)
        {
        }
    }

    /// <summary>
    /// Compatibility factory for callers that classify provider unavailability before
    /// normalization. It deliberately emits NoUsableMatch, never a domain Match.
    /// </summary>
    public sealed class Unavailable : ComparisonAdapterResult
    {
        public Unavailable(string safeReasonCode, string safeMessage)
            : base(ComparisonOutcome.NoUsableMatch, safeReasonCode, safeMessage)
        {
        }
    }
}

public interface IIdentifierValidationAdapter
{
    VerificationOperationType SupportedOperation { get; }

    Task<IdentifierAdapterResult> ValidateAsync(
        IdentifierValidationInput input,
        CancellationToken ct);
}

public interface IComparisonAdapter
{
    VerificationOperationType SupportedOperation { get; }

    Task<ComparisonAdapterResult> VerifyAsync(
        ProviderNeutralComparisonInput input,
        CancellationToken ct);
}

/// <summary>
/// Resolves a comparison adapter using the mapping-profile/adapter-name string supplied
/// by the comparison command handlers. Provider option objects remain inside the resolver
/// and selected adapter; they do not cross the neutral adapter boundary.
/// </summary>
public interface IComparisonAdapterResolver
{
    IComparisonAdapter Resolve(
        VerificationOperationType operation,
        string adapterName);
}
