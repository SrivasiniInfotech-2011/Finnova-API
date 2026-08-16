using MediatR;
using Finnova.Models.Contracts.Organizations;

namespace Finnova.Service.Organizations.Commands.CreateOrganization;

public record CreateOrganizationCommand(
    string Name,
    string Code,
    string? Description,
    string? Address,
    string? Phone,
    string? Email
) : IRequest<OrganizationResponse>;
