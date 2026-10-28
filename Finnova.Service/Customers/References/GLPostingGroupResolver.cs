using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;

namespace Finnova.Service.Customers.References;

public interface IGLPostingGroupResolver
{
    Task<GLPostingGroupProjection> ResolveAsync(Guid companyId, string code, Guid? glAccountId, CancellationToken ct = default);
    Task<IReadOnlyCollection<GLPostingGroupProjection>> GetActiveAsync(Guid companyId, CancellationToken ct = default);
}

public sealed class GLPostingGroupResolver(IGLAccountRepository repository) : IGLPostingGroupResolver
{
    public async Task<GLPostingGroupProjection> ResolveAsync(Guid companyId, string code, Guid? glAccountId, CancellationToken ct = default)
    {
        var row = await repository.GetActiveApprovedPostableByCompanyAndCodeAsync(companyId, code, ct);
        if (row is not null && glAccountId.HasValue && row.Id != glAccountId.Value) row = null;
        return row is null ? throw new CustomerGLPostingGroupInvalidException(companyId, code, "Account is not active, approved, postable, or Company-scoped.") : new(row.Id, row.CompanyId, row.GLCode, row.Name);
    }

    public async Task<IReadOnlyCollection<GLPostingGroupProjection>> GetActiveAsync(Guid companyId, CancellationToken ct = default)
        => (await repository.GetAllForCompanyAsync(companyId)).Where(x => x.Status == GLStatus.Active && x.AuthorizedVersionNumber > 0 && x.IsPostable).Select(x => new GLPostingGroupProjection(x.Id, x.CompanyId, x.GLCode, x.Name)).ToArray();
}

// If the existing GL contract provides a narrower approved/postable query, add that method to IGLAccountRepository and use it here; do not infer validity from Name or another Company.
