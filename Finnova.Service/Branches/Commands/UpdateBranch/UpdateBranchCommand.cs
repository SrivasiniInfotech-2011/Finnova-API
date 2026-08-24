using MediatR;
using Finnova.Models.Contracts.Branches;

namespace Finnova.Service.Branches.Commands.UpdateBranch;

public record UpdateBranchCommand(
    Guid Id,
    string BranchType,
    string CorporateCode,
    string StateCode,
    string BranchCode,
    string BranchName,
    string Address,
    string Landmark,
    string State,
    string Country,
    string? Pincode,
    string? Telephone,
    string? Mobile,
    bool IsActive,
    bool IsOperational
) : IRequest<BranchResponse?>;
