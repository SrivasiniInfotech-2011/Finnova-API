namespace Finnova.Models.Domain.Exceptions;

/// <summary>Thrown when an operation targets an entity that does not exist. Maps to ERR-ENT-404.</summary>
public class EntityNotFoundException : Exception
{
    public EntityNotFoundException(Guid id)
        : base($"Entity '{id}' was not found.")
    {
    }
}