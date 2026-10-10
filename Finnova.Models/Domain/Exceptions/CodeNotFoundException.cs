namespace Finnova.Models.Domain.Exceptions;

/// <summary>Thrown when a code-master record id does not exist (R2.6, R6.3). Maps to ERR-AST-404.</summary>
public class CodeNotFoundException : Exception
{
    public CodeNotFoundException(string master, Guid id)
        : base($"{master} '{id}' was not found.")
    {
    }
}
