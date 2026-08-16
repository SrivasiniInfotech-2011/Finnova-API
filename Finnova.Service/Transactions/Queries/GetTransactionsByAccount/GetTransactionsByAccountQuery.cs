using MediatR;
using Finnova.Models.Contracts.Accounts;

namespace Finnova.Service.Transactions.Queries.GetTransactionsByAccount;

public record GetTransactionsByAccountQuery(Guid AccountId, int Page = 1, int PageSize = 20) : IRequest<List<TransactionResponse>>;
