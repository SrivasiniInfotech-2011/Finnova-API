using MediatR;
using Finnova.Repository.Interfaces;

namespace Finnova.Service.Branches.Commands.SoftDeleteBranch;

public class SoftDeleteBranchCommandHandler : IRequestHandler<SoftDeleteBranchCommand, bool>
{
    private readonly IBranchRepository _repository;

    public SoftDeleteBranchCommandHandler(IBranchRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(SoftDeleteBranchCommand request, CancellationToken cancellationToken)
    {
        var branch = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (branch is null) return false;

        branch.IsActive = false;
        branch.IsOperational = false;
        branch.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(branch, cancellationToken);
        return true;
    }
}
