namespace Finnova.Models.Domain.Exceptions;

/// <summary>Thrown on any attempt to update/delete an audit entry (R14.4). Maps to ERR-USR-409.</summary>
public class AuditEntryImmutableException : Exception
{
    public const string ErrorCode = "ERR-USR-409";
    public AuditEntryImmutableException() : base("Audit entries are immutable and cannot be changed or removed.") { }
}
