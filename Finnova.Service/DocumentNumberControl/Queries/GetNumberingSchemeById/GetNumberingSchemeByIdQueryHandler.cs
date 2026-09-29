using Finnova.Models.Contracts.DocumentNumberControl;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.DocumentNumberControl.Queries.GetNumberingSchemeById;

public class GetNumberingSchemeByIdQueryHandler
    : IRequestHandler<GetNumberingSchemeByIdQuery, NumberingSchemeResponse>
{
    private readonly INumberingSchemeRepository _repository;

    public GetNumberingSchemeByIdQueryHandler(INumberingSchemeRepository repository) => _repository = repository;

    public async Task<NumberingSchemeResponse> Handle(
        GetNumberingSchemeByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NumberingSchemeNotFoundException(request.Id);   // R4.9
        return entity.ToResponse();
    }
}
