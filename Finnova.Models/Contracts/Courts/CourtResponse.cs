using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Contracts.Courts;

public record CourtResponse(
    Guid Id,
    string Code,
    string Name,
    CourtType CourtType,
    string Jurisdiction,
    string Location,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);