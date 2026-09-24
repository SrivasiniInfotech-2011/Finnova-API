namespace Finnova.Models.Domain.Exceptions;

/// <summary>
/// Thrown when a nationality create is attempted with a code that already exists
/// (case-insensitive, trimmed) (R2.2). Carries the exact user-facing message the UI
/// displays and lets the middleware map it to 409 (ERR-NAT-409) by type, avoiding
/// message-sniffing on generic validation failures.
/// </summary>
public class NationalityDuplicateCodeException : Exception
{
    /// <summary>Stable error code returned to clients (R2.2, R2.3).</summary>
    public const string ErrorCode = "ERR-NAT-409";

    public NationalityDuplicateCodeException()
        : base("Nationality code must be unique")
    {
    }
}
