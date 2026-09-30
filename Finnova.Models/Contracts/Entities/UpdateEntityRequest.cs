namespace Finnova.Models.Contracts.Entities;

/// <summary>
/// Request to update an entity's editable fields. Code and EntityType are immutable after creation
/// (R2.3, R4.2) and are therefore absent from this contract.
/// </summary>
public record UpdateEntityRequest(
    string Name,
    string? RegistrationIdentifier,
    string? ContactPerson,
    string? Email,
    string? Phone,
    string? AddressLine,
    IReadOnlyDictionary<string, string>? Attributes,
    bool IsActive);