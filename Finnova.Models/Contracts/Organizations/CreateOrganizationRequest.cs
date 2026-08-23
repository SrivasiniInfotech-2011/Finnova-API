namespace Finnova.Models.Contracts.Organizations;

public record CreateOrganizationRequest(
    string Code,
    string Name,
    string ConstitutionType,
    string? Description,
    string? CeoName,
    DateTime? RegistrationDate,
    string? RegistrationNumber,
    string? PanNumber,
    string? GstNumber,
    string? CorporateAddress,
    string? CorporateCity,
    string? CorporateState,
    string? CorporateCountry,
    string? CorporatePincode,
    string? CommunicationAddress,
    string? CommunicationCity,
    string? CommunicationState,
    string? CommunicationCountry,
    string? CommunicationPincode,
    string? Telephone,
    string? Mobile,
    string? Email,
    string? Website,
    string AccountingCurrency = "INR"
);
