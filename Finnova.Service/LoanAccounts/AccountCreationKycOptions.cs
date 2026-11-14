namespace Finnova.Service.LoanAccounts;

/// <summary>Bound from "AccountCreation:Kyc". Document codes are assumed values (A26) to be confirmed against the Document Master.</summary>
public sealed class AccountCreationKycOptions
{
    public const string SectionName = "AccountCreation:Kyc";
    public string PanDocumentCode { get; set; } = "PAN";
    public string AadhaarDocumentCode { get; set; } = "AADHAAR";
}