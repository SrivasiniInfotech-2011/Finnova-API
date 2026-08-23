using MediatR;
using Finnova.Models.Contracts.Accounts;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;

namespace Finnova.Service.Transactions.Commands.CreateTransaction;

public class CreateTransactionCommandHandler : IRequestHandler<CreateTransactionCommand, TransactionResponse>
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IAccountRepository _accountRepository;

    public CreateTransactionCommandHandler(
        ITransactionRepository transactionRepository,
        IAccountRepository accountRepository)
    {
        _transactionRepository = transactionRepository;
        _accountRepository = accountRepository;
    }

    public async Task<TransactionResponse> Handle(CreateTransactionCommand request, CancellationToken cancellationToken)
    {
        var account = await _accountRepository.GetByIdAsync(request.AccountId, cancellationToken)
            ?? throw new InvalidOperationException($"Account {request.AccountId} not found.");

        var type = Enum.Parse<TransactionType>(request.Type, ignoreCase: true);

        var balanceChange = type switch
        {
            TransactionType.Deposit or TransactionType.Interest => request.Amount,
            TransactionType.Withdrawal or TransactionType.Fee => -request.Amount,
            TransactionType.Transfer when request.CounterpartyAccountId is not null => -request.Amount,
            TransactionType.Reversal => request.Amount,
            _ => 0m
        };

        account.Balance += balanceChange;
        account.UpdatedAt = DateTime.UtcNow;

        var transaction = new Transaction
        {
            AccountId = request.AccountId,
            Type = type,
            Amount = request.Amount,
            BalanceAfter = account.Balance,
            Description = request.Description,
            ReferenceNumber = $"TXN{DateTime.UtcNow:yyyyMMddHHmmss}{new Random().Next(1000, 9999)}",
            Status = TransactionStatus.Completed,
            CounterpartyAccountId = request.CounterpartyAccountId
        };

        await _accountRepository.UpdateAsync(account, cancellationToken);
        await _transactionRepository.AddAsync(transaction, cancellationToken);

        return transaction.ToResponse();
    }
}
