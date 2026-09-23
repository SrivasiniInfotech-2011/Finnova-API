using MediatR;
using Finnova.Models.Contracts.Organizations;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;

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

        // Basic Info
        org.Code = request.Code;
        org.Name = request.Name;
        org.ConstitutionType = Enum.Parse<ConstitutionType>(request.ConstitutionType, ignoreCase: true);
        org.Description = request.Description;

        // Registration Info
        org.CeoName = request.CeoName;
        org.RegistrationDate = request.RegistrationDate;
        org.RegistrationNumber = request.RegistrationNumber;
        org.PanNumber = request.PanNumber;
        org.GstNumber = request.GstNumber;

        // Corporate Address
        org.CorporateAddress = request.CorporateAddress;
        org.CorporateCity = request.CorporateCity;
        org.CorporateState = request.CorporateState;
        org.CorporateCountry = request.CorporateCountry;
        org.CorporatePincode = request.CorporatePincode;

        // Communication Address
        org.CommunicationAddress = request.CommunicationAddress;
        org.CommunicationCity = request.CommunicationCity;
        org.CommunicationState = request.CommunicationState;
        org.CommunicationCountry = request.CommunicationCountry;
        org.CommunicationPincode = request.CommunicationPincode;

        // Contact Details
        org.Telephone = request.Telephone;
        org.Mobile = request.Mobile;
        org.Email = request.Email;
        org.Website = request.Website;

        // Accounting
        org.AccountingCurrency = request.AccountingCurrency;

        // Status & Audit
        org.Status = Enum.Parse<OrganizationStatus>(request.Status, ignoreCase: true);
        org.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(org, cancellationToken);
        return org.ToResponse();
    }
}
