using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Contracts.Entities;

/// <summary>Request to create an entity. IsActive is nullable so the service defaults it to true (R1.2).</summary>
public record CreateEntityRequest(
    string Code,
    string Name,
    EntityType EntityType,
    string? RegistrationIdentifier,
    string? ContactPerson,
    string? Email,
    string? Phone,
    string? AddressLine,
    IReadOnlyDictionary<string, string>? Attributes,
    bool? IsActive);