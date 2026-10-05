using MediatR;
using Finnova.Models.Contracts.DraweeBanks;

namespace Finnova.Service.DraweeBank.Commands.UpdateDraweeBank;

/// <summary>Updates a drawee bank's Name, branch set, and restriction (BankCode immutable).
/// Branches/restriction are the full desired set; omitted branches are removed (R3.7).</summary>
public record UpdateDraweeBankCommand(
    Guid Id,
    string BankName,
    IReadOnlyList<DraweeBranchRequest> Branches,
    RestrictionDetailRequest? Restriction,
    string ActingAdmin
) : IRequest<DraweeBankResponse>;
