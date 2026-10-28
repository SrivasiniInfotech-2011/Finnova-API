namespace Finnova.Models.Domain.Entities;

public sealed class IndividualCustomerDetails
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid CustomerId { get; private set; }
    public string? GenderCode { get; private set; }
    public DateOnly? DateOfBirth { get; private set; }
    public string? SpouseName { get; private set; }
    public int? Children { get; private set; }
    public int? TotalDependents { get; private set; }
    public DateOnly? WeddingAnniversaryDate { get; private set; }
    public string? HouseOrFlat { get; private set; }
    public bool? Own { get; private set; }
    public decimal? CurrentMarketValue { get; private set; }
    public decimal? RemainingLoanValue { get; private set; }
    public decimal? TotalNetWorth { get; private set; }
    public string? Qualification { get; private set; }
    public string? Profession { get; private set; }
    public string? IndiaIdentityType { get; private set; }
    public string? IndiaIdentityReference { get; private set; }
    public DateOnly? IdentityIssueDate { get; private set; }
    public DateOnly? IdentityExpiryDate { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public void Initialize(
        string? genderCode, DateOnly? dateOfBirth, string? spouseName, int? children, int? totalDependents,
        DateOnly? weddingAnniversaryDate, string? houseOrFlat, bool? own, decimal? currentMarketValue,
        decimal? remainingLoanValue, decimal? totalNetWorth, string? qualification, string? profession,
        string? indiaIdentityType, string? indiaIdentityReference, DateOnly? identityIssueDate,
        DateOnly? identityExpiryDate)
    {
        GenderCode = genderCode;
        DateOfBirth = dateOfBirth;
        SpouseName = spouseName;
        Children = children;
        TotalDependents = totalDependents;
        WeddingAnniversaryDate = weddingAnniversaryDate;
        HouseOrFlat = houseOrFlat;
        Own = own;
        CurrentMarketValue = currentMarketValue;
        RemainingLoanValue = remainingLoanValue;
        TotalNetWorth = totalNetWorth;
        Qualification = qualification;
        Profession = profession;
        IndiaIdentityType = indiaIdentityType;
        IndiaIdentityReference = indiaIdentityReference;
        IdentityIssueDate = identityIssueDate;
        IdentityExpiryDate = identityExpiryDate;
        UpdatedAtUtc = DateTime.UtcNow;
    }
    public DateTime UpdatedAtUtc { get; private set; } = DateTime.UtcNow;
}
