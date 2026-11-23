namespace Finnova.Models.Domain.Exceptions;

public abstract class ReceiptAppropriationException(string code, string message) : Exception(message)
{
    public string ErrorCode { get; } = code;
    public IReadOnlyDictionary<string, string[]> Errors { get; init; } = new Dictionary<string, string[]>();
}

public sealed class ReceiptAppropriationValidationException(string message) : ReceiptAppropriationException("ERR-APPROP-VALIDATION", message);
public sealed class ReceiptAppropriationNotFoundException(string message) : ReceiptAppropriationException("ERR-APPROP-NOT-FOUND", message);
public sealed class ReceiptAppropriationForbiddenException(string message) : ReceiptAppropriationException("ERR-APPROP-403", message);
public sealed class ReceiptAppropriationConflictException(string code, string message) : ReceiptAppropriationException(code, message);
public sealed class ReceiptAppropriationRuleException(string message) : ReceiptAppropriationException("ERR-APPROP-RULE-CONFIGURATION", message);
public sealed class ReceiptAppropriationAdvanceAccountException(string message) : ReceiptAppropriationException("ERR-APPROP-ADVANCE-ACCOUNT", message);
public sealed class ReceiptAppropriationReconciliationException(string message) : ReceiptAppropriationException("ERR-APPROP-RECONCILIATION", message);
public sealed class ReceiptAppropriationDependencyException(string message) : ReceiptAppropriationException("ERR-APPROP-DEPENDENCY", message);

public static class ReceiptAppropriationErrorCodes
{
    public const string IdempotencyConflict = "ERR-APPROP-IDEMPOTENCY-CONFLICT";
    public const string AlreadyConfirmed = "ERR-APPROP-ALREADY-CONFIRMED";
    public const string StaleData = "ERR-APPROP-STALE-DATA";
    public const string Concurrency = "ERR-APPROP-CONCURRENCY";
}