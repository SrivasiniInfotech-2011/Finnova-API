using Mapster;
using MediatR;
using Finnova.Models.Contracts.Organizations;
using Finnova.Repository.Interfaces;

namespace Finnova.Service.Organizations.Queries.GetAllOrganizations;

public class GetAllOrganizationsQueryHandler : IRequestHandler<GetAllOrganizationsQuery, List<OrganizationResponse>>
{
    private readonly IOrganizationRepository _repository;

    public GetAllOrganizationsQueryHandler(IOrganizationRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<OrganizationResponse>> Handle(GetAllOrganizationsQuery request, CancellationToken cancellationToken)
    {
        var orgs = await _repository.GetAllAsync(cancellationToken);
        return orgs.Adapt<List<OrganizationResponse>>();
    }
}
