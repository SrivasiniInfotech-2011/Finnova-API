using Finnova.Models.Contracts.Entities;
using Finnova.Models.Domain.Enums;
using MediatR;

namespace Finnova.Service.Entity.Commands.CreateEntity;

public record CreateEntityCommand(
    string Code,
    string Name,
    EntityType EntityType,
    string? RegistrationIdentifier,
    string? ContactPerson,
    string? Email,
    string? Phone,
    string? AddressLine,
    IReadOnlyDictionary<string, string>? Attributes,
    bool? IsActive,
    string ActingAdmin) : IRequest<EntityResponse>;