using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Finnova.Repository.Repositories;

public sealed class ReceiptRepository(FinnovaDbContext db) : IReceiptRepository
{
    public async Task AddAsync(Receipt receipt, CancellationToken ct) => await db.Receipts.AddAsync(receipt, ct);

    public Task<Receipt?> GetByClientRequestIdAsync(Guid clientRequestId, CancellationToken ct) =>
        Detail().FirstOrDefaultAsync(x => x.ClientRequestId == clientRequestId, ct);

    public Task<Receipt?> GetDetailAsync(Guid id, CancellationToken ct) =>
        Detail().FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<bool> DocNoExistsAsync(ReceiptType type, string docNo, CancellationToken ct)
    {
        var key = docNo.Trim().ToUpper();
        return db.Receipts.AsNoTracking().AnyAsync(x => x.ReceiptType == type && x.DocNo.ToUpper() == key, ct);
    }

    public async Task<(List<Receipt> Items, int Total)> SearchAsync(ReceiptSearchFilter f, CancellationToken ct)
    {
        var q = db.Receipts.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var term = f.Search.Trim();
            q = q.Where(x => x.DocNo.Contains(term) || x.CustomerCode.Contains(term));
        }
        if (f.ReceiptType is { } t) q = q.Where(x => x.ReceiptType == t);
        if (f.Mode is { } m) q = q.Where(x => x.Mode == m);
        if (f.Status is { } s) q = q.Where(x => x.Status == s);
        if (f.LineOfBusinessId is { } lob) q = q.Where(x => x.LineOfBusinessId == lob);
        if (f.BranchId is { } br) q = q.Where(x => x.BranchId == br);
        if (f.DocDateFrom is { } from) q = q.Where(x => x.DocDate >= from);
        if (f.DocDateTo is { } to) q = q.Where(x => x.DocDate <= to);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(x => x.DocDate).ThenByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id)
            .Skip((f.Page - 1) * f.PageSize).Take(f.PageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    private IQueryable<Receipt> Detail() => db.Receipts.AsNoTracking().AsSplitQuery()
        .Include(x => x.Lines).Include(x => x.AddLess).Include(x => x.Denominations)
        .Include(x => x.Cheque).Include(x => x.BankTransfer);
}

public sealed class ReceiptAuditRepository(FinnovaDbContext db) : IReceiptAuditRepository
{
    public async Task AddAsync(ReceiptAuditEntry entry, CancellationToken ct) => await db.ReceiptAuditEntries.AddAsync(entry, ct);

    public Task<List<ReceiptAuditEntry>> GetByReceiptIdAsync(Guid receiptId, CancellationToken ct) =>
        db.ReceiptAuditEntries.AsNoTracking().Where(x => x.ReceiptId == receiptId)
            .OrderByDescending(x => x.OccurredAtUtc).ThenByDescending(x => x.Id).ToListAsync(ct);
}