using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

public sealed class Customer
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string CustomerCode { get; private set; } = string.Empty; // generated, exactly 12, immutable
    public Guid CompanyId { get; private set; } // explicit Company scope for GL validation
    public bool IsCustomer { get; private set; }
    public bool IsGuarantor { get; private set; }
    public bool IsCoApplicant { get; private set; }
    public string CustomerTypeCode { get; private set; } = string.Empty;
    public string? GroupCode { get; private set; } // max 4 numeric digits
    public string? IndustryCode { get; private set; } // max 3 characters
    public string? CustomerPostingGroupCode { get; private set; }
    public Guid? GLAccountId { get; private set; }
    public string? ConstitutionCode { get; private set; }
    public string? NationalityCode { get; private set; }
    public string? TitleCode { get; private set; }
    public string? MaritalStatusCode { get; private set; }
    public string? CustomerBusinessTypeCode { get; private set; }
    public string? CustomerBusinessSubTypeCode { get; private set; }
    public string? IndustrySectorCode { get; private set; }
    public string? SubSectorCode { get; private set; }
    public string? FirstName { get; private set; }
    public string? SecondName { get; private set; }
    public string? ThirdName { get; private set; }
    public string? FourthName { get; private set; }
    public string? FifthName { get; private set; }
    public string? SixthName { get; private set; }
    public string? Notes { get; private set; }
    public bool? IsVip { get; private set; }
    public bool? IsSeniorMember { get; private set; }
    public string? FinancialRequirement { get; private set; }
    public string? Accommodation { get; private set; }
    public bool? IsRelatedParty { get; private set; }
    public string? RelationCode { get; private set; }
    public string? EmployerEconomicActivityCode { get; private set; }
    public CustomerLifecycleStatus Status { get; private set; } = CustomerLifecycleStatus.Active;
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; private set; } = DateTime.UtcNow;

    public List<CustomerAddress> Addresses { get; private set; } = new();
    public IndividualCustomerDetails? IndividualDetails { get; private set; }
    public NonIndividualCustomerDetails? NonIndividualDetails { get; private set; }
    public List<BankAccount> BankAccounts { get; private set; } = new();
    public List<CustomerDocumentReference> Documents { get; private set; } = new();
    public List<CustomerTrackEntry> Tracks { get; private set; } = new();
    public List<FactoringSubLimit> FactoringSubLimits { get; private set; } = new();

    public void SetGeneratedCode(string code) => CustomerCode = code;

    public void Initialize(
        Guid companyId, bool isCustomer, bool isGuarantor, bool isCoApplicant, string customerTypeCode,
        string? groupCode, string? industryCode, string? customerPostingGroupCode, Guid? glAccountId,
        string? constitutionCode, string? nationalityCode, string? titleCode, string? maritalStatusCode,
        string? customerBusinessTypeCode, string? customerBusinessSubTypeCode, string? industrySectorCode,
        string? subSectorCode, string? firstName, string? secondName, string? thirdName, string? fourthName,
        string? fifthName, string? sixthName, string? notes, bool? isVip, bool? isSeniorMember,
        string? financialRequirement, string? accommodation, bool? isRelatedParty, string? relationCode,
        string? employerEconomicActivityCode)
    {
        CompanyId = companyId;
        IsCustomer = isCustomer;
        IsGuarantor = isGuarantor;
        IsCoApplicant = isCoApplicant;
        CustomerTypeCode = customerTypeCode;
        GroupCode = groupCode;
        IndustryCode = industryCode;
        CustomerPostingGroupCode = customerPostingGroupCode;
        GLAccountId = glAccountId;
        ConstitutionCode = constitutionCode;
        NationalityCode = nationalityCode;
        TitleCode = titleCode;
        MaritalStatusCode = maritalStatusCode;
        CustomerBusinessTypeCode = customerBusinessTypeCode;
        CustomerBusinessSubTypeCode = customerBusinessSubTypeCode;
        IndustrySectorCode = industrySectorCode;
        SubSectorCode = subSectorCode;
        FirstName = firstName;
        SecondName = secondName;
        ThirdName = thirdName;
        FourthName = fourthName;
        FifthName = fifthName;
        SixthName = sixthName;
        Notes = notes;
        IsVip = isVip;
        IsSeniorMember = isSeniorMember;
        FinancialRequirement = financialRequirement;
        Accommodation = accommodation;
        IsRelatedParty = isRelatedParty;
        RelationCode = relationCode;
        EmployerEconomicActivityCode = employerEconomicActivityCode;
        Touch();
    }

    public void SetIndividualDetails(IndividualCustomerDetails? details) => IndividualDetails = details;
    public void SetNonIndividualDetails(NonIndividualCustomerDetails? details) => NonIndividualDetails = details;
    public void Touch() => UpdatedAtUtc = DateTime.UtcNow;
}
