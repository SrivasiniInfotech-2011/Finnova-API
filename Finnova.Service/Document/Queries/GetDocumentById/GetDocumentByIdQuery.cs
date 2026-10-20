using Finnova.Models.Contracts.Documents;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.Document.Queries.GetDocumentById;

public record GetDocumentByIdQuery(Guid Id) : IRequest<DocumentResponse>;

public class GetDocumentByIdQueryHandler : IRequestHandler<GetDocumentByIdQuery, DocumentResponse>
{
    private readonly IDocumentRepository _repository;
    public GetDocumentByIdQueryHandler(IDocumentRepository repository) => _repository = repository;

    public async Task<DocumentResponse> Handle(GetDocumentByIdQuery request, CancellationToken ct)
    {
        var entity = await _repository.GetByIdWithRulesAsync(request.Id, ct)
            ?? throw new DocumentNotFoundException();
        return entity.ToResponse();   // includes Inactive status (R6.3)
    }
}