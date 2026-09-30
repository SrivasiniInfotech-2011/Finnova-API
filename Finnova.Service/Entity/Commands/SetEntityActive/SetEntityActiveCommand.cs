using Finnova.Models.Contracts.Entities;
using MediatR;

namespace Finnova.Service.Entity.Commands.SetEntityActive;

/// <summary>Activates or deactivates an entity (soft retire) (R6).</summary>
public record SetEntityActiveCommand(Guid Id, bool IsActive, string ActingAdmin) : IRequest<EntityResponse>;