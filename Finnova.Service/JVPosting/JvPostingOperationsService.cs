using Finnova.Models.Contracts.JVPosting;
using Finnova.Repository.Context;
using Microsoft.EntityFrameworkCore;

namespace Finnova.Service.JVPosting;

public sealed class JvPostingOperationsService(FinnovaDbContext db)
{
    public async Task<IReadOnlyList<JvPostingQueueBucket>> QueueStatusAsync(Guid? companyId, Guid? branchId, CancellationToken ct)
    {
        var query = db.JvPostingInboxes.AsNoTracking().AsQueryable();
        if (companyId.HasValue) query = query.Where(x => x.CompanyId == companyId.Value);
        if (branchId.HasValue) query = query.Where(x => x.BranchId == branchId.Value);
        var rows = await query.GroupBy(x => x.Status).Select(g => new JvPostingQueueBucket(g.Key.ToString(), g.LongCount(), g.LongCount() == 0 ? null : (long?)EF.Functions.DateDiffSecond(g.Min(x => x.CreatedAtUtc), DateTime.UtcNow))).ToListAsync(ct);
        return rows;
    }

    public async Task<JvPostingDetailResponse?> DetailAsync(Guid requestId, CancellationToken ct)
    {
        return await db.JvPostingInboxes.AsNoTracking().Where(x => x.RequestId == requestId).Select(x => new JvPostingDetailResponse(x.RequestId, x.SourceSystem, x.CompanyId, x.BranchId, x.Status.ToString(), x.AttemptCount, x.JournalId, x.PostingUserId, x.CreatedAtUtc, x.UpdatedAtUtc, x.LastErrorCategory, x.LastErrorCode)).SingleOrDefaultAsync(ct);
    }
}
