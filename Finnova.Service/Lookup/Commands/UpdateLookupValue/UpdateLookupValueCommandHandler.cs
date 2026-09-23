using MediatR;
using FluentValidation;
using Finnova.Models.Contracts.Lookups;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;

namespace Finnova.Service.Lookup.Commands.UpdateLookupValue;

public class UpdateLookupValueCommandHandler
    : IRequestHandler<UpdateLookupValueCommand, LookupValueResponse>
{
    private readonly ILookupRepository _repository;

    public UpdateLookupValueCommandHandler(ILookupRepository repository)
    {
        _repository = repository;
    }

    public async Task<LookupValueResponse> Handle(UpdateLookupValueCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new LookupNotFoundException(request.Id);           // R5.4

        var isRename = !string.Equals(entity.Code, request.Code, StringComparison.Ordinal);

        // R3.2 + R3.4: system-locked rows reject rename (alone or combined with other changes).
        if (entity.IsSystemLocked && isRename)
            throw new LookupLockedException();                          // ERR-LKP-005, nothing persisted

        // R5.3: uniqueness within scope on rename (exclude self).
        if (isRename && await _repository.ExistsByCodeAsync(
                entity.Module, entity.LookupType, request.Code, entity.Id, cancellationToken))
            throw new ValidationException("Duplicate Lookup Code within Module and Lookup Type.");

        // R3.3 / R5.1: editable fields applied (locked rows still allow non-identity edits).
        entity.Code = request.Code;
        entity.Value = request.Value;
        entity.DisplayOrder = request.DisplayOrder;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(entity, cancellationToken);
        return entity.ToResponse();
    }
}
