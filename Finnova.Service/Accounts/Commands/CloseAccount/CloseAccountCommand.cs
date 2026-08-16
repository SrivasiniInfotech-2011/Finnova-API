using MediatR;
using Finnova.Models.Contracts.Accounts;

namespace Finnova.Service.Accounts.Commands.CloseAccount;

public record CloseAccountCommand(Guid AccountId) : IRequest<AccountResponse?>;
