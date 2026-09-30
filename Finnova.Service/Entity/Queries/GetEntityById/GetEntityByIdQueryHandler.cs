using Finnova.Models.Contracts.Entities;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.Entity.Queries.GetEntityById;

public class GetEntityByIdQueryHandler : IRequestHandler<GetEntityByIdQuery, EntityResponse>
{
    private readonly IEntityRepository _repository;

    public GetEntityByIdQueryHandler(IEntityRepository repository) => _repository = repository;

    public async Task<EntityResponse> Handle(GetEntityByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new EntityNotFoundException(request.Id);   // R3.12
        return entity.ToResponse();
    }
}