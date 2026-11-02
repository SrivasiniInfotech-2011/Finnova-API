using Finnova.Models.Contracts.IdentityVerification;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Interfaces;

public interface IIdentityVerificationRepository : IRepository<ValidationRecord>
{
    Task AddSessionAsync(VerificationSession session, CancellationToken ct = default);
    Task AppendRecordAsync(ValidationRecord record, CancellationToken ct = default);
    Task AppendRecordsAsync(IReadOnlyCollection<ValidationRecord> records, CancellationToken ct = default);
    Task<VerificationSession?> GetSessionAsync(Guid id, CancellationToken ct = default);
    Task<ValidationRecord?> GetRecordAsync(Guid id, CancellationToken ct = default);
    Task<(List<ValidationRecord> Items, int Total)> GetPagedAsync(VerificationRecordQuery query, CancellationToken ct = default);
    Task<List<ValidationRecord>> GetBySessionAsync(Guid id, CancellationToken ct = default);
    Task<ProtectedDocumentReference?> GetProtectedDocumentReferenceAsync(Guid id, CancellationToken ct = default);
}
