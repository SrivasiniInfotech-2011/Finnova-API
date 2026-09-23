using MediatR;
using FluentValidation;
using Finnova.Models.Contracts.Lookups;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;

namespace Finnova.Service.Lookup.Queries.GetLookupDropdownItems;

public class GetLookupDropdownItemsQueryHandler
    : IRequestHandler<GetLookupDropdownItemsQuery, List<LookupDropdownItemResponse>>
{
    private readonly ILookupRepository _repository;

    public GetLookupDropdownItemsQueryHandler(ILookupRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<LookupDropdownItemResponse>> Handle(
        GetLookupDropdownItemsQuery request, CancellationToken cancellationToken)
    {
        // R6.5: unknown scope is an error, distinct from "defined but empty" (R6.6).
        if (!await _repository.ScopeExistsAsync(request.Module, request.LookupType, cancellationToken))
            throw new ValidationException("Unknown Module or Lookup Type.");

        var values = await _repository.GetActiveByModuleAndTypeAsync(
            request.Module, request.LookupType, cancellationToken);

        return values.Select(v => v.ToDropdownItem()).ToList(); // empty list allowed (R6.6)
    }
}
