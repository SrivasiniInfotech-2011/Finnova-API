namespace Finnova.Models.Domain.Exceptions;

/// <summary>
/// Thrown when a challan-rule create/update uses a Rule Code that already exists under the same
/// drawee bank (trimmed, case-insensitive) (R6.4). Maps to ERR-DRB-409 by type.
/// </summary>
public class ChallanRuleDuplicateCodeException : Exception
{
    public const string ErrorCode = "ERR-DRB-409";

    public ChallanRuleDuplicateCodeException()
        : base("Rule code must be unique within the bank")
    {
    }
}
