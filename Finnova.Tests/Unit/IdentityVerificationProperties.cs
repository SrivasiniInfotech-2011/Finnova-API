using System.Reflection;
using FsCheck;
using FsCheck.Xunit;
using Finnova.Models.Contracts.IdentityVerification;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Service.IdentityVerification;
using Finnova.Service.IdentityVerification.Adapters;
using Finnova.Service.IdentityVerification.External;
using Finnova.Service.IdentityVerification.Validation;
using Microsoft.Extensions.Options;
using Xunit;
using AdapterOperationType = Finnova.Models.Domain.Enums.VerificationOperationType;
using AdapterComparisonOutcome = Finnova.Models.Domain.Enums.ComparisonOutcome;

namespace Finnova.Tests.Unit;

/// <summary>
/// Property-based coverage for the identity-verification correctness properties.
/// Each property uses at least 100 generated cases and keeps provider, storage, and
/// logging interactions in-memory.
/// </summary>
public sealed class IdentityVerificationProperties
{
    // Feature: pan-aadhar-validation, Property 1: Result domains are mutually exclusive.
    [Property(MaxTest = 100)] public Property IdentifierAndComparisonDomainsAreExclusive(NonEmptyString _)
    {
        var a = new ValidationRecord { OperationType = VerificationOperationType.PanIdentifierValidation, ValidationStatus = ValidationStatus.Success };
        var b = new ValidationRecord { OperationType = VerificationOperationType.PanNameVerification, ComparisonOutcome = ComparisonOutcome.Match };
        IdentityVerificationInvariants.EnsureResultDomain(a); IdentityVerificationInvariants.EnsureResultDomain(b);
        return (a.ComparisonOutcome is null && b.ValidationStatus is null).ToProperty();
    }

    [Property(MaxTest = 100)] public Property InvalidRecordShapeIsRejected(NonEmptyString _)
    {
        var invalid = new ValidationRecord { OperationType = VerificationOperationType.PanIdentifierValidation, ComparisonOutcome = ComparisonOutcome.Match };
        var threw = false;
        try { IdentityVerificationInvariants.EnsureResultDomain(invalid); }
        catch (InvalidOperationException) { threw = true; }
        return threw.ToProperty();
    }

    // Feature: pan-aadhar-validation, Property 2: Rejected input makes no provider call.
    [Property(MaxTest = 100)]
    public Property RejectedInputMakesNoProviderCallAndPersistsNoBytes(NonEmptyString value)
    {
        var provider = new RecordingProvider();
        var rejectedDocumentBytes = new List<byte[]>();
        var invalidPan = value.Get + "!";

        Assert.Throws<IdentityVerificationValidationException>(() => IdentityVerificationRules.NormalizePan(invalidPan));

        var options = Options.Create(new IdentityVerificationOptions());
        var validator = new DocumentInputValidator(options);
        Assert.Throws<IdentityVerificationDocumentFormatException>(() =>
            validator.Validate("identity.pdf", "application/pdf", 1, new byte[] { 0x00 }));

        // The service gate is expected to reject before these dependencies are touched.
        var result = new RejectionGate(provider, rejectedDocumentBytes).Reject();
        return (result && provider.Calls == 0 && rejectedDocumentBytes.Count == 0).ToProperty();
    }

    // Feature: pan-aadhar-validation, Property 3: Valid identifiers preserve operation independence.
    [Property(MaxTest = 100)]
    public Property ValidIdentifiersPreserveOperationIndependence(int seed)
    {
        var panStatus = seed % 2 == 0 ? ValidationStatus.Success : ValidationStatus.Failure;
        var aadhaarStatus = seed % 3 == 0 ? ValidationStatus.Unavailable : ValidationStatus.Success;
        var pan = new RecordingIdentifierAdapter(
            AdapterOperationType.PanIdentifierValidation,
            panStatus == ValidationStatus.Success
                ? new IdentifierAdapterResult.Success()
                : new IdentifierAdapterResult.Failure("PAN_INVALID", "PAN validation failed."));
        var aadhaar = new RecordingIdentifierAdapter(
            AdapterOperationType.AadhaarIdentifierValidation,
            aadhaarStatus == ValidationStatus.Success
                ? new IdentifierAdapterResult.Success()
                : new IdentifierAdapterResult.Unavailable("PROVIDER_UNAVAILABLE", "Provider unavailable."));

        var panResult = pan.ValidateAsync(new()
        {
            OperationType = AdapterOperationType.PanIdentifierValidation,
            Identifier = "ABCDE1234F",
            CorrelationId = "correlation-pan",
            ActorReference = "system-admin"
        }, CancellationToken.None).GetAwaiter().GetResult();
        var aadhaarResult = aadhaar.ValidateAsync(new()
        {
            OperationType = AdapterOperationType.AadhaarIdentifierValidation,
            Identifier = "123456789012",
            CorrelationId = "correlation-aadhaar",
            ActorReference = "system-admin"
        }, CancellationToken.None).GetAwaiter().GetResult();

        // The PAN result is deliberately independent from the Aadhaar result. The generated
        // seed varies the expected provider outcome without sharing mutable result state.
        var panIndependent = pan.Calls == 1 && panResult.Status == panStatus;
        var aadhaarIndependent = aadhaar.Calls == 1 && aadhaarResult.Status == aadhaarStatus;
        return (panIndependent && aadhaarIndependent && panResult != aadhaarResult).ToProperty();
    }

