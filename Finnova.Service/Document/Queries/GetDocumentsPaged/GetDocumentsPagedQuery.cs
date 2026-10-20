using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.Documents;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;
using FluentValidation;
using MediatR;

namespace Finnova.Service.Document.Queries.GetDocumentsPaged;

public record GetDocumentsPagedQuery(
    string? Search, string? Category, string? Status, int Page = 1, int PageSize = 20)
    : IRequest<PaginatedResponse<DocumentResponse>>;

public class GetDocumentsPagedQueryHandler
    : IRequestHandler<GetDocumentsPagedQuery, PaginatedResponse<DocumentResponse>>
{
    private readonly IDocumentRepository _repository;
    public GetDocumentsPagedQueryHandler(IDocumentRepository repository) => _repository = repository;

    public async Task<PaginatedResponse<DocumentResponse>> Handle(
        GetDocumentsPagedQuery request, CancellationToken ct)
    {
        DocumentStatus? status = null;
        if (!string.IsNullOrWhiteSpace(request.Status)
            && Enum.TryParse<DocumentStatus>(request.Status, ignoreCase: true, out var parsed))
            status = parsed;

        var (items, total) = await _repository.GetPagedAsync(
            request.Search, request.Category, status, request.Page, request.PageSize, ct);

        var totalPages = (int)Math.Ceiling(total / (double)request.PageSize);
        return new PaginatedResponse<DocumentResponse>(
            items.ToResponseList(), total, request.Page, request.PageSize, totalPages);  // R10.3/10.9
    }
}

public class GetDocumentsPagedQueryValidator : AbstractValidator<GetDocumentsPagedQuery>
{
    public GetDocumentsPagedQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1)
            .WithMessage("Page number is out of range.");                       // R10.6
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100)
            .WithMessage("Page size is out of range.");                         // R10.7
    }
}