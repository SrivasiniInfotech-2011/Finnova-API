using Mapster;
using MediatR;
using Finnova.Models.Contracts.Accounts;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Interfaces;

namespace Finnova.Service.Accounts.Commands.CloseAccount;

public class CloseAccountCommandHandler : IRequestHandler<CloseAccountCommand, AccountResponse?>
{
    private readonly IAccountRepository _repository;

    public CloseAccountCommandHandler(IAccountRepository repository)
    {
        _repository = repository;
    }

    public async Task<AccountResponse?> Handle(CloseAccountCommand request, CancellationToken cancellationToken)
    {
        var account = await _repository.GetByIdAsync(request.AccountId, cancellationToken);
        if (account is null) return null;

        account.Status = AccountStatus.Closed;
        account.ClosedAt = DateTime.UtcNow;
        account.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(account, cancellationToken);
        return account.Adapt<AccountResponse>();
    }
}
