using MediatR;
using Finnova.Models.Contracts.Branches;

namespace Finnova.Service.Branches.Queries.GetBranchById;

public record GetBranchByIdQuery(Guid Id) : IRequest<BranchResponse?>;
