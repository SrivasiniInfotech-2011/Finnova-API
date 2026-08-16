using Mapster;
using MediatR;
using Finnova.Models.Contracts.Accounts;
using Finnova.Repository.Interfaces;

namespace Finnova.Service.Accounts.Queries.GetAccountsByUser;

public class GetAccountsByUserQueryHandler : IRequestHandler<GetAccountsByUserQuery, List<AccountResponse>>
{
    private readonly IAccountRepository _repository;

    public GetAccountsByUserQueryHandler(IAccountRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<AccountResponse>> Handle(GetAccountsByUserQuery request, CancellationToken cancellationToken)
    {
        var accounts = await _repository.GetByUserIdAsync(request.UserId, cancellationToken);
        return accounts.Adapt<List<AccountResponse>>();
    }
}
