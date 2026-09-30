using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Contracts.Entities;

public record EntityResponse(
    Guid Id,
    string Code,
    string Name,
    EntityType EntityType,
    string? RegistrationIdentifier,
    string? ContactPerson,
    string? Email,
    string? Phone,
    string? AddressLine,
    IReadOnlyDictionary<string, string> Attributes,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);