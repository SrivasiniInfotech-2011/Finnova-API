using MediatR;
using Finnova.Models.Contracts.Organizations;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
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
            // Basic Info
            Code = request.Code,
            Name = request.Name,
            ConstitutionType = Enum.Parse<ConstitutionType>(request.ConstitutionType, ignoreCase: true),
            Description = request.Description,

            // Registration Info
            CeoName = request.CeoName,
            RegistrationDate = request.RegistrationDate,
            RegistrationNumber = request.RegistrationNumber,
            PanNumber = request.PanNumber,
            GstNumber = request.GstNumber,

            // Corporate Address
            CorporateAddress = request.CorporateAddress,
            CorporateCity = request.CorporateCity,
            CorporateState = request.CorporateState,
            CorporateCountry = request.CorporateCountry,
            CorporatePincode = request.CorporatePincode,

            // Communication Address
            CommunicationAddress = request.CommunicationAddress,
            CommunicationCity = request.CommunicationCity,
            CommunicationState = request.CommunicationState,
            CommunicationCountry = request.CommunicationCountry,
            CommunicationPincode = request.CommunicationPincode,

            // Contact Details
            Telephone = request.Telephone,
            Mobile = request.Mobile,
            Email = request.Email,
            Website = request.Website,

            // Accounting
            AccountingCurrency = request.AccountingCurrency,
        };

        await _repository.AddAsync(organization, cancellationToken);
        return organization.ToResponse();
    }
}
