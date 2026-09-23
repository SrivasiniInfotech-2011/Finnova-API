using MediatR;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;

namespace Finnova.Service.Lookup.Commands.DeleteLookupValue;

public class DeleteLookupValueCommandHandler : IRequestHandler<DeleteLookupValueCommand, bool>
{
    private readonly ILookupRepository _repository;

    public DeleteLookupValueCommandHandler(ILookupRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(DeleteLookupValueCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new LookupNotFoundException(request.Id); // R5.4

        if (entity.IsSystemLocked)
            throw new LookupLockedException(); // R3.1 -> ERR-LKP-005

        // R5.6 in-use guard hook. No foreign-key references to lookup values exist yet
        // (design A6), so this is a hook pending real consuming-module references:
        // if (await _referenceChecker.IsReferencedAsync(entity, cancellationToken)) throw new LookupInUseException();

        await _repository.DeleteAsync(entity, cancellationToken); // R5.2 hard delete
        return true;
    }
}
