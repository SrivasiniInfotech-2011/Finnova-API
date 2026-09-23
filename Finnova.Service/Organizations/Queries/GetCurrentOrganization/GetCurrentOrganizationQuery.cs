using MediatR;
using Finnova.Models.Contracts.Organizations;

namespace Finnova.Service.Organizations.Queries.GetCurrentOrganization;

/// <summary>
/// Returns the single company for this product instance.
/// The product supports exactly one organization.
/// </summary>
public record GetCurrentOrganizationQuery : IRequest<OrganizationResponse?>;
