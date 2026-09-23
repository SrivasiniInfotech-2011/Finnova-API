using MediatR;
using Finnova.Models.Contracts.Organizations;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;

namespace Finnova.Service.Organizations.Queries.GetCurrentOrganization;

public class GetCurrentOrganizationQueryHandler : IRequestHandler<GetCurrentOrganizationQuery, OrganizationResponse?>
{
    private readonly IOrganizationRepository _repository;

    public GetCurrentOrganizationQueryHandler(IOrganizationRepository repository)
    {
        _repository = repository;
    }

    public async Task<OrganizationResponse?> Handle(GetCurrentOrganizationQuery request, CancellationToken cancellationToken)
    {
        var org = await _repository.GetCurrentAsync(cancellationToken);
        return org?.ToResponse();
    }
}
