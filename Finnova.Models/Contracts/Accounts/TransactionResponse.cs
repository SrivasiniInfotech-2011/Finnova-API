namespace Finnova.Models.Contracts.Accounts;

public record TransactionResponse(
    Guid Id,
    Guid AccountId,
    string Type,
    decimal Amount,
    decimal BalanceAfter,
    string? Description,
    string? ReferenceNumber,
    string Status,
    Guid? CounterpartyAccountId,
    DateTime TransactionDate,
    DateTime CreatedAt
);
