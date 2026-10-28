using Finnova.Models.Contracts.Customers;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;

namespace Finnova.Service.Customers.References;

public interface ILookupReferenceResolver
{
    Task<LookupReferenceProjection> ResolveActiveAsync(string module, string lookupType, string code, string fieldName, CancellationToken ct = default);
    Task<IReadOnlyCollection<LookupReferenceProjection>> GetActiveAsync(string module, string lookupType, CancellationToken ct = default);
}

public sealed class LookupReferenceResolver(ILookupRepository repository) : ILookupReferenceResolver
{
    public async Task<LookupReferenceProjection> ResolveActiveAsync(string module, string lookupType, string code, string fieldName, CancellationToken ct = default)
    {
        var values = await repository.GetActiveByModuleAndTypeAsync(module, lookupType, ct);
        var value = values.FirstOrDefault(x => string.Equals(x.Code.Trim(), code.Trim(), StringComparison.OrdinalIgnoreCase));
        return value is null
            ? throw new CustomerLookupReferenceNotFoundException(fieldName, module, lookupType, code)
            : new(module, lookupType, value.Code, value.Value);
    }

    public async Task<IReadOnlyCollection<LookupReferenceProjection>> GetActiveAsync(string module, string lookupType, CancellationToken ct = default)
        => (await repository.GetActiveByModuleAndTypeAsync(module, lookupType, ct)).Select(x => new LookupReferenceProjection(module, lookupType, x.Code, x.Value)).ToArray();
}
