using FluentValidation;
using Finnova.Models.Contracts.IdentityVerification;
using Finnova.Models.Domain.Enums;
using Finnova.Service.IdentityVerification.Commands.ManualOverrides;
using Finnova.Service.IdentityVerification.Commands.ValidateIdentifiers;
using Finnova.Service.IdentityVerification.Commands.VerifyAadharAddress;
using Finnova.Service.IdentityVerification.Commands.VerifyPanName;

namespace Finnova.Service.IdentityVerification.Commands;

public sealed class ValidatePanIdentifierCommandValidator : AbstractValidator<ValidatePanIdentifierCommand>
{
    public ValidatePanIdentifierCommandValidator()
    {
        RuleFor(x => x.Pan)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(IsPan)
            .WithMessage("PAN must contain five letters, four digits, and one final letter.");

        RuleFor(x => x.ActorReference)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(200)
            .WithMessage("Actor reference is invalid.");
    }

    private static bool IsPan(string value)
    {
        var normalized = value.Trim().ToUpperInvariant();
        return normalized.Length == 10 &&
               normalized.Take(5).All(char.IsLetter) &&
               normalized.Skip(5).Take(4).All(char.IsDigit) &&
               char.IsLetter(normalized[9]);
    }
}

public sealed class ValidateAadhaarIdentifierCommandValidator : AbstractValidator<ValidateAadhaarIdentifierCommand>
{
    public ValidateAadhaarIdentifierCommandValidator()
    {
        RuleFor(x => x.AadhaarNumber)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(IsAadhaar)
            .WithMessage("Aadhaar number must contain exactly 12 digits.");

        RuleFor(x => x.ActorReference)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(200)
            .WithMessage("Actor reference is invalid.");
    }

    private static bool IsAadhaar(string value) =>
        value.Count(char.IsDigit) == 12 && value.All(character => char.IsDigit(character) || char.IsWhiteSpace(character) || character == '-');
}

public sealed class VerifyPanNameCommandValidator : AbstractValidator<VerifyPanNameCommand>
{
    public VerifyPanNameCommandValidator()
    {
        RuleFor(x => x.Request)
            .NotNull()
            .WithMessage("Verification request is required.");

        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.SubmittedName)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .MaximumLength(200)
                .WithMessage("Submitted name is invalid.");

            RuleFor(x => x.Request.Source)
                .NotNull()
                .WithMessage("Comparison source is required.");

            When(x => x.Request.Source is not null, () =>
            {
                RuleFor(x => x.Request.Source).SetValidator(new ComparisonSourceContractValidator());
            });
        });

        RuleFor(x => x.ActorReference)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(200)
            .WithMessage("Actor reference is invalid.");
    }
}

public sealed class VerifyAadharAddressCommandValidator : AbstractValidator<VerifyAadharAddressCommand>
{
    public VerifyAadharAddressCommandValidator()
    {
        RuleFor(x => x.Request)
            .NotNull()
            .WithMessage("Verification request is required.");

        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.SubmittedAddress)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .MaximumLength(1000)
                .WithMessage("Submitted address is invalid.");

            RuleFor(x => x.Request.Source)
                .NotNull()
                .WithMessage("Comparison source is required.");

            When(x => x.Request.Source is not null, () =>
            {
                RuleFor(x => x.Request.Source).SetValidator(new ComparisonSourceContractValidator());
            });
        });

        RuleFor(x => x.ActorReference)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(200)
            .WithMessage("Actor reference is invalid.");
    }
}

public sealed class VerificationWorkflowRequestValidator : AbstractValidator<VerificationWorkflowRequest>
{
    public VerificationWorkflowRequestValidator()
    {
        RuleFor(x => x.WorkflowType)
            .IsInEnum()
            .WithMessage("Workflow type is invalid.");

        RuleFor(x => x.InputMode)
            .IsInEnum()
            .WithMessage("Workflow input mode is invalid.");

        RuleFor(x => x.Pan)
            .MaximumLength(10)
            .Must(value => value is null || IsPan(value))
            .WithMessage("PAN is invalid.");

        RuleFor(x => x.AadhaarNumber)
            .MaximumLength(12)
            .Must(value => value is null || IsAadhaar(value))
            .WithMessage("Aadhaar number is invalid.");

        RuleFor(x => x.PanSubmittedName)
            .MaximumLength(200)
            .WithMessage("Submitted PAN name is invalid.");

        RuleFor(x => x.AadhaarSubmittedAddress)
            .MaximumLength(1000)
            .WithMessage("Submitted Aadhaar address is invalid.");

        When(x => x.PanSource is not null, () =>
        {
            RuleFor(x => x.PanSource!).SetValidator(new ComparisonSourceContractValidator());
        });

        When(x => x.AadhaarSource is not null, () =>
        {
            RuleFor(x => x.AadhaarSource!).SetValidator(new ComparisonSourceContractValidator());
        });
    }

    private static bool IsPan(string value)
    {
        var normalized = value.Trim().ToUpperInvariant();
        return normalized.Length == 10 &&
               normalized.Take(5).All(char.IsLetter) &&
               normalized.Skip(5).Take(4).All(char.IsDigit) &&
               char.IsLetter(normalized[9]);
    }

    private static bool IsAadhaar(string value) =>
        value.Count(char.IsDigit) == 12 && value.All(character => char.IsDigit(character) || char.IsWhiteSpace(character) || character == '-');
}

public sealed class ManualPanOverrideCommandValidator : AbstractValidator<ManualPanOverrideCommand>
{
    public ManualPanOverrideCommandValidator()
    {
        RuleFor(x => x.Pan)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(IsPan)
            .WithMessage("PAN is invalid.");

        RuleFor(x => x.SubmittedName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(200)
            .WithMessage("Submitted name is invalid.");

        RuleFor(x => x.ProtectedDocumentReferenceId)
            .NotEmpty()
            .WithMessage("Protected document reference is required.");

        Include(new DocumentMetadataValidator<ManualPanOverrideCommand>());
        Include(new ActorReferenceValidator<ManualPanOverrideCommand>());
    }

    private static bool IsPan(string value)
    {
        var normalized = value.Trim().ToUpperInvariant();
        return normalized.Length == 10 &&
               normalized.Take(5).All(char.IsLetter) &&
               normalized.Skip(5).Take(4).All(char.IsDigit) &&
               char.IsLetter(normalized[9]);
    }
}

public sealed class ManualAadhaarOverrideCommandValidator : AbstractValidator<ManualAadhaarOverrideCommand>
{
    public ManualAadhaarOverrideCommandValidator()
    {
        RuleFor(x => x.AadhaarNumber)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(IsAadhaar)
            .WithMessage("Aadhaar number is invalid.");

        RuleFor(x => x.SubmittedName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(200)
            .WithMessage("Submitted name is invalid.");

        RuleFor(x => x.SubmittedAddress)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(1000)
            .WithMessage("Submitted address is invalid.");

        RuleFor(x => x.ProtectedDocumentReferenceId)
            .NotEmpty()
            .WithMessage("Protected document reference is required.");

        Include(new DocumentMetadataValidator<ManualAadhaarOverrideCommand>());
        Include(new ActorReferenceValidator<ManualAadhaarOverrideCommand>());
    }

    private static bool IsAadhaar(string value) =>
        value.Count(char.IsDigit) == 12 && value.All(character => char.IsDigit(character) || char.IsWhiteSpace(character) || character == '-');
}

public sealed class ManualComparisonDecisionCommandValidator : AbstractValidator<ManualComparisonDecisionCommand>
{
    public ManualComparisonDecisionCommandValidator()
    {
        RuleFor(x => x.OperationType)
            .Must(operation => operation is VerificationOperationType.PanNameVerification or VerificationOperationType.AadhaarAddressVerification)
            .WithMessage("Comparison operation type is invalid.");

        RuleFor(x => x.Outcome)
            .Must(outcome => outcome is ComparisonOutcome.Match or ComparisonOutcome.NonMatch)
            .WithMessage("Manual comparison outcome is invalid.");

        RuleFor(x => x.SourceRecordId)
            .NotEmpty()
            .WithMessage("Source record is required.");

        RuleFor(x => x.SafeReason)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(500)
            .WithMessage("Manual decision reason is invalid.");

        RuleFor(x => x.ActorReference)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(200)
            .WithMessage("Actor reference is invalid.");
    }
}

internal sealed class ComparisonSourceContractValidator : AbstractValidator<ComparisonSourceContract>
{
    public ComparisonSourceContractValidator()
    {
        RuleFor(x => x.DocumentContentType)
            .Must(value => value is null || IdentityVerificationRules.IsSupportedContentType(value))
            .WithMessage("Document content type is not supported.");

        RuleFor(x => x.DocumentSizeBytes)
            .InclusiveBetween(1, 5 * 1024 * 1024)
            .When(x => x.DocumentSizeBytes is not null)
            .WithMessage("Document size is invalid.");

        RuleFor(x => x.MappingProfile)
            .MaximumLength(200)
            .WithMessage("Mapping profile is invalid.");

        RuleForEach(x => x.ExtractedValues!.Keys)
            .NotEmpty()
            .MaximumLength(100)
            .When(x => x.ExtractedValues is not null)
            .WithMessage("Extracted value key is invalid.");

        RuleForEach(x => x.ExtractedValues!.Values)
            .NotEmpty()
            .MaximumLength(2000)
            .When(x => x.ExtractedValues is not null)
            .WithMessage("Extracted value is invalid.");
    }
}

internal sealed class DocumentMetadataValidator<T> : AbstractValidator<T>
    where T : class
{
    public DocumentMetadataValidator()
    {
        RuleFor(x => GetContentType(x))
            .Must(IdentityVerificationRules.IsSupportedContentType)
            .WithMessage("Document content type is not supported.");

        RuleFor(x => GetSize(x))
            .InclusiveBetween(1, 5 * 1024 * 1024)
            .WithMessage("Document size is invalid.");
    }

    private static string GetContentType(T command) => command switch
    {
        ManualPanOverrideCommand pan => pan.DocumentContentType,
        ManualAadhaarOverrideCommand aadhaar => aadhaar.DocumentContentType,
        _ => string.Empty
    };

    private static long GetSize(T command) => command switch
    {
        ManualPanOverrideCommand pan => pan.DocumentSizeBytes,
        ManualAadhaarOverrideCommand aadhaar => aadhaar.DocumentSizeBytes,
        _ => 0
    };
}

internal sealed class ActorReferenceValidator<T> : AbstractValidator<T>
    where T : class
{
    public ActorReferenceValidator()
    {
        RuleFor(x => GetActorReference(x))
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(200)
            .WithMessage("Actor reference is invalid.");
    }

    private static string GetActorReference(T command) => command switch
    {
        ManualPanOverrideCommand pan => pan.ActorReference,
        ManualAadhaarOverrideCommand aadhaar => aadhaar.ActorReference,
        _ => string.Empty
    };
}
