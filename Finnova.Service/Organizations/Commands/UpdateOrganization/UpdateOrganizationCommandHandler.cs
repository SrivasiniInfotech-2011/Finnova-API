using Mapster;
using MediatR;
using Finnova.Models.Contracts.Organizations;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Interfaces;

namespace Finnova.Service.Organizations.Commands.UpdateOrganization;

public class UpdateOrganizationCommandHandler : IRequestHandler<UpdateOrganizationCommand, OrganizationResponse?>
{
    private readonly IOrganizationRepository _repository;

    public UpdateOrganizationCommandHandler(IOrganizationRepository repository)
    {
        _repository = repository;
    }

    public async Task<OrganizationResponse?> Handle(UpdateOrganizationCommand request, CancellationToken cancellationToken)
    {
        var org = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (org is null) return null;

        org.Name = request.Name;
        org.Code = request.Code;
        org.Description = request.Description;
        org.Address = request.Address;
        org.Phone = request.Phone;
        org.Email = request.Email;
        org.Status = Enum.Parse<OrganizationStatus>(request.Status, ignoreCase: true);
        org.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(org, cancellationToken);
        return org.Adapt<OrganizationResponse>();
    }
}
