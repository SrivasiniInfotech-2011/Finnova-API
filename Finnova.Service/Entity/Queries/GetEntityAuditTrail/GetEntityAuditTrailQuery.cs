using Finnova.Models.Contracts.Entities;
using MediatR;

namespace Finnova.Service.Entity.Queries.GetEntityAuditTrail;

public record GetEntityAuditTrailQuery(Guid EntityId) : IRequest<List<EntityAuditEntryResponse>>;