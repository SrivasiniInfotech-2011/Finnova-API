using MediatR;
using Finnova.Models.Contracts.Organizations;

namespace Finnova.Service.Organizations.Queries.GetOrganizationById;

public record GetOrganizationByIdQuery(Guid Id) : IRequest<OrganizationResponse?>;
