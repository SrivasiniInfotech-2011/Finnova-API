namespace Finnova.Models.Domain.Exceptions;

/// <summary>Thrown when an asset id does not exist (R4.9, R5.9). Maps to ERR-AST-404.</summary>
public class AssetNotFoundException : Exception
{
    public AssetNotFoundException(Guid id)
        : base($"Asset '{id}' was not found.")
    {
    }
}
