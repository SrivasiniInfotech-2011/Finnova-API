using Finnova.Models.Contracts.Entities;
using MediatR;

namespace Finnova.Service.Entity.Queries.GetEntityById;

public record GetEntityByIdQuery(Guid Id) : IRequest<EntityResponse>;