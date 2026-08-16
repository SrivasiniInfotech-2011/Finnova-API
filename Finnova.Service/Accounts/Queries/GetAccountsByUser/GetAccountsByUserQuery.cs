using MediatR;
using Finnova.Models.Contracts.Accounts;

namespace Finnova.Service.Accounts.Queries.GetAccountsByUser;

public record GetAccountsByUserQuery(Guid UserId) : IRequest<List<AccountResponse>>;
