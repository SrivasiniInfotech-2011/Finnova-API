using Mapster;
using MediatR;
using Finnova.Models.Contracts.Accounts;
using Finnova.Repository.Interfaces;

namespace Finnova.Service.Transactions.Queries.GetTransactionsByAccount;

public class GetTransactionsByAccountQueryHandler : IRequestHandler<GetTransactionsByAccountQuery, List<TransactionResponse>>
{
    private readonly ITransactionRepository _repository;

    public GetTransactionsByAccountQueryHandler(ITransactionRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<TransactionResponse>> Handle(GetTransactionsByAccountQuery request, CancellationToken cancellationToken)
    {
        var transactions = await _repository.GetByAccountIdPagedAsync(
            request.AccountId, request.Page, request.PageSize, cancellationToken);
        return transactions.Adapt<List<TransactionResponse>>();
    }
}
