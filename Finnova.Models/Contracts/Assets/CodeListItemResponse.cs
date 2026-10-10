namespace Finnova.Models.Contracts.Assets;

/// <summary>Slim projection for dropdowns / the class-code filter panel (R11.3, R12.1).</summary>
public record CodeListItemResponse(Guid Id, string Code, string Description);
