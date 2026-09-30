namespace Finnova.Models.Domain.Exceptions;

/// <summary>
/// Thrown when an entity create is attempted with a code that already exists within the same
/// Entity Type. Maps to ERR-ENT-409.
/// </summary>
public class EntityDuplicateCodeException : Exception
{
    public const string ErrorCode = "ERR-ENT-409";

    public EntityDuplicateCodeException()
        : base("Entity code must be unique per entity type")
    {
    }
}