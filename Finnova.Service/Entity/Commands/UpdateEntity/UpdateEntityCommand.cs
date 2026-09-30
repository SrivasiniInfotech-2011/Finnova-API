using Finnova.Models.Contracts.Entities;
using MediatR;

namespace Finnova.Service.Entity.Commands.UpdateEntity;

public record UpdateEntityCommand(
    Guid Id,
    string Name,
    string? RegistrationIdentifier,
    string? ContactPerson,
    string? Email,
    string? Phone,
    string? AddressLine,
    IReadOnlyDictionary<string, string>? Attributes,
    bool IsActive,
    string ActingAdmin) : IRequest<EntityResponse>;