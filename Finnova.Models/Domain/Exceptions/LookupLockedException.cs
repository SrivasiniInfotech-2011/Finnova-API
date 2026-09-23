namespace Finnova.Models.Domain.Exceptions;

/// <summary>
/// Thrown when a protected change (rename or deletion) is attempted on a
/// system-locked lookup value. Maps to error code ERR-LKP-005 and surfaces the
/// exact user-facing message the UI displays (R3: R3.1, R3.2, R3.4).
/// </summary>
public class LookupLockedException : Exception
{
    /// <summary>Stable error code returned to clients (R3).</summary>
    public const string ErrorCode = "ERR-LKP-005";

    public LookupLockedException()
        : base("System-defined lookup codes cannot be altered.")
    {
    }
}
