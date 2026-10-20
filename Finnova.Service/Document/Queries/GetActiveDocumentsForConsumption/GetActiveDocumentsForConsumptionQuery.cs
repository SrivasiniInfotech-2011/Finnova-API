using Finnova.Models.Contracts.Documents;
using Finnova.Repository.Interfaces;
using Finnova.Service.Document.Ports;
using Finnova.Service.Mappers;
using FluentValidation;
using MediatR;

namespace Finnova.Service.Document.Queries.GetActiveDocumentsForConsumption;

public record GetActiveDocumentsForConsumptionQuery(string Constitution, string EntityRole)
    : IRequest<List<ActiveDocumentForConsumptionResponse>>;

public class GetActiveDocumentsForConsumptionQueryHandler
    : IRequestHandler<GetActiveDocumentsForConsumptionQuery, List<ActiveDocumentForConsumptionResponse>>
{
    private readonly IDocumentRepository _repository;
    private readonly ILookupValidationPort _lookups;

    public GetActiveDocumentsForConsumptionQueryHandler(
        IDocumentRepository repository, ILookupValidationPort lookups)
    { _repository = repository; _lookups = lookups; }

    public async Task<List<ActiveDocumentForConsumptionResponse>> Handle(
        GetActiveDocumentsForConsumptionQuery request, CancellationToken ct)
    {
        // R11.6 — unknown Constitution / Entity Role is a validation error, not an empty set.
        if (!await _lookups.IsActiveLookupAsync("CONSTITUTION", request.Constitution, ct))
            throw new ValidationException($"Unknown Constitution '{request.Constitution}'.");
        if (!await _lookups.IsActiveLookupAsync("APPLICABLE_ENTITY", request.EntityRole, ct))
            throw new ValidationException($"Unknown Entity Role '{request.EntityRole}'.");

        // R11.1/R11.3 — Active only, applicability matrix must include the pair; live values only (R11.4).
        var docs = await _repository.GetActiveForConsumptionAsync(
            request.Constitution, request.EntityRole, ct);

        return docs.Select(d =>
        {
            var rule = d.ApplicabilityRules.First(r =>
                r.Constitution == request.Constitution && r.EntityRole == request.EntityRole);
            return d.ToConsumptionResponse(rule.IsMandatory);   // R11.2 resolved mandatory
        }).ToList();   // empty set allowed (R11.5)
    }
}