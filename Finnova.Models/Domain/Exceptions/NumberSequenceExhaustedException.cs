namespace Finnova.Models.Domain.Exceptions;

/// <summary>
/// Thrown when advancing a sequence would exceed the capacity of its configured padding width
/// (R5.9). Maps to ERR-DCN-409.
/// </summary>
public class NumberSequenceExhaustedException : Exception
{
    public NumberSequenceExhaustedException(string code)
        : base($"Numbering scheme '{code}' has exhausted its sequence for the configured padding width.")
    {
    }
}
