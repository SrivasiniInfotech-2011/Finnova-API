using Finnova.Models.Contracts.Courts;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.Court.Queries.GetCourtById;

public class GetCourtByIdQueryHandler : IRequestHandler<GetCourtByIdQuery, CourtResponse>
{
    private readonly ICourtRepository _repository;

    public GetCourtByIdQueryHandler(ICourtRepository repository) => _repository = repository;

    public async Task<CourtResponse> Handle(GetCourtByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new CourtNotFoundException(request.Id);   // R3.9
        return entity.ToResponse();
    }
}