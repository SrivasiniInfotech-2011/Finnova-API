using MediatR;
using Finnova.Models.Contracts.Organizations;

namespace Finnova.Service.Organizations.Queries.GetAllOrganizations;

public record GetAllOrganizationsQuery : IRequest<List<OrganizationResponse>>;
