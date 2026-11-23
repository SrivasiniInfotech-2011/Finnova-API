using Finnova.Models.Domain.Entities;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Finnova.Repository.Repositories;

public sealed class ReceiptAppropriationRepository(FinnovaDbContext db) : IReceiptAppropriationRepository
{
    public Task<ReceiptAppropriation?> FindByReceiptIdAsync(Guid receiptId, CancellationToken ct) => db.ReceiptAppropriations.AsNoTracking().Include(x => x.Allocations).Include(x => x.AdvanceAccountAllocation).SingleOrDefaultAsync(x => x.ReceiptId == receiptId, ct);
    public Task<ReceiptAppropriation?> FindByClientRequestIdAsync(Guid clientRequestId, CancellationToken ct) => db.ReceiptAppropriations.AsNoTracking().Include(x => x.Allocations).Include(x => x.AdvanceAccountAllocation).SingleOrDefaultAsync(x => x.ClientRequestId == clientRequestId, ct);
    public Task AddAsync(ReceiptAppropriation entity, CancellationToken ct) => db.ReceiptAppropriations.AddAsync(entity, ct).AsTask();
    public Task AddAuditAsync(AppropriationAuditEntry entry, CancellationToken ct) => db.AppropriationAuditEntries.AddAsync(entry, ct).AsTask();
}