using MediatR;
using Finnova.Models.Contracts.Organizations;
using Finnova.Models.Domain.Entities;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;

namespace Finnova.Service.Organizations.Commands.CreateOrganization;

public class CreateOrganizationCommandHandler : IRequestHandler<CreateOrganizationCommand, OrganizationResponse>
{
    private readonly IOrganizationRepository _repository;

    public CreateOrganizationCommandHandler(IOrganizationRepository repository)
    {
        _repository = repository;
    }

    public async Task<OrganizationResponse> Handle(CreateOrganizationCommand request, CancellationToken cancellationToken)
    {
        var organization = new Organization
        {
            Name = request.Name,
            Code = request.Code,
            Description = request.Description,
            Email = request.Email
        };

        await _repository.AddAsync(organization, cancellationToken);
        return organization.ToResponse();
    }
}
