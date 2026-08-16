namespace Finnova.Models.Contracts.Accounts;

public record AccountResponse(
    Guid Id,
    string AccountNumber,
    string AccountName,
    string Type,
    string Status,
    decimal Balance,
    string Currency,
    Guid? UserId,
    Guid? OrganizationId,
    string? BranchCode,
    string? IfscCode,
    DateTime OpenedAt,
    DateTime? ClosedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
