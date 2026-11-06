using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Interfaces;

public sealed record ApplicationFilters(string? Search, Guid? LineOfBusinessId, Guid? CustomerId,
    string? ProductCode, Guid? BranchId, string? SchemeCode, int Page, int PageSize);
public interface IApplicationRepository : IRepository<Application>
{
    Task<(List<Application> Items, int Total)> SearchAsync(ApplicationFilters filters, CancellationToken ct);
    Task<Application?> GetTrackedAsync(Guid id, CancellationToken ct);
}
public interface IApplicationScheduleRepository
{
    Task<ApplicationSchedule?> GetByApplicationIdAsync(Guid applicationId, CancellationToken ct);
    Task AddAsync(ApplicationSchedule schedule, CancellationToken ct);
}
public interface IApplicationStatusAuditRepository
{
    Task AddAsync(ApplicationStatusAuditEntry entry, CancellationToken ct);
}
public interface IApplicationNumberGenerator
{
    string Next();
}
