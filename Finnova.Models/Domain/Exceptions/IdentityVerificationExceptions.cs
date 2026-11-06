namespace Finnova.Models.Domain.Exceptions;

public abstract class IdentityVerificationException : Exception
{
    protected IdentityVerificationException(string message) : base(message) { }
    public abstract string ErrorCode { get; }
}

public sealed class IdentityVerificationValidationException : IdentityVerificationException
{
    public IdentityVerificationValidationException() : base("The identity verification request is invalid.") { }

    public IdentityVerificationValidationException(IReadOnlyCollection<string> errors) : this()
    {
        Errors = errors;
    }

    public IReadOnlyCollection<string> Errors { get; } = Array.Empty<string>();
    public override string ErrorCode => "ERR-IVF-400";
}

public sealed class IdentityVerificationConfigurationException : IdentityVerificationException
{
    public IdentityVerificationConfigurationException() : base("Identity verification is not configured.") { }
    public override string ErrorCode => "ERR-IVF-503";
}

public sealed class IdentityVerificationProviderUnavailableException : IdentityVerificationException
{
    public IdentityVerificationProviderUnavailableException() : base("The verification provider is temporarily unavailable.") { }
    public override string ErrorCode => "ERR-IVF-503";
}

public sealed class IdentityVerificationPersistenceException : IdentityVerificationException
{
    public IdentityVerificationPersistenceException(Exception inner) : base("The verification result could not be recorded.")
    {
        Inner = inner;
    }

    public override string ErrorCode => "ERR-IVF-500";
    public Exception Inner { get; }
}

public sealed class IdentityVerificationDocumentSizeException : IdentityVerificationException
{
    public IdentityVerificationDocumentSizeException() : base("The document exceeds the 5 MB limit.") { }
    public override string ErrorCode => "ERR-IVF-413";
}

public sealed class IdentityVerificationDocumentFormatException : IdentityVerificationException
{
    public IdentityVerificationDocumentFormatException() : base("Only JPG, PNG, and PDF documents are supported.") { }
    public override string ErrorCode => "ERR-IVF-415";
}

public sealed class IdentityVerificationRecordNotFoundException : IdentityVerificationException
{
    public IdentityVerificationRecordNotFoundException() : base("The requested verification record was not found.") { }
    public override string ErrorCode => "ERR-IVF-404";
}

public sealed class IdentityVerificationImmutableException : IdentityVerificationException
{
    public IdentityVerificationImmutableException() : base("Verification history is immutable.") { }
    public override string ErrorCode => "ERR-IVF-409";
}

public sealed class IdentityVerificationAuthorizationException : IdentityVerificationException
{
    public IdentityVerificationAuthorizationException() : base("You are not authorized for this identity-verification operation.") { }
    public override string ErrorCode => "ERR-IVF-403";
}
