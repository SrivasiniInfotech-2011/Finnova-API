namespace Finnova.Models.Contracts.Locations;

public record LocationResponse(
    Guid Id,
    string Code,
    string Name,
    int Level,
    Guid? ParentId,
    string? Description,
    bool IsActive,
    double? Latitude,
    double? Longitude,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<LocationResponse> Children
);
