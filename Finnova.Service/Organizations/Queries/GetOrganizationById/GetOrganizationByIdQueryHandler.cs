using MediatR;
using Finnova.Models.Contracts.Organizations;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;

namespace Finnova.Service.Organizations.Queries.GetOrganizationById;

public class GetOrganizationByIdQueryHandler : IRequestHandler<GetOrganizationByIdQuery, OrganizationResponse?>
{
    private readonly IOrganizationRepository _repository;

    public GetOrganizationByIdQueryHandler(IOrganizationRepository repository)
    {
        _repository = repository;
    }

    public async Task<OrganizationResponse?> Handle(GetOrganizationByIdQuery request, CancellationToken cancellationToken)
    {
        var org = await _repository.GetByIdAsync(request.Id, cancellationToken);
        return org?.ToResponse();
    }
}
