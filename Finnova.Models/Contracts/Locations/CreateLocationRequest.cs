namespace Finnova.Models.Contracts.Locations;

public record CreateLocationRequest(
    string Code,
    string Name,
    int Level,
    Guid? ParentId,
    string? Description,
    bool IsActive,
    double? Latitude,
    double? Longitude
);
