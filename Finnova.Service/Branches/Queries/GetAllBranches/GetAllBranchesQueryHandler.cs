using MediatR;
using Finnova.Models.Contracts.Branches;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;

namespace Finnova.Service.Branches.Queries.GetAllBranches;

public class GetAllBranchesQueryHandler : IRequestHandler<GetAllBranchesQuery, List<BranchResponse>>
{
    private readonly IBranchRepository _repository;

    public GetAllBranchesQueryHandler(IBranchRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<BranchResponse>> Handle(GetAllBranchesQuery request, CancellationToken cancellationToken)
    {
        var branches = await _repository.GetAllAsync(cancellationToken);
        return branches.ToResponseList();
    }
}
