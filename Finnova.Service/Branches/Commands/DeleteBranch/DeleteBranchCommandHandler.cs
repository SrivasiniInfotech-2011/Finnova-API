using MediatR;
using Finnova.Repository.Interfaces;

namespace Finnova.Service.Branches.Commands.DeleteBranch;

public class DeleteBranchCommandHandler : IRequestHandler<DeleteBranchCommand, bool>
{
    private readonly IBranchRepository _repository;

    public DeleteBranchCommandHandler(IBranchRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(DeleteBranchCommand request, CancellationToken cancellationToken)
    {
        var branch = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (branch is null) return false;

        await _repository.DeleteAsync(branch, cancellationToken);
        return true;
    }
}
