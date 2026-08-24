namespace Finnova.Models.Contracts.Branches;

public record UpdateBranchRequest(
    string BranchType,
    string CorporateCode,
    string StateCode,
    string BranchCode,
    string BranchName,
    string Address,
    string Landmark,
    string State,
    string Country,
    string? Pincode,
    string? Telephone,
    string? Mobile,
    bool IsActive,
    bool IsOperational
);
