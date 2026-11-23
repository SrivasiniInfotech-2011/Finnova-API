namespace Finnova.Models.Domain.Exceptions;

/// <summary>Stable rule keys carried in the problem response "rule" extension (R15.4).</summary>
public static class ReceiptRuleKeys
{
    public const string CashDenominationMismatch = "CASH_DENOMINATION_MISMATCH";
    public const string DocAmountMismatch = "DOC_AMOUNT_MISMATCH";
    public const string DocAmountNotPositive = "DOC_AMOUNT_NOT_POSITIVE";
    public const string AccountNotFound = "ACCOUNT_NOT_FOUND";
    public const string AccountNotLive = "ACCOUNT_NOT_LIVE";
    public const string AccountCustomerMismatch = "ACCOUNT_CUSTOMER_MISMATCH";
    public const string GlNotPostable = "GL_NOT_POSTABLE";
    public const string CustomerGlNotConfigured = "CUSTOMER_GL_NOT_CONFIGURED";
    public const string NumberingSchemeNotConfigured = "NUMBERING_SCHEME_NOT_CONFIGURED";
}

/// <summary>Base of the receipt error family. Mapped to ProblemDetails by the AccountsService middleware.</summary>
public abstract class ReceiptException : Exception
{
    protected ReceiptException(string message) : base(message) { }
    public abstract string ErrorCode { get; }
    public abstract int StatusCode { get; }
    public virtual string? Rule => null;
}

public sealed class ReceiptValidationException : ReceiptException
{
    public ReceiptValidationException(string message) : base(message) { }
    public override string ErrorCode => "ERR-RCP-400";
    public override int StatusCode => 400;
}

public sealed class ReceiptNotFoundException : ReceiptException
{
    public ReceiptNotFoundException(string message) : base(message) { }
    public static ReceiptNotFoundException ForReceipt(Guid id) => new($"Receipt '{id}' was not found.");
    public override string ErrorCode => "ERR-RCP-404";
    public override int StatusCode => 404;
}

public sealed class ReceiptDuplicateDocNoException : ReceiptException
{
    public ReceiptDuplicateDocNoException() : base("Document number already exists") { }
    public override string ErrorCode => "ERR-RCP-409";
    public override int StatusCode => 409;
}

public sealed class ReceiptBusinessRuleException : ReceiptException
{
    private readonly string _rule;
    public ReceiptBusinessRuleException(string rule, string message) : base(message) => _rule = rule;
    public override string ErrorCode => "ERR-RCP-422";
    public override int StatusCode => 422;
    public override string? Rule => _rule;
}