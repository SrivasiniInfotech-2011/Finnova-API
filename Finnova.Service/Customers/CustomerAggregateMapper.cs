using System.Text.Json;
using Finnova.Models.Contracts.Customers;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Service.Customers.References;
using Finnova.Service.Customers.Validation;

namespace Finnova.Service.Customers;

public sealed class CustomerAggregateMapper : ICustomerAggregateMapper
{
    public string NormalizeIdentity(CustomerCreateRequest request)
        => DuplicateIdentityRules.Normalize(request.IndividualDetails?.IndiaIdentityReference ?? string.Empty);

    public Customer Create(CustomerCreateRequest request, string customerCode, MasterReferenceValidationResult references)
    {
        var customer = new Customer();
        customer.SetGeneratedCode(customerCode);
        ApplyScalars(customer, request.CompanyId, request.IsCustomer, request.IsGuarantor, request.IsCoApplicant,
            request.CustomerTypeCode, request.GroupCode, request.IndustryCode, request.CustomerPostingGroupCode,
            request.GLAccountId, request.ConstitutionCode, request.NationalityCode, request.TitleCode,
            request.MaritalStatusCode, request.CustomerBusinessTypeCode, request.CustomerBusinessSubTypeCode,
            request.IndustrySectorCode, request.SubSectorCode, request.FirstName, request.SecondName,
            request.ThirdName, request.FourthName, request.FifthName, request.SixthName, request.Notes,
            request.IsVip, request.IsSeniorMember, request.FinancialRequirement, request.Accommodation,
            request.IsRelatedParty, request.RelationCode, request.EmployerEconomicActivityCode);

        foreach (var address in references.ApprovedAddresses())
            customer.Addresses.Add(ToAddress(address));
        if (request.IndividualDetails is not null)
            customer.SetIndividualDetails(ToIndividual(request.IndividualDetails));
        if (request.NonIndividualDetails is not null)
            customer.SetNonIndividualDetails(ToNonIndividual(request.NonIndividualDetails));
        foreach (var bank in request.BankAccounts)
            customer.BankAccounts.Add(ToBank(bank));
        foreach (var document in request.Documents)
            customer.Documents.Add(ToDocument(document, references));
        foreach (var track in request.Tracks)
            customer.Tracks.Add(ToTrack(track));
        foreach (var limit in request.FactoringSubLimits)
            customer.FactoringSubLimits.Add(ToFactoring(limit));
        return customer;
    }

    public void ApplyModify(Customer customer, CustomerModifyRequest request, MasterReferenceValidationResult references)
    {
        ApplyScalars(customer, request.CompanyId, customer.IsCustomer, customer.IsGuarantor, customer.IsCoApplicant,
            request.CustomerTypeCode ?? customer.CustomerTypeCode, request.GroupCode, request.IndustryCode,
            request.CustomerPostingGroupCode, request.GLAccountId, request.ConstitutionCode, request.NationalityCode,
            request.TitleCode, request.MaritalStatusCode, request.CustomerBusinessTypeCode,
            request.CustomerBusinessSubTypeCode, request.IndustrySectorCode, request.SubSectorCode,
            request.FirstName, request.SecondName, request.ThirdName, request.FourthName, request.FifthName,
            request.SixthName, request.Notes, request.IsVip, request.IsSeniorMember, request.FinancialRequirement,
            request.Accommodation, request.IsRelatedParty, request.RelationCode, request.EmployerEconomicActivityCode);

        if (request.Addresses.Count == customer.Addresses.Count && request.Addresses.Count > 0)
        {
            var approved = references.ApprovedAddresses().ToArray();
            for (var i = 0; i < approved.Length; i++)
                UpdateAddress(customer.Addresses[i], approved[i]);
        }
        else
        {
            customer.Addresses.Clear();
            foreach (var address in references.ApprovedAddresses())
                customer.Addresses.Add(ToAddress(address));
        }
        customer.SetIndividualDetails(null);
        customer.SetNonIndividualDetails(null);
        if (request.IndividualDetails is not null)
            customer.SetIndividualDetails(ToIndividual(request.IndividualDetails));
        if (request.NonIndividualDetails is not null)
            customer.SetNonIndividualDetails(ToNonIndividual(request.NonIndividualDetails));
        customer.BankAccounts.Clear();
        foreach (var bank in request.BankAccounts)
            customer.BankAccounts.Add(ToBank(bank));
        customer.Documents.Clear();
        foreach (var document in request.Documents)
            customer.Documents.Add(ToDocument(document, references));
        customer.Tracks.Clear();
        foreach (var track in request.Tracks)
            customer.Tracks.Add(ToTrack(track));
        customer.FactoringSubLimits.Clear();
        foreach (var limit in request.FactoringSubLimits)
            customer.FactoringSubLimits.Add(ToFactoring(limit));
        customer.Touch();
    }

    public CustomerAuditEntry CreatedAudit(Customer customer, string actorId)
        => CustomerAuditEntry.Create(customer.Id, CustomerAuditAction.Created, actorId,
            JsonSerializer.Serialize(new { Action = "Created", customer.CustomerCode }));

    public CustomerAuditEntry ModifiedAudit(Customer customer, string actorId)
        => CustomerAuditEntry.Create(customer.Id, CustomerAuditAction.Modified, actorId,
            JsonSerializer.Serialize(new { Action = "Modified", customer.CustomerCode }));

    public CustomerResponse Response(Customer customer, MasterReferenceValidationResult references)
    {
        var refs = references.References;
        CustomerReferenceProjection? Ref(string key, string? code)
            => code is null ? null : refs.TryGetValue(key, out var value) ? value : new(code, null);
        string? Name(string key) => refs.TryGetValue(key, out var value) ? value.Name : null;

        return ToResponse(customer,
            Ref("group", customer.GroupCode), Name("group"),
            Ref("industry", customer.IndustryCode), Name("industry"),
            Ref("posting", customer.CustomerPostingGroupCode)?.Code, Name("posting"),
            Ref("constitution", customer.ConstitutionCode), Name("constitution"),
            Ref("nationality", customer.NationalityCode), Name("nationality"),
            Ref("title", customer.TitleCode), Ref("marital", customer.MaritalStatusCode),
            Ref("businessType", customer.CustomerBusinessTypeCode), Ref("businessSubType", customer.CustomerBusinessSubTypeCode),
            Ref("sector", customer.IndustrySectorCode), Ref("subSector", customer.SubSectorCode));
    }

    internal static CustomerResponse ToResponse(Customer customer,
        CustomerReferenceProjection? group, string? groupName,
        CustomerReferenceProjection? industry, string? industryName,
        string? postingCode, string? glName,
        CustomerReferenceProjection? constitution, string? constitutionName,
        CustomerReferenceProjection? nationality, string? nationalityName,
        CustomerReferenceProjection? title, CustomerReferenceProjection? marital,
        CustomerReferenceProjection? businessType, CustomerReferenceProjection? businessSubType,
        CustomerReferenceProjection? sector, CustomerReferenceProjection? subSector)
        => new(customer.Id, customer.CustomerCode, customer.CompanyId, customer.IsCustomer, customer.IsGuarantor,
            customer.IsCoApplicant, customer.CustomerTypeCode, group, groupName, industry, industryName,
            postingCode, customer.GLAccountId, glName, constitution, constitutionName, nationality, nationalityName,
            title, marital, businessType, businessSubType, sector, subSector, customer.FirstName,
            customer.SecondName, customer.ThirdName, customer.FourthName, customer.FifthName, customer.SixthName,
            customer.Notes, customer.IsVip, customer.IsSeniorMember, customer.FinancialRequirement,
            customer.Accommodation, customer.IsRelatedParty, customer.RelationCode,
            customer.EmployerEconomicActivityCode, customer.Status, customer.CreatedAtUtc, customer.UpdatedAtUtc,
            customer.Addresses.Select(ToAddressResponse).ToArray(), ToIndividualRequest(customer.IndividualDetails),
            ToNonIndividualRequest(customer.NonIndividualDetails), customer.BankAccounts.Select(ToBankResponse).ToArray(),
            customer.Documents.Select(ToDocumentResponse).ToArray(), customer.Tracks.Select(ToTrackResponse).ToArray(),
            null, customer.FactoringSubLimits.Select(ToFactoringResponse).ToArray());

    private static void ApplyScalars(Customer c, Guid companyId, bool isCustomer, bool isGuarantor, bool isCoApplicant,
        string customerTypeCode, string? groupCode, string? industryCode, string? postingCode, Guid? glAccountId,
        string? constitutionCode, string? nationalityCode, string? titleCode, string? maritalStatusCode,
        string? businessType, string? businessSubType, string? sector, string? subSector, string? firstName,
        string? secondName, string? thirdName, string? fourthName, string? fifthName, string? sixthName,
        string? notes, bool? isVip, bool? isSeniorMember, string? financialRequirement, string? accommodation,
        bool? isRelatedParty, string? relationCode, string? employerActivity)
        => c.Initialize(companyId, isCustomer, isGuarantor, isCoApplicant, customerTypeCode, groupCode, industryCode,
            postingCode, glAccountId, constitutionCode, nationalityCode, titleCode, maritalStatusCode, businessType,
            businessSubType, sector, subSector, firstName, secondName, thirdName, fourthName, fifthName, sixthName,
            notes, isVip, isSeniorMember, financialRequirement, accommodation, isRelatedParty, relationCode,
            employerActivity);

    private static CustomerAddress ToAddress(CustomerAddressResponse x)
    {
        var row = new CustomerAddress();
        row.Initialize(x.AddressType, x.HouseNumber, x.BlockNumber, x.PostalCode, x.City, x.State, x.Country,
            x.POBoxNumber, x.WayNumber, x.Telephone, x.ResidenceFaxNumber, x.Mobile, x.Email, x.Website,
            x.ContactPerson, x.ContactPersonPhoneNumber, x.ContactPersonFlatNumber, x.AreaSheik, x.LocationLandmark);
        return row;
    }

    private static void UpdateAddress(CustomerAddress row, CustomerAddressResponse x)
        => row.Initialize(x.AddressType, x.HouseNumber, x.BlockNumber, x.PostalCode, x.City, x.State, x.Country,
            x.POBoxNumber, x.WayNumber, x.Telephone, x.ResidenceFaxNumber, x.Mobile, x.Email, x.Website,
            x.ContactPerson, x.ContactPersonPhoneNumber, x.ContactPersonFlatNumber, x.AreaSheik, x.LocationLandmark);

    private static CustomerAddressResponse ToAddressResponse(CustomerAddress x)
        => new(x.AddressType, x.HouseNumber, x.BlockNumber, x.PostalCode, x.City, x.State, x.Country,
            x.POBoxNumber, x.WayNumber, x.Telephone, x.ResidenceFaxNumber, x.Mobile, x.Email, x.Website,
            x.ContactPerson, x.ContactPersonPhoneNumber, x.ContactPersonFlatNumber, x.AreaSheik, x.LocationLandmark);

    private static IndividualCustomerDetails ToIndividual(IndividualCustomerDetailsRequest x)
    {
        var row = new IndividualCustomerDetails();
        row.Initialize(x.GenderCode, x.DateOfBirth, x.SpouseName, x.Children, x.TotalDependents,
            x.WeddingAnniversaryDate, x.HouseOrFlat, x.Own, x.CurrentMarketValue, x.RemainingLoanValue,
            x.TotalNetWorth, x.Qualification, x.Profession, x.IndiaIdentityType, x.IndiaIdentityReference,
            x.IdentityIssueDate, x.IdentityExpiryDate);
        return row;
    }

    private static IndividualCustomerDetailsRequest? ToIndividualRequest(IndividualCustomerDetails? x)
        => x is null ? null : new(x.GenderCode, x.DateOfBirth, x.SpouseName, x.Children, x.TotalDependents,
            x.WeddingAnniversaryDate, x.HouseOrFlat, x.Own, x.CurrentMarketValue, x.RemainingLoanValue,
            x.TotalNetWorth, x.Qualification, x.Profession, x.IndiaIdentityType, x.IndiaIdentityReference,
            x.IdentityIssueDate, x.IdentityExpiryDate);

    private static NonIndividualCustomerDetails ToNonIndividual(NonIndividualCustomerDetailsRequest x)
    {
        var row = new NonIndividualCustomerDetails();
        row.Initialize(x.PublicOrCloselyHeldCode, x.NumberOfDirectorsOrPartners, x.KeyDecisionMaker,
            x.ListedAtStockExchange, x.PaidUpCapital, x.FaceValueOfShares, x.BookValueOfShares, x.BusinessProfile,
            x.MonthlyBusinessIncome, x.GeographicalCoverage, x.NumberOfBranches, x.GovernmentInstitutionalParticipation,
            x.PromoterStakePercentage, x.JVPartnerName, x.JVPartnerStake, x.CEOOrMDName, x.AgeOfCEOOrMD,
            x.ExperienceInYears, x.ResidentialAddress, x.WeddingAnniversaryDate, x.CustomerBusinessTypeCode,
            x.CustomerBusinessSubTypeCode, x.IndustrySectorCode, x.SubSectorCode, x.CRNumber, x.CRIssueDate,
            x.CRExpiryDate, x.BusinessActivities, x.FactoringApplicable, x.FactoringTypeCode, x.FactoringLimit,
            x.Covenants, x.CovenantClause);
        return row;
    }

