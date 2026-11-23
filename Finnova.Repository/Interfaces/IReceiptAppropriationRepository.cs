using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Interfaces;

public interface IReceiptAppropriationRepository
{
    Task<ReceiptAppropriation?> FindByReceiptIdAsync(Guid receiptId, CancellationToken ct);
    Task<ReceiptAppropriation?> FindByClientRequestIdAsync(Guid clientRequestId, CancellationToken ct);
    Task AddAsync(ReceiptAppropriation entity, CancellationToken ct);
    Task AddAuditAsync(AppropriationAuditEntry entry, CancellationToken ct);
}