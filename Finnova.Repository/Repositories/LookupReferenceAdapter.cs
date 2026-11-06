using Finnova.Models.Contracts.Lookups;
using Finnova.Repository.Interfaces;

namespace Finnova.Repository.Repositories;

public sealed class LookupReferenceAdapter : ILookupReferencePort
{
    private readonly ILookupRepository _lookup;
    public LookupReferenceAdapter(ILookupRepository lookup) => _lookup = lookup;
    public async Task<List<LookupDropdownItemResponse>> GetActiveAsync(string module, string type, CancellationToken ct) =>
        (await _lookup.GetActiveByModuleAndTypeAsync(module, type, ct))
        .Select(x => new LookupDropdownItemResponse(x.Code, x.Value)).ToList();
    public Task<bool> ExistsActiveAsync(string module, string type, string code, CancellationToken ct) =>
        _lookup.GetByModuleTypeCodeAsync(module, type, code, ct)
            .ContinueWith(t => t.Result is { IsActive: true }, ct);
}
