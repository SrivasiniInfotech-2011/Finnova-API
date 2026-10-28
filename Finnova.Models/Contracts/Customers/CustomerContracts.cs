// CREATE Finnova.Models/Contracts/Customers/CustomerContracts.cs
using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Contracts.Customers;

public sealed record CustomerAddressRequest(
    CustomerAddressType AddressType, string? HouseNumber, string? BlockNumber, string? PostalCode,
    string? City, string? State, string? Country, string? POBoxNumber, string? WayNumber,
    string? Telephone, string? ResidenceFaxNumber, string? Mobile, string? Email, string? Website,
    string? ContactPerson, string? ContactPersonPhoneNumber, string? ContactPersonFlatNumber,
    string? AreaSheik, string? LocationLandmark);

public sealed record IndividualCustomerDetailsRequest(
    string? GenderCode, DateOnly? DateOfBirth, string? SpouseName, int? Children, int? TotalDependents,
    DateOnly? WeddingAnniversaryDate, string? HouseOrFlat, bool? Own, decimal? CurrentMarketValue,
    decimal? RemainingLoanValue, decimal? TotalNetWorth, string? Qualification, string? Profession,
    string? IndiaIdentityType, string? IndiaIdentityReference, DateOnly? IdentityIssueDate,
    DateOnly? IdentityExpiryDate);

public sealed record NonIndividualCustomerDetailsRequest(
    string? PublicOrCloselyHeldCode, int? NumberOfDirectorsOrPartners, string? KeyDecisionMaker,
    bool? ListedAtStockExchange, decimal? PaidUpCapital, decimal? FaceValueOfShares,
    decimal? BookValueOfShares, string? BusinessProfile, decimal? MonthlyBusinessIncome,
    string? GeographicalCoverage, int? NumberOfBranches, bool? GovernmentInstitutionalParticipation,
    decimal? PromoterStakePercentage, string? JVPartnerName, decimal? JVPartnerStake,
    string? CEOOrMDName, int? AgeOfCEOOrMD, int? ExperienceInYears, string? ResidentialAddress,
    DateOnly? WeddingAnniversaryDate, string? CustomerBusinessTypeCode, string? CustomerBusinessSubTypeCode,
    string? IndustrySectorCode, string? SubSectorCode, string? CRNumber, DateOnly? CRIssueDate,
    DateOnly? CRExpiryDate, string? BusinessActivities, bool? FactoringApplicable,
    string? FactoringTypeCode, decimal? FactoringLimit, string? Covenants, string? CovenantClause);

public sealed record BankAccountRequest(
    string? AccountTypeCode, string AccountNumber, string? BankCode, string? MICRCode,
    DateOnly? ClearingDate, string? Address, string? ContractNumber, string? SubContractNumber,
    bool IsDefault);

public sealed record CustomerDocumentReferenceRequest(
    string ConstitutionCode, Guid DocumentDefinitionId, string DocumentCode, string? Remarks,
    bool IsCollected, bool IsScanned, string? Value, string? ValueSource, string? DmsReference,
    string? ViewMetadata);

public sealed record CustomerTrackRequest(
    string? LineOfBusinessCode, string? AccountNumber, string TrackTypeCode, DateOnly TrackDate,
    string? Reason, string? ByLogin);

public sealed record FactoringSubLimitRequest(
    bool FactoringApplicable, string? FactoringTypeCode, decimal? Limit, string? Covenant,
    string? CovenantClause, DateOnly? CutOffDate);

public sealed record CustomerCreateRequest(
    Guid CompanyId, bool IsCustomer, bool IsGuarantor, bool IsCoApplicant, string CustomerTypeCode,
    string? GroupCode, string? IndustryCode, string? CustomerPostingGroupCode, Guid? GLAccountId,
    string? ConstitutionCode, string? NationalityCode, string? TitleCode, string? MaritalStatusCode,
    string? CustomerBusinessTypeCode, string? CustomerBusinessSubTypeCode, string? IndustrySectorCode,
    string? SubSectorCode, string? FirstName, string? SecondName, string? ThirdName, string? FourthName,
    string? FifthName, string? SixthName, string? Notes, bool? IsVip, bool? IsSeniorMember,
    string? FinancialRequirement, string? Accommodation, bool? IsRelatedParty, string? RelationCode,
    string? EmployerEconomicActivityCode, IReadOnlyCollection<CustomerAddressRequest> Addresses,
    IndividualCustomerDetailsRequest? IndividualDetails, NonIndividualCustomerDetailsRequest? NonIndividualDetails,
    IReadOnlyCollection<BankAccountRequest> BankAccounts, IReadOnlyCollection<CustomerDocumentReferenceRequest> Documents,
    IReadOnlyCollection<CustomerTrackRequest> Tracks, IReadOnlyCollection<FactoringSubLimitRequest> FactoringSubLimits);

public sealed record CustomerModifyRequest(
    string CustomerCode, Guid CompanyId, string? CustomerTypeCode, string? GroupCode, string? IndustryCode,
    string? CustomerPostingGroupCode, Guid? GLAccountId, string? ConstitutionCode, string? NationalityCode,
    string? TitleCode, string? MaritalStatusCode, string? CustomerBusinessTypeCode,
    string? CustomerBusinessSubTypeCode, string? IndustrySectorCode, string? SubSectorCode, string? FirstName,
    string? SecondName, string? ThirdName, string? FourthName, string? FifthName, string? SixthName,
    string? Notes, bool? IsVip, bool? IsSeniorMember, string? FinancialRequirement, string? Accommodation,
    bool? IsRelatedParty, string? RelationCode, string? EmployerEconomicActivityCode,
    IReadOnlyCollection<CustomerAddressRequest> Addresses, IndividualCustomerDetailsRequest? IndividualDetails,
    NonIndividualCustomerDetailsRequest? NonIndividualDetails, IReadOnlyCollection<BankAccountRequest> BankAccounts,
    IReadOnlyCollection<CustomerDocumentReferenceRequest> Documents, IReadOnlyCollection<CustomerTrackRequest> Tracks,
    IReadOnlyCollection<FactoringSubLimitRequest> FactoringSubLimits, bool CopyCommunicationAddressToPermanent);

public sealed record CustomerQueryRequest(string? CustomerCode, string? Search, Guid? CompanyId, int Page = 1, int PageSize = 25);

public sealed record CustomerDisplayProjection(string Code, string? Name);
public sealed record CustomerReferenceProjection(string? Code, string? Name);
public sealed record CustomerAddressResponse(CustomerAddressType AddressType, string? HouseNumber, string? BlockNumber,
    string? PostalCode, string? City, string? State, string? Country, string? POBoxNumber, string? WayNumber,
    string? Telephone, string? ResidenceFaxNumber, string? Mobile, string? Email, string? Website,
    string? ContactPerson, string? ContactPersonPhoneNumber, string? ContactPersonFlatNumber,
    string? AreaSheik, string? LocationLandmark);
public sealed record BankAccountResponse(Guid Id, string? AccountTypeCode, string AccountNumber, string? BankCode,
    string? BankName, string? BankBranch, string? MICRCode, DateOnly? ClearingDate, bool IsDefault);
public sealed record CustomerDocumentResponse(Guid Id, Guid DocumentDefinitionId, string DocumentCode,
    string? DocumentCategory, string? DocumentDescription, bool IsMandatory, bool? IsImageRequired,
    bool IsCollected, bool IsScanned, string? Remarks, string? Value, string? ValueSource,
    string? DmsReference, string? ViewMetadata);
public sealed record CustomerTrackResponse(Guid Id, string? LineOfBusinessCode, string? AccountNumber,
    string TrackTypeCode, string? TrackTypeName, DateOnly TrackDate, string? Reason, string? ByLogin,
    DateOnly? ReleaseDate, string? ReleasedBy, CustomerTrackStatus Status);
public sealed record CustomerCreditResponse(Guid CustomerId, string? LineOfBusiness, string? ProductGroup,
    decimal SanctionedAmount, decimal ApprovedAmount, decimal UtilizedAmount, decimal RemainingAmount,
    DateOnly? ValidUpTo, string? FacilityType);
public sealed record FactoringSubLimitResponse(Guid Id, bool FactoringApplicable, string? FactoringTypeCode,
    string? FactoringTypeName, decimal? Limit, string? Covenant, string? CovenantClause, DateOnly? CutOffDate);

public sealed record CustomerResponse(
    Guid Id, string CustomerCode, Guid CompanyId, bool IsCustomer, bool IsGuarantor, bool IsCoApplicant,
    string CustomerTypeCode, CustomerReferenceProjection? Group, string? GroupName, CustomerReferenceProjection? Industry,
    string? IndustryName, string? CustomerPostingGroupCode, Guid? GLAccountId, string? GLName,
    CustomerReferenceProjection? Constitution, string? ConstitutionName, CustomerReferenceProjection? Nationality,
    string? NationalityName, CustomerReferenceProjection? Title,
    CustomerReferenceProjection? MaritalStatus, CustomerReferenceProjection? CustomerBusinessType,
    CustomerReferenceProjection? CustomerBusinessSubType, CustomerReferenceProjection? IndustrySector,
    CustomerReferenceProjection? SubSector, string? FirstName, string? SecondName, string? ThirdName,
    string? FourthName, string? FifthName, string? SixthName, string? Notes, bool? IsVip,
    bool? IsSeniorMember, string? FinancialRequirement, string? Accommodation, bool? IsRelatedParty,
    string? RelationCode, string? EmployerEconomicActivityCode, CustomerLifecycleStatus Status,
    DateTime CreatedAtUtc, DateTime UpdatedAtUtc, IReadOnlyCollection<CustomerAddressResponse> Addresses,
    IndividualCustomerDetailsRequest? IndividualDetails, NonIndividualCustomerDetailsRequest? NonIndividualDetails,
    IReadOnlyCollection<BankAccountResponse> BankAccounts, IReadOnlyCollection<CustomerDocumentResponse> Documents,
    IReadOnlyCollection<CustomerTrackResponse> Tracks, CustomerCreditResponse? Credit,
    IReadOnlyCollection<FactoringSubLimitResponse> FactoringSubLimits);

public sealed record LookupCatalogEntry(string Module, string LookupType, string Code, string Value, int DisplayOrder);
public sealed record CustomerLookupCatalogResponse(IReadOnlyCollection<LookupCatalogEntry> Values);
public sealed record CustomerCodeLookup(string CustomerCode, Guid CustomerId);
public sealed record CopyCommunicationAddressRequest(string CustomerCode);
public sealed record CopyCommunicationAddressResult(string CustomerCode, CustomerAddressResponse PermanentAddress);
public sealed record CustomerApplicabilityContext(string CustomerTypeCode, string? OccupationCode,
    bool? IsSsEmployee, string? BusinessTypeCode, string? BusinessSubTypeCode,
    bool? FactoringApplicable, string? ConstitutionCode, string Mode);
public enum CustomerFieldState { Required, Optional, ReadOnly, Unavailable }
public sealed record CustomerFieldApplicability(string FieldName, CustomerFieldState State, string? Reason);
public sealed record MasterReferenceValidationResult(
    IReadOnlyDictionary<string, CustomerReferenceProjection> References,
    IReadOnlyCollection<CustomerDocumentResponse> ApplicableDocuments,
    CustomerAddressResponse? CommunicationAddress,
    CustomerAddressResponse? PermanentAddress);
