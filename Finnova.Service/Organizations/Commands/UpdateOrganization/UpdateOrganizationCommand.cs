using MediatR;
using Finnova.Models.Contracts.Organizations;

namespace Finnova.Service.Organizations.Commands.UpdateOrganization;

public record UpdateOrganizationCommand(
    Guid Id,
    string Name,
    string Code,
    string? Description,
    string? Address,
    string? Phone,
    string? Email,
    string Status
) : IRequest<OrganizationResponse?>;
