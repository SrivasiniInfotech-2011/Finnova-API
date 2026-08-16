using MediatR;
using Finnova.Models.Contracts.Accounts;

namespace Finnova.Service.Accounts.Queries.GetAccountById;

public record GetAccountByIdQuery(Guid Id) : IRequest<AccountResponse?>;
