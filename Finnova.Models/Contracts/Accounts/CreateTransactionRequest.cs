namespace Finnova.Models.Contracts.Accounts;

public record CreateTransactionRequest(
    Guid AccountId,
    string Type,
    decimal Amount,
    string? Description,
    Guid? CounterpartyAccountId
);
