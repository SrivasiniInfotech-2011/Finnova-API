using MediatR;
using Finnova.Models.Contracts.Branches;

namespace Finnova.Service.Branches.Queries.GetAllBranches;

public record GetAllBranchesQuery : IRequest<List<BranchResponse>>;
