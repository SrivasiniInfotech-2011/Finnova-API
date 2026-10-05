using MediatR;
using Finnova.Models.Contracts.DraweeBanks;

namespace Finnova.Service.DraweeBank.Commands.CreateDraweeBank;

public record CreateDraweeBankCommand(
    string BankCode,
    string BankName,
    bool? IsActive,
    IReadOnlyList<DraweeBranchRequest> Branches,
    RestrictionDetailRequest? Restriction,
    string ActingAdmin
) : IRequest<DraweeBankResponse>;
