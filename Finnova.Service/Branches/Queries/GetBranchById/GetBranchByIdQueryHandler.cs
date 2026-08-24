using MediatR;
using Finnova.Models.Contracts.Branches;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;

namespace Finnova.Service.Branches.Queries.GetBranchById;

public class GetBranchByIdQueryHandler : IRequestHandler<GetBranchByIdQuery, BranchResponse?>
{
    private readonly IBranchRepository _repository;

    public GetBranchByIdQueryHandler(IBranchRepository repository)
    {
        _repository = repository;
    }

    public async Task<BranchResponse?> Handle(GetBranchByIdQuery request, CancellationToken cancellationToken)
    {
        var branch = await _repository.GetByIdAsync(request.Id, cancellationToken);
        return branch?.ToResponse();
    }
}
