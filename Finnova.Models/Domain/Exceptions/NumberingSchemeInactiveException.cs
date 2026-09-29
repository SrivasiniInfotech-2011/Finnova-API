namespace Finnova.Models.Domain.Exceptions;

/// <summary>
/// Thrown when issuance is attempted against an inactive scheme (R5.7). Maps to ERR-DCN-409.
/// </summary>
public class NumberingSchemeInactiveException : Exception
{
    public NumberingSchemeInactiveException(string code)
        : base($"Numbering scheme '{code}' is inactive and cannot issue numbers.")
    {
    }
}
