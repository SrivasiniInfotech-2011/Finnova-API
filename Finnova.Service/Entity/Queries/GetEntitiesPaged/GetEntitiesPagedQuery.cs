using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.Entities;
using Finnova.Models.Domain.Enums;
using MediatR;

namespace Finnova.Service.Entity.Queries.GetEntitiesPaged;

public record GetEntitiesPagedQuery(
    string? SearchTerm,
    EntityType? EntityType,
    int Page = 1,
    int PageSize = 20) : IRequest<PaginatedResponse<EntityResponse>>;