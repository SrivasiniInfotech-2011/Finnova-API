using MediatR;
using Finnova.Models.Contracts.Accounts;

namespace Finnova.Service.Transactions.Commands.CreateTransaction;

public record CreateTransactionCommand(
    Guid AccountId,
    string Type,
    decimal Amount,
    string? Description,
    Guid? CounterpartyAccountId
) : IRequest<TransactionResponse>;
