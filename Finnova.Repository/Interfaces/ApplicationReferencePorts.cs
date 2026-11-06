using Finnova.Models.Contracts.Applications;
using Finnova.Models.Contracts.Lookups;
using Finnova.Models.Contracts.UserManagement;

namespace Finnova.Repository.Interfaces;

public interface ILineOfBusinessReferencePort
{
    Task<List<LineOfBusinessRefResponse>> GetActiveAsync(CancellationToken ct);
    Task<bool> ExistsActiveAsync(Guid id, CancellationToken ct);
}

public interface ICustomerReferencePort
{
    Task<(List<CustomerReferenceOptionResponse> Items, int Total)> SearchActiveAsync(string? search, int page, int pageSize, CancellationToken ct);
    Task<bool> ExistsActiveAsync(Guid id, CancellationToken ct);
}

public interface IBranchReferencePort
{
    Task<List<ApplicationReferenceOptionResponse>> GetActiveAsync(Guid? lineOfBusinessId, CancellationToken ct);
    Task<bool> ExistsActiveAsync(Guid id, CancellationToken ct);
}

public interface ILookupReferencePort
{
    Task<List<LookupDropdownItemResponse>> GetActiveAsync(string module, string type, CancellationToken ct);
    Task<bool> ExistsActiveAsync(string module, string type, string code, CancellationToken ct);
}
