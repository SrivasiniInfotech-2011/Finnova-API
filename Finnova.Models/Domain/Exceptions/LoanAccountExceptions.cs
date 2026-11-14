namespace Finnova.Models.Domain.Exceptions;

public abstract class LoanAccountException : Exception
{
    protected LoanAccountException(string message) : base(message) { }
    public abstract string ErrorCode { get; }
}

public sealed class LoanAccountNotFoundException : LoanAccountException
{
    public LoanAccountNotFoundException(string message) : base(message) { }
    public static LoanAccountNotFoundException ForAccount(Guid id) => new($"Loan account '{id}' was not found.");
    public override string ErrorCode => "ERR-ACC-404";
}

public sealed class LoanAccountValidationException : LoanAccountException
{
    public LoanAccountValidationException(string message) : base(message) { }
    public override string ErrorCode => "ERR-ACC-400";
}

public sealed class LoanAccountConflictException : LoanAccountException
{
    public LoanAccountConflictException(string message) : base(message) { }
    public override string ErrorCode => "ERR-ACC-409";
}

/// <summary>Checker equals maker (four-eyes). Maps to HTTP 403.</summary>
public sealed class KycResolutionForbiddenException : LoanAccountException
{
    public KycResolutionForbiddenException() : base("The maker of a KYC resolution request cannot approve or reject it.") { }
    public override string ErrorCode => "ERR-ACC-403";
}

/// <summary>Aggregator mode selected but no provider is configured.</summary>
public sealed class KycAggregatorNotConfiguredException : Exception
{
    public KycAggregatorNotConfiguredException() : base("No KYC aggregator provider is configured.") { }
}