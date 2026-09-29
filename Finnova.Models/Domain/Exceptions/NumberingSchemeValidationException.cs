namespace Finnova.Models.Domain.Exceptions;

/// <summary>
/// Thrown for structural-validity failures on a numbering scheme that are enforced in the handler
/// rather than by FluentValidation: invalid/malformed format template, missing sequence token, or
/// inconsistent scope/scope-id. Maps to ERR-DCN-400.
/// </summary>
public class NumberingSchemeValidationException : Exception
{
    public NumberingSchemeValidationException(string message) : base(message)
    {
    }
}
