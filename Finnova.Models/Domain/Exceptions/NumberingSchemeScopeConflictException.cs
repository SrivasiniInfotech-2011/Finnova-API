namespace Finnova.Models.Domain.Exceptions;

/// <summary>
/// Thrown when a scheme create would collide with an existing scheme on the
/// (DocumentType, Scope, ScopeId) uniqueness rule. Maps to ERR-DCN-409.
/// </summary>
public class NumberingSchemeScopeConflictException : Exception
{
    public const string ErrorCode = "ERR-DCN-409";

    public NumberingSchemeScopeConflictException()
        : base("A scheme already exists for this document type and scope")
    {
    }
}
