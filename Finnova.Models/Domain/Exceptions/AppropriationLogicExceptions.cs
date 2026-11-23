namespace Finnova.Models.Domain.Exceptions;

public abstract class AppropriationLogicException(string code, string message) : Exception(message)
{
    public string ErrorCode { get; } = code;
    public IReadOnlyDictionary<string, string[]> Errors { get; init; } = new Dictionary<string, string[]>();
}

public sealed class AppropriationLogicValidationException(string message) : AppropriationLogicException("ERR-APPROP-RULE-VALIDATION", message);
public sealed class AppropriationLogicNotFoundException(string message) : AppropriationLogicException("ERR-APPROP-RULE-NOT-FOUND", message);
public sealed class AppropriationLogicForbiddenException(string message) : AppropriationLogicException("ERR-APPROP-RULE-403", message);
public sealed class AppropriationLogicConflictException(string code, string message) : AppropriationLogicException(code, message);
public sealed class AppropriationLogicDependencyException(string message) : AppropriationLogicException("ERR-APPROP-RULE-DEPENDENCY", message);

public static class AppropriationLogicErrorCodes
{
    public const string Ambiguous = "ERR-APPROP-RULE-AMBIGUOUS";
    public const string DuplicateDueHead = "ERR-APPROP-DUPLICATE-DUE-HEAD";
    public const string TotalOver100 = "ERR-APPROP-RULE-TOTAL-OVER-100";
    public const string Stale = "ERR-APPROP-RULE-STALE";
    public const string IdempotencyConflict = "ERR-APPROP-IDEMPOTENCY-CONFLICT";
    public const string DueMapping = "ERR-APPROP-DUE-MAPPING";
}