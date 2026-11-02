using Finnova.Models.Contracts.IdentityVerification;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.IdentityVerification.External;
using Finnova.Service.IdentityVerification.Queries;
using Finnova.Service.IdentityVerification.Storage;
using Finnova.Service.IdentityVerification.Validation;
using Finnova.Service.IdentityVerification;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Finnova.Tests.Unit;

public sealed class IdentityVerificationExamples
{
    [Theory]
    [InlineData("ABCDE1234F")]
    [InlineData("abcde1234f")]
    [InlineData("  abcde1234f  ")]
    public void PanIsUppercaseNormalized(string input)
    {
        Assert.Equal("ABCDE1234F", IdentityVerificationRules.NormalizePan(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("ABCDE123")]
    [InlineData("ABCDE12345")]
    [InlineData("AB1DE1234F")]
    [InlineData("ABCDE1234")]
    public void InvalidPanIsRejected(string input)
    {
        var exception = Assert.Throws<IdentityVerificationValidationException>(
            () => IdentityVerificationRules.NormalizePan(input));

        Assert.Equal("ERR-IVF-400", exception.ErrorCode);
        if (input.Length > 0)
        {
            Assert.DoesNotContain(input, exception.Message, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("123456789012")]
    [InlineData("1234 5678 9012")]
    [InlineData("1234-5678-9012")]
    public void AadhaarRemovesSeparatorsAndRequiresTwelveDigits(string input)
    {
        Assert.Equal("123456789012", IdentityVerificationRules.NormalizeAadhaar(input));
    }

    [Theory]
    [InlineData("1234")]
    [InlineData("12345678901")]
    [InlineData("1234567890123")]
    [InlineData("1234-5678-901A")]
    public void InvalidAadhaarIsRejected(string input)
    {
        var exception = Assert.Throws<IdentityVerificationValidationException>(
            () => IdentityVerificationRules.NormalizeAadhaar(input));

        Assert.Equal("ERR-IVF-400", exception.ErrorCode);
    }

    [Theory]
    [InlineData(ComparisonOutcome.Match)]
    [InlineData(ComparisonOutcome.NonMatch)]
    [InlineData(ComparisonOutcome.NoUsableMatch)]
    public void ComparisonOutcomesRemainIndependent(ComparisonOutcome outcome)
    {
        var result = new ComparisonVerificationResult(
            VerificationOperationType.PanNameVerification,
            outcome,
            null,
            Guid.NewGuid(),
            "corr-identity-001",
            DateTime.UtcNow,
            "Comparison completed.",
            outcome == ComparisonOutcome.NoUsableMatch,
            "********1234");

        Assert.Equal(outcome, result.ComparisonOutcome);
        Assert.Equal(VerificationOperationType.PanNameVerification, result.OperationType);
        Assert.Null(result.RecordId);
    }

    [Theory]
    [InlineData(ValidationStatus.Success)]
    [InlineData(ValidationStatus.Failure)]
    [InlineData(ValidationStatus.Unavailable)]
    public void ValidationStatusesRemainIndependent(ValidationStatus status)
    {
        var result = new IdentifierValidationResult(
            VerificationOperationType.AadhaarIdentifierValidation,
            status,
            Guid.NewGuid(),
            null,
            "corr-identity-002",
            DateTime.UtcNow,
            "Identifier validation completed.",
            status != ValidationStatus.Success,
            "********9012");

        Assert.Equal(status, result.ValidationStatus);
        Assert.Equal(VerificationOperationType.AadhaarIdentifierValidation, result.OperationType);
        Assert.DoesNotContain("123456789012", result.MaskedSubjectReference, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RequiredComparisonInputIsRejectedBeforeProviderInvocation()
    {
        var providerCalls = 0;
        Task ProviderCall()
        {
            providerCalls++;
            return Task.CompletedTask;
        }

        async Task ExecuteAsync()
        {
            IdentityVerificationRules.RequireText("   ", "SubmittedName");
            await ProviderCall();
        }

        var exception = await Assert.ThrowsAsync<IdentityVerificationValidationException>(ExecuteAsync);

        Assert.Equal("ERR-IVF-400", exception.ErrorCode);
        Assert.Equal(0, providerCalls);
    }

    [Fact]
    public void FiveMbDocumentIsAcceptedAtTheMaximumBoundary()
    {
        var validator = CreateDocumentValidator();

        validator.Validate(
            "identity.pdf",
            "application/pdf",
            5 * 1024 * 1024,
            "%PDF-"u8.ToArray());
    }

    [Fact]
    public void DocumentAboveFiveMbIsRejectedBeforeFormatProcessing()
    {
        var validator = CreateDocumentValidator();

        var exception = Assert.Throws<IdentityVerificationDocumentSizeException>(() =>
            validator.Validate(
                "identity.pdf",
                "application/pdf",
                5 * 1024 * 1024 + 1,
                "%PDF-"u8.ToArray()));

        Assert.Equal("ERR-IVF-413", exception.ErrorCode);
    }

    public static IEnumerable<object[]> SupportedDocumentFormats()
    {
        yield return ["identity.jpg", "image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF, 0x00 }];
        yield return ["identity.jpeg", "image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF, 0x00 }];
        yield return ["identity.png", "image/png", new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }];
        yield return ["identity.pdf", "application/pdf", "%PDF-1.7"u8.ToArray()];
    }

    [Theory]
    [MemberData(nameof(SupportedDocumentFormats))]
    public void SupportedDocumentFormatsRequireMatchingSignatures(
        string fileName,
        string contentType,
        byte[] signature)
    {
        CreateDocumentValidator().Validate(fileName, contentType, signature.Length, signature);
    }

    [Theory]
    [InlineData("identity.txt", "text/plain")]
    [InlineData("identity.pdf", "image/png")]
    [InlineData("identity.png", "application/pdf")]
    public void UnsupportedDocumentExtensionOrMimeTypeIsRejected(string fileName, string contentType)
    {
        var exception = Assert.Throws<IdentityVerificationDocumentFormatException>(() =>
            CreateDocumentValidator().Validate(fileName, contentType, 8, "%PDF-1.7"u8.ToArray()));

        Assert.Equal("ERR-IVF-415", exception.ErrorCode);
    }

    [Theory]
    [MemberData(nameof(MalformedDocumentFormats))]
    public void UnsupportedDocumentSignatureIsRejected(
        string fileName,
        string contentType,
        byte[] signature)
    {
        Assert.Throws<IdentityVerificationDocumentFormatException>(() =>
            CreateDocumentValidator().Validate(fileName, contentType, signature.Length, signature));
    }

    public static IEnumerable<object[]> MalformedDocumentFormats()
    {
        yield return ["identity.jpg", "image/jpeg", new byte[] { 0x00, 0x00, 0x00 }];
        yield return ["identity.png", "image/png", new byte[] { 0x89, 0x50, 0x4E }];
        yield return ["identity.pdf", "application/pdf", "NOTPDF"u8.ToArray()];
    }

    [Fact]
    public void PartialWorkflowFailurePreservesIndependentSuccessfulResult()
    {
        var panResult = new IdentifierValidationResult(
            VerificationOperationType.PanIdentifierValidation,
            ValidationStatus.Success,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "corr-partial-001",
            DateTime.UtcNow,
            "PAN validation completed.",
            false,
            "******34F");
        var failure = new WorkflowFailureSummary(
            VerificationOperationType.PanNameVerification,
            "ERR-IVF-503",
            "The comparison provider is temporarily unavailable.");
        var response = new VerificationWorkflowResponse(
            panResult.SessionId!.Value,
            panResult.CorrelationId,
            panResult.TimestampUtc,
            panResult,
            null,
            null,
            null,
            [failure]);

        Assert.Same(panResult, response.PanIdentifierResult);
        Assert.Null(response.PanNameResult);
        Assert.Single(response.PartialFailures);
        Assert.Equal("ERR-IVF-503", response.PartialFailures[0].Code);
    }

    [Fact]
    public void ManualDeclineKeepsIndeterminateResultWithoutCreatingSuccess()
    {
        var failure = new WorkflowFailureSummary(
            VerificationOperationType.AadhaarAddressVerification,
            "ERR-IVF-503",
            "The comparison provider is temporarily unavailable.");
        var response = new VerificationWorkflowResponse(
            Guid.NewGuid(),
            "corr-manual-decline-001",
            DateTime.UtcNow,
            null,
            null,
            null,
            null,
            [failure]);

        Assert.Null(response.AadhaarAddressResult);
        Assert.Single(response.PartialFailures);
        Assert.DoesNotContain(response.PartialFailures, item => item.Code == "SUCCESS");
    }

    [Fact]
    public void PersistenceFailureUsesPrivacySafeMessage()
    {
        var exception = new IdentityVerificationPersistenceException(
            new InvalidOperationException("PAN ABCDE1234F and Aadhaar 123456789012 were included in the provider payload."));

        Assert.Equal("ERR-IVF-500", exception.ErrorCode);
        Assert.DoesNotContain("ABCDE1234F", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("123456789012", exception.Message, StringComparison.Ordinal);
        Assert.Equal("The verification result could not be recorded.", exception.Message);
    }

    [Fact]
    public void SafeDiagnosticReferenceAndMaskedValueDoNotExposeIdentityData()
    {
        var result = new ComparisonVerificationResult(
            VerificationOperationType.AadhaarAddressVerification,
            ComparisonOutcome.NoUsableMatch,
            null,
            null,
            "corr-safe-001",
            DateTime.UtcNow,
            "The comparison could not be completed. Diagnostic reference: diag-123.",
            true,
            "********9012");

        Assert.Contains("diag-123", result.SafeMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("Mumbai, Maharashtra", result.SafeMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("123456789012", result.MaskedSubjectReference, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DefaultPaginationUsesTwentyItemsAndEmptyBeyondLastPage()
    {
        var repository = new Mock<IIdentityVerificationRepository>();
        repository
            .Setup(x => x.GetPagedAsync(
                It.Is<VerificationRecordQuery>(query => query.Page == 2 && query.PageSize == 20),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<ValidationRecord>(), 20));

        var handler = new GetVerificationRecordsHandler(repository.Object);
        var response = await handler.Handle(
            new GetVerificationRecordsQuery(new VerificationRecordQuery(null, null, null, null, null, null, null, null, 2, 0)),
            CancellationToken.None);

        Assert.Empty(response.Data);
        Assert.Equal(20, response.Total);
        Assert.Equal(2, response.Page);
        Assert.Equal(20, response.PageSize);
        Assert.Equal(1, response.TotalPages);
    }

    [Fact]
    public async Task ProtectedDocumentNotFoundReturnsSafeNotFoundException()
    {
        var repository = new Mock<IIdentityVerificationRepository>();
        repository
            .Setup(x => x.GetRecordAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ValidationRecord?)null);
        var store = new Mock<IProtectedDocumentStore>();
        var query = new ProtectedDocumentQuery(repository.Object, store.Object);

        var exception = await Assert.ThrowsAsync<IdentityVerificationRecordNotFoundException>(
            () => query.GetAsync(Guid.NewGuid(), CancellationToken.None));

        Assert.Equal("ERR-IVF-404", exception.ErrorCode);
        store.Verify(
            x => x.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void ExistingIdentifierResultFieldsRemainAvailable()
    {
        var recordId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;
        var result = new IdentifierValidationResult(
            VerificationOperationType.PanIdentifierValidation,
            ValidationStatus.Failure,
            recordId,
            sessionId,
            "corr-compatible-001",
            timestamp,
            "The PAN could not be validated.",
            true,
            "******34F");

        Assert.Equal(recordId, result.RecordId);
        Assert.Equal(sessionId, result.SessionId);
        Assert.Equal("corr-compatible-001", result.CorrelationId);
        Assert.Equal(timestamp, result.TimestampUtc);
        Assert.Equal("The PAN could not be validated.", result.SafeMessage);
        Assert.True(result.ManualOverrideAvailable);
        Assert.Equal("******34F", result.MaskedSubjectReference);
    }

    private static DocumentInputValidator CreateDocumentValidator(long maxDocumentBytes = 5 * 1024 * 1024)
    {
        return new DocumentInputValidator(Options.Create(new IdentityVerificationOptions
        {
            MaxDocumentBytes = maxDocumentBytes,
            AcceptedContentTypes = ["image/jpeg", "image/png", "application/pdf"]
        }));
    }
}
