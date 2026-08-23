using MediatR;
using Finnova.Models.Contracts.Accounts;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;

namespace Finnova.Service.Accounts.Commands.CreateAccount;

public class CreateAccountCommandHandler : IRequestHandler<CreateAccountCommand, AccountResponse>
{
    private readonly IAccountRepository _repository;

    public CreateAccountCommandHandler(IAccountRepository repository)
    {
        _repository = repository;
    }

    public async Task<AccountResponse> Handle(CreateAccountCommand request, CancellationToken cancellationToken)
    {
        var account = new Account
        {
            AccountNumber = GenerateAccountNumber(),
            AccountName = request.AccountName,
            Type = Enum.Parse<AccountType>(request.Type, ignoreCase: true),
            Currency = request.Currency,
            Balance = request.InitialDeposit,
            UserId = request.UserId,
            OrganizationId = request.OrganizationId,
            BranchCode = request.BranchCode,
            IfscCode = request.IfscCode,
            Status = AccountStatus.Active
        };

        await _repository.AddAsync(account, cancellationToken);
        return account.ToResponse();
    }

    private static string GenerateAccountNumber()
    {
        var random = new Random();
        return $"{random.NextInt64(100000000000, 999999999999)}";
    }
}
