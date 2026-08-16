using MediatR;
using Finnova.Models.Contracts.Accounts;

namespace Finnova.Service.Accounts.Commands.CreateAccount;

public record CreateAccountCommand(
    string AccountName,
    string Type,
    string Currency,
    Guid? UserId,
    Guid? OrganizationId,
    string? BranchCode,
    string? IfscCode,
    decimal InitialDeposit
) : IRequest<AccountResponse>;
