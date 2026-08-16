namespace Finnova.Models.Contracts.Accounts;

public record CreateAccountRequest(
    string AccountName,
    string Type,
    string Currency,
    Guid? UserId,
    Guid? OrganizationId,
    string? BranchCode,
    string? IfscCode,
    decimal InitialDeposit
);
