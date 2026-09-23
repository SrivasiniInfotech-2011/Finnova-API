using MediatR;
using Finnova.Repository.Interfaces;

namespace Finnova.Service.Locations.Commands.DeleteLocation;

public class DeleteLocationCommandHandler : IRequestHandler<DeleteLocationCommand, bool>
{
    private readonly ILocationRepository _repository;

    public DeleteLocationCommandHandler(ILocationRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(DeleteLocationCommand request, CancellationToken cancellationToken)
    {
        var location = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (location is null) return false;

        // Cascade-delete children first (FK is Restrict), depth-first.
        await DeleteRecursiveAsync(request.Id, cancellationToken);
        return true;
    }

    private async Task DeleteRecursiveAsync(Guid id, CancellationToken cancellationToken)
    {
        var children = await _repository.GetChildrenAsync(id, cancellationToken);
        foreach (var child in children)
        {
            await DeleteRecursiveAsync(child.Id, cancellationToken);
        }

        var entity = await _repository.GetByIdAsync(id, cancellationToken);
        if (entity is not null)
        {
            await _repository.DeleteAsync(entity, cancellationToken);
        }
    }
}