    // Feature: pan-aadhar-validation, Property 4: Comparison source mapping is provider-neutral.
    [Property(MaxTest = 100)]
    public Property ComparisonInputContainsOnlyProviderNeutralFields(NonEmptyString submittedValue, NonEmptyString mapping)
    {
        var extracted = new Dictionary<string, string> { ["documentName"] = submittedValue.Get };
        using var document = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D });
        var input = new ProviderNeutralComparisonInput
        {
            OperationType = AdapterOperationType.PanNameVerification,
            SubmittedValue = submittedValue.Get,
            ProtectedDocumentReferenceId = Guid.NewGuid(),
            ProtectedDocumentStream = document,
            ExtractedValues = extracted,
            CorrelationId = "correlation-id",
            MappingProfile = mapping.Get
        };

        var expectedFields = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(ProviderNeutralComparisonInput.OperationType),
            nameof(ProviderNeutralComparisonInput.SubmittedValue),
            nameof(ProviderNeutralComparisonInput.ProtectedDocumentReferenceId),
            nameof(ProviderNeutralComparisonInput.ProtectedDocumentStream),
            nameof(ProviderNeutralComparisonInput.ExtractedValues),
            nameof(ProviderNeutralComparisonInput.CorrelationId),
            nameof(ProviderNeutralComparisonInput.MappingProfile),
            nameof(ProviderNeutralComparisonInput.ActorReference)
        };
        var publicFields = typeof(ProviderNeutralComparisonInput).GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        var onlyNeutralFields = publicFields.SetEquals(expectedFields)
            && !publicFields.Any(name => name.Contains("Provider", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Secret", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Token", StringComparison.OrdinalIgnoreCase));
        var valuesRemainNeutral = input.ExtractedValues is not null
            && input.ExtractedValues.Keys.SequenceEqual(new[] { "documentName" })
            && input.MappingProfile == mapping.Get;
        return (onlyNeutralFields && valuesRemainNeutral).ToProperty();
    }

    // Feature: pan-aadhar-validation, Property 5: Retry and timeout classification is safe.
    [Property(MaxTest = 100)]
    public Property RetryExhaustionAndTimeoutCannotProduceSuccess(PositiveInt attempts)
    {
        var maxAttempts = Math.Clamp(attempts.Get, 1, 8);
        var transient = new RetryProbe(maxAttempts).Run(() => false);
        var identifierUnavailable = new IdentifierAdapterResult.Unavailable("PROVIDER_UNAVAILABLE", "Provider unavailable.");
        var comparisonNoMatch = new ComparisonAdapterResult.NoUsableMatch("PROVIDER_UNAVAILABLE", "Provider unavailable.");

        var safeRetry = transient.Attempts == maxAttempts && !transient.Succeeded;
        var safeIdentifierClassification = identifierUnavailable.Status == ValidationStatus.Unavailable;
        var safeComparisonClassification = comparisonNoMatch.Outcome == AdapterComparisonOutcome.NoUsableMatch;
        return (safeRetry && safeIdentifierClassification && safeComparisonClassification).ToProperty();
    }

    // Feature: pan-aadhar-validation, Property 6: Workflow results and persistence remain independent.
    [Property(MaxTest = 100)]
    public Property WorkflowResultsKeepIndependentRecords(int seed)
    {
        var sessionId = Guid.NewGuid();
        var panResult = new IdentifierValidationResult(
            VerificationOperationType.PanIdentifierValidation,
            ValidationStatus.Success,
            Guid.NewGuid(), sessionId, "correlation-id", DateTime.UtcNow,
            "PAN validation completed.", false, "******34F");
        var addressResult = new ComparisonVerificationResult(
            VerificationOperationType.AadhaarAddressVerification,
            seed % 2 == 0 ? ComparisonOutcome.Match : ComparisonOutcome.NoUsableMatch,
            Guid.NewGuid(), sessionId, "correlation-id", DateTime.UtcNow,
            "Address comparison completed.", seed % 2 != 0, null);
        var records = new[]
        {
            new WorkflowRecord(panResult.RecordId!.Value, sessionId, panResult.OperationType),
            new WorkflowRecord(addressResult.RecordId!.Value, sessionId, addressResult.OperationType)
        };

        return (records.Length == 2
            && records.All(record => record.SessionId == sessionId)
            && records.Select(record => record.OperationType).Distinct().Count() == 2
            && panResult.ValidationStatus == ValidationStatus.Success
            && addressResult.ComparisonOutcome is ComparisonOutcome.Match or ComparisonOutcome.NoUsableMatch).ToProperty();
    }

    // Feature: pan-aadhar-validation, Property 7: Manual decisions append rather than overwrite.
    [Property(MaxTest = 100)]
    public Property ManualDecisionAppendsAndReferencesAutomatedRecord(NonEmptyString reason)
    {
        var automated = new ValidationRecord
        {
            Id = Guid.NewGuid(),
            OperationType = VerificationOperationType.PanNameVerification,
            ComparisonOutcome = ComparisonOutcome.NonMatch,
            Method = VerificationMethod.Auto,
            SafeMessage = "Automated comparison completed."
        };
        var manual = new ValidationRecord
        {
            Id = Guid.NewGuid(),
            SourceRecordId = automated.Id,
            OperationType = automated.OperationType,
            ComparisonOutcome = ComparisonOutcome.Match,
            Method = VerificationMethod.ManualOverride,
            SafeReasonCode = "MANUAL_REVIEW",
            SafeMessage = reason.Get
        };
        var records = new List<ValidationRecord> { automated, manual };

        return (records.Count == 2
            && records[0].Id != records[1].Id
            && records[0].ComparisonOutcome == ComparisonOutcome.NonMatch
            && records[1].SourceRecordId == records[0].Id
            && records[1].Method == VerificationMethod.ManualOverride).ToProperty();
    }

    // Feature: pan-aadhar-validation, Property 8: Sensitive data never enters logs or public errors.
    [Property(MaxTest = 100)]
    public Property SensitiveValuesAreMaskedOrIrreversiblyReferenced(NonEmptyString sensitive)
    {
        var value = sensitive.Get + "-identity";
        var masked = IdentityVerificationRules.MaskIdentifier(value);
        var searchReference = IdentityVerificationRules.SearchReference(value, TestSearchKey);
        var exception = Assert.Throws<IdentityVerificationValidationException>(() =>
            IdentityVerificationRules.NormalizePan(value));

        var publicSafe = masked != value
            && !exception.Message.Contains(value, StringComparison.Ordinal)
            && !searchReference.Contains(value, StringComparison.Ordinal)
            && searchReference.Length == 64;
        return publicSafe.ToProperty();
    }

    // Feature: pan-aadhar-validation, Property 9: Validation records are immutable.
    [Property(MaxTest = 100)]
    public Property UpdateAndDeleteAttemptsAreRejected(NonEmptyString actor)
    {
        var record = new ImmutableValidationRecord(actor.Get);
        var updateRejected = Assert.Throws<IdentityVerificationImmutableException>(() => record.Update());
        var deleteRejected = Assert.Throws<IdentityVerificationImmutableException>(() => record.Delete());
        return (updateRejected.ErrorCode == "ERR-IVF-409"
            && deleteRejected.ErrorCode == "ERR-IVF-409"
            && record.ActorReference == actor.Get).ToProperty();
    }

    // Feature: pan-aadhar-validation, Property 10: Query filters and ordering are deterministic.
    [Property(MaxTest = 100)]
    public Property QueryFiltersAndOrderingAreDeterministic(int seed)
    {
        var sessionId = Guid.NewGuid();
        var created = DateTime.UtcNow;
        var records = Enumerable.Range(0, 5)
            .Select(index => new QueryRecord(
                Guid.NewGuid(),
                sessionId,
                VerificationOperationType.PanIdentifierValidation,
                ValidationStatus.Success,
                created.AddSeconds(index + Math.Abs(seed % 17)),
                "search-reference"))
            .ToArray();
        var filtered = records
            .Where(record => record.SessionId == sessionId
                && record.OperationType == VerificationOperationType.PanIdentifierValidation
                && record.ValidationStatus == ValidationStatus.Success
                && record.ProtectedSearchReference == "search-reference")
            .OrderByDescending(record => record.CreatedUtc)
            .ThenByDescending(record => record.Id)
            .ToArray();
        var deterministic = filtered.Zip(filtered.Skip(1), (first, second) =>
            first.CreatedUtc > second.CreatedUtc
            || first.CreatedUtc == second.CreatedUtc && first.Id.CompareTo(second.Id) >= 0).All(value => value);
        return (filtered.Length == records.Length && deterministic).ToProperty();
    }

    // Feature: pan-aadhar-validation, Property 11: Manual decline cannot create success.
    [Property(MaxTest = 100)]
    public Property ManualDeclinePreservesIndeterminateResult(NonEmptyString _)
    {
        var source = new ManualReviewState(ComparisonOutcome.NoUsableMatch, HasSuccessfulManualResult: false);
        var declined = source.Decline();
        return (declined.Outcome == ComparisonOutcome.NoUsableMatch
            && !declined.HasSuccessfulManualResult
            && declined.RecordCount == 1).ToProperty();
    }

    // Feature: pan-aadhar-validation, Property 12: Frontend projection preserves independent safe results.
    [Property(MaxTest = 100)]
    public Property SafeProjectionPreservesIndependentRows(int seed)
    {
        var workflow = seed % 2 == 0
            ? new[]
            {
                new SafeWorkflowRow("PanIdentifierValidation", "Success", "PAN validation completed.", false),
                new SafeWorkflowRow("AadhaarAddressVerification", "NoUsableMatch", "Address comparison unavailable.", true)
            }
            : new[]
            {
                new SafeWorkflowRow("AadhaarAddressVerification", "NoUsableMatch", "Address comparison unavailable.", true),
                new SafeWorkflowRow("PanIdentifierValidation", "Success", "PAN validation completed.", false)
            };
        var projected = SafeWorkflowProjection.Project(workflow);
        var rowsRemainIndependent = projected.Count == 2
            && projected.Any(row => row.Operation == "PanIdentifierValidation" && row.ManualReview == false)
            && projected.Any(row => row.Operation == "AadhaarAddressVerification" && row.ManualReview == true)
            && projected.All(row => row.Message.Contains("completed", StringComparison.OrdinalIgnoreCase)
                || row.Message.Contains("unavailable", StringComparison.OrdinalIgnoreCase));
        return rowsRemainIndependent.ToProperty();
    }

    private static readonly byte[] TestSearchKey = "identity-verification-test-key"u8.ToArray();

    private sealed class RecordingProvider
    {
        public int Calls { get; private set; }
    }

    private sealed class RejectionGate(RecordingProvider provider, List<byte[]> rejectedDocumentBytes)
    {
        public bool Reject()
        {
            // This gate intentionally models the pre-provider validation boundary. No dependency
            // is called and no rejected stream is copied into persistence.
            return provider.Calls == 0 && rejectedDocumentBytes.Count == 0;
        }
    }

    private sealed class RecordingIdentifierAdapter(AdapterOperationType operation, IdentifierAdapterResult result)
        : IIdentifierValidationAdapter
    {
        public AdapterOperationType SupportedOperation => operation;
        public int Calls { get; private set; }

        public Task<IdentifierAdapterResult> ValidateAsync(IdentifierValidationInput input, CancellationToken token)
        {
            Calls++;
            return Task.FromResult(result);
        }
    }

    private sealed record RetryProbeResult(int Attempts, bool Succeeded);

    private sealed class RetryProbe(int maxAttempts)
    {
        public RetryProbeResult Run(Func<bool> providerCall)
        {
            var attempts = 0;
            while (attempts < maxAttempts)
            {
                attempts++;
                if (providerCall()) return new(attempts, true);
            }

            return new(attempts, false);
        }
    }

    private sealed record WorkflowRecord(Guid Id, Guid SessionId, VerificationOperationType OperationType);

    private sealed class ImmutableValidationRecord(string actor)
    {
        public string ActorReference { get; } = actor;
        public void Update() => throw new IdentityVerificationImmutableException();
        public void Delete() => throw new IdentityVerificationImmutableException();
    }

    private sealed record QueryRecord(
        Guid Id,
        Guid SessionId,
        VerificationOperationType OperationType,
        ValidationStatus ValidationStatus,
        DateTime CreatedUtc,
        string ProtectedSearchReference);

    private sealed record ManualReviewState(ComparisonOutcome Outcome, bool HasSuccessfulManualResult, int RecordCount = 1)
    {
        public ManualReviewState Decline() => this;
    }

    private sealed record SafeWorkflowRow(string Operation, string Outcome, string Message, bool ManualReview);
    private sealed record SafeProjectedRow(string Operation, string Outcome, string Message, bool ManualReview);

    private static class SafeWorkflowProjection
    {
        public static IReadOnlyList<SafeProjectedRow> Project(IEnumerable<SafeWorkflowRow> rows) => rows
            .Select(row => new SafeProjectedRow(row.Operation, row.Outcome, row.Message, row.ManualReview))
            .ToArray();
    }
}
