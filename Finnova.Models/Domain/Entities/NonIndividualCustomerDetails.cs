namespace Finnova.Models.Domain.Entities;

public sealed class NonIndividualCustomerDetails
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid CustomerId { get; private set; }
    public string? PublicOrCloselyHeldCode { get; private set; }
    public int? NumberOfDirectorsOrPartners { get; private set; }
    public string? KeyDecisionMaker { get; private set; }
    public bool? ListedAtStockExchange { get; private set; }
    public decimal? PaidUpCapital { get; private set; }
    public decimal? FaceValueOfShares { get; private set; }
    public decimal? BookValueOfShares { get; private set; }
    public string? BusinessProfile { get; private set; }
    public decimal? MonthlyBusinessIncome { get; private set; }
    public string? GeographicalCoverage { get; private set; }
    public int? NumberOfBranches { get; private set; }
    public bool? GovernmentInstitutionalParticipation { get; private set; }
    public decimal? PromoterStakePercentage { get; private set; }
    public string? JVPartnerName { get; private set; }
    public decimal? JVPartnerStake { get; private set; }
    public string? CEOOrMDName { get; private set; }
    public int? AgeOfCEOOrMD { get; private set; }
    public int? ExperienceInYears { get; private set; }
    public string? ResidentialAddress { get; private set; }
    public DateOnly? WeddingAnniversaryDate { get; private set; }
    public string? CustomerBusinessTypeCode { get; private set; }
    public string? CustomerBusinessSubTypeCode { get; private set; }
    public string? IndustrySectorCode { get; private set; }
    public string? SubSectorCode { get; private set; }
    public string? CRNumber { get; private set; }
    public DateOnly? CRIssueDate { get; private set; }
    public DateOnly? CRExpiryDate { get; private set; }
    public string? BusinessActivities { get; private set; }
    public bool? FactoringApplicable { get; private set; }
    public string? FactoringTypeCode { get; private set; }
    public decimal? FactoringLimit { get; private set; }
    public string? Covenants { get; private set; }
    public string? CovenantClause { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public void Initialize(
        string? publicOrCloselyHeldCode, int? numberOfDirectorsOrPartners, string? keyDecisionMaker,
        bool? listedAtStockExchange, decimal? paidUpCapital, decimal? faceValueOfShares,
        decimal? bookValueOfShares, string? businessProfile, decimal? monthlyBusinessIncome,
        string? geographicalCoverage, int? numberOfBranches, bool? governmentInstitutionalParticipation,
        decimal? promoterStakePercentage, string? jvPartnerName, decimal? jvPartnerStake,
        string? ceoOrMdName, int? ageOfCeoOrMd, int? experienceInYears, string? residentialAddress,
        DateOnly? weddingAnniversaryDate, string? customerBusinessTypeCode, string? customerBusinessSubTypeCode,
        string? industrySectorCode, string? subSectorCode, string? crNumber, DateOnly? crIssueDate,
        DateOnly? crExpiryDate, string? businessActivities, bool? factoringApplicable, string? factoringTypeCode,
        decimal? factoringLimit, string? covenants, string? covenantClause)
    {
        PublicOrCloselyHeldCode = publicOrCloselyHeldCode;
        NumberOfDirectorsOrPartners = numberOfDirectorsOrPartners;
        KeyDecisionMaker = keyDecisionMaker;
        ListedAtStockExchange = listedAtStockExchange;
        PaidUpCapital = paidUpCapital;
        FaceValueOfShares = faceValueOfShares;
        BookValueOfShares = bookValueOfShares;
        BusinessProfile = businessProfile;
        MonthlyBusinessIncome = monthlyBusinessIncome;
        GeographicalCoverage = geographicalCoverage;
        NumberOfBranches = numberOfBranches;
        GovernmentInstitutionalParticipation = governmentInstitutionalParticipation;
        PromoterStakePercentage = promoterStakePercentage;
        JVPartnerName = jvPartnerName;
        JVPartnerStake = jvPartnerStake;
        CEOOrMDName = ceoOrMdName;
        AgeOfCEOOrMD = ageOfCeoOrMd;
        ExperienceInYears = experienceInYears;
        ResidentialAddress = residentialAddress;
        WeddingAnniversaryDate = weddingAnniversaryDate;
        CustomerBusinessTypeCode = customerBusinessTypeCode;
        CustomerBusinessSubTypeCode = customerBusinessSubTypeCode;
        IndustrySectorCode = industrySectorCode;
        SubSectorCode = subSectorCode;
        CRNumber = crNumber;
        CRIssueDate = crIssueDate;
        CRExpiryDate = crExpiryDate;
        BusinessActivities = businessActivities;
        FactoringApplicable = factoringApplicable;
        FactoringTypeCode = factoringTypeCode;
        FactoringLimit = factoringLimit;
        Covenants = covenants;
        CovenantClause = covenantClause;
        UpdatedAtUtc = DateTime.UtcNow;
    }
    public DateTime UpdatedAtUtc { get; private set; } = DateTime.UtcNow;
}
