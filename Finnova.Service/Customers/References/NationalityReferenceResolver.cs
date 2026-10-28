using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;

namespace Finnova.Service.Customers.References;

public interface INationalityReferenceResolver
{
    Task<NationalityReferenceProjection> ResolveActiveAsync(string code, CancellationToken ct = default);
    Task<IReadOnlyCollection<NationalityReferenceProjection>> GetActiveAsync(CancellationToken ct = default);
}

public sealed class NationalityReferenceResolver(INationalityRepository repository) : INationalityReferenceResolver
{
    public async Task<NationalityReferenceProjection> ResolveActiveAsync(string code, CancellationToken ct = default)
    {
        var row = await repository.GetActiveByCodeAsync(code, ct);
        return row is null ? throw new CustomerNationalityReferenceNotFoundException(code) : new(row.Code, row.Name);
    }

    public async Task<IReadOnlyCollection<NationalityReferenceProjection>> GetActiveAsync(CancellationToken ct = default)
        => (await repository.GetAllAsync(ct)).Where(x => x.IsActive).Select(x => new NationalityReferenceProjection(x.Code, x.Name)).ToArray();
}
