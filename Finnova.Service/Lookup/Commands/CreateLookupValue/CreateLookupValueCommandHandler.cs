using MediatR;
using FluentValidation;
using Finnova.Models.Contracts.Lookups;
using Finnova.Models.Domain.Entities;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;

namespace Finnova.Service.Lookup.Commands.CreateLookupValue;

public class CreateLookupValueCommandHandler : IRequestHandler<CreateLookupValueCommand, LookupValueResponse>
{
    private readonly ILookupRepository _repository;

    public CreateLookupValueCommandHandler(ILookupRepository repository)
    {
        _repository = repository;
    }

    public async Task<LookupValueResponse> Handle(CreateLookupValueCommand request, CancellationToken cancellationToken)
    {
        // R2.9 unknown reference: the Module + Lookup Type scope must be defined.
        if (!await _repository.ScopeExistsAsync(request.Module, request.LookupType, cancellationToken))
            throw new ValidationException("Unknown Module or Lookup Type reference.");

        // R2.8 duplicate code within the Module + Lookup Type scope.
        if (await _repository.ExistsByCodeAsync(request.Module, request.LookupType, request.Code, null, cancellationToken))
            throw new ValidationException("A lookup value with this Code already exists for the Module and Lookup Type.");

        var entity = new LookupValue
        {
            Module = request.Module,
            LookupType = request.LookupType,
            Code = request.Code,
            Value = request.Value,
            DisplayOrder = request.DisplayOrder,
            IsActive = request.IsActive ?? true,   // R2.4 default true
        };

        await _repository.AddAsync(entity, cancellationToken);   // R2.1 persisted, R2.6 immediately queryable
        return entity.ToResponse();
    }
}