    private static NonIndividualCustomerDetailsRequest? ToNonIndividualRequest(NonIndividualCustomerDetails? x)
        => x is null ? null : new(x.PublicOrCloselyHeldCode, x.NumberOfDirectorsOrPartners, x.KeyDecisionMaker,
            x.ListedAtStockExchange, x.PaidUpCapital, x.FaceValueOfShares, x.BookValueOfShares, x.BusinessProfile,
            x.MonthlyBusinessIncome, x.GeographicalCoverage, x.NumberOfBranches, x.GovernmentInstitutionalParticipation,
            x.PromoterStakePercentage, x.JVPartnerName, x.JVPartnerStake, x.CEOOrMDName, x.AgeOfCEOOrMD,
            x.ExperienceInYears, x.ResidentialAddress, x.WeddingAnniversaryDate, x.CustomerBusinessTypeCode,
            x.CustomerBusinessSubTypeCode, x.IndustrySectorCode, x.SubSectorCode, x.CRNumber, x.CRIssueDate,
            x.CRExpiryDate, x.BusinessActivities, x.FactoringApplicable, x.FactoringTypeCode, x.FactoringLimit,
            x.Covenants, x.CovenantClause);

    private static BankAccount ToBank(BankAccountRequest x)
    {
        var row = new BankAccount();
        row.Initialize(x.AccountTypeCode, x.AccountNumber, x.BankCode, x.MICRCode, x.ClearingDate, x.Address,
            x.ContractNumber, x.SubContractNumber, x.IsDefault);
        return row;
    }

    private static BankAccountResponse ToBankResponse(BankAccount x)
        => new(x.Id, x.AccountTypeCode, x.AccountNumber, x.BankCode, null, null, x.MICRCode, x.ClearingDate, x.IsDefault);

    private static CustomerDocumentReference ToDocument(CustomerDocumentReferenceRequest x, MasterReferenceValidationResult references)
    {
        var definition = references.ApplicableDocuments.FirstOrDefault(d => d.Id == x.DocumentDefinitionId);
        var row = new CustomerDocumentReference();
        row.Initialize(x.ConstitutionCode, x.DocumentDefinitionId, x.DocumentCode, definition?.DocumentCategory,
            definition?.DocumentDescription, definition?.IsMandatory ?? false, definition?.IsImageRequired, x.Remarks,
            x.IsCollected, x.IsScanned, x.Value, x.ValueSource, x.DmsReference, x.ViewMetadata);
        return row;
    }

    private static CustomerDocumentResponse ToDocumentResponse(CustomerDocumentReference x)
        => new(x.Id, x.DocumentDefinitionId, x.DocumentCode, x.DocumentCategory, x.DocumentDescription,
            x.IsMandatory, x.IsImageRequired, x.IsCollected, x.IsScanned, x.Remarks, x.Value, x.ValueSource,
            x.DmsReference, x.ViewMetadata);

    private static CustomerTrackEntry ToTrack(CustomerTrackRequest x)
    {
        var row = new CustomerTrackEntry();
        row.Initialize(x.LineOfBusinessCode, x.AccountNumber, x.TrackTypeCode, x.TrackDate, x.Reason, x.ByLogin);
        return row;
    }

    private static CustomerTrackResponse ToTrackResponse(CustomerTrackEntry x)
        => new(x.Id, x.LineOfBusinessCode, x.AccountNumber, x.TrackTypeCode, null, x.TrackDate, x.Reason,
            x.ByLogin, x.ReleaseDate, x.ReleasedBy, x.Status);

    private static FactoringSubLimit ToFactoring(FactoringSubLimitRequest x)
    {
        var row = new FactoringSubLimit();
        row.Initialize(x.FactoringApplicable, x.FactoringTypeCode, x.Limit, x.Covenant, x.CovenantClause, x.CutOffDate);
        return row;
    }

    private static FactoringSubLimitResponse ToFactoringResponse(FactoringSubLimit x)
        => new(x.Id, x.FactoringApplicable, x.FactoringTypeCode, null, x.Limit, x.Covenant, x.CovenantClause, x.CutOffDate);
}

internal static class MasterReferenceValidationResultExtensions
{
    public static IEnumerable<CustomerAddressResponse> ApprovedAddresses(this MasterReferenceValidationResult references)
    {
        if (references.CommunicationAddress is not null)
            yield return references.CommunicationAddress;
        if (references.PermanentAddress is not null)
            yield return references.PermanentAddress;
    }
}
