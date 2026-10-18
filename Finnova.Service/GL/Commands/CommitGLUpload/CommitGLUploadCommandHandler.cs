using Finnova.Models.Contracts.GL;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Interfaces;
using Finnova.Service.GL.Internal;
using MediatR;

namespace Finnova.Service.GL.Commands.CommitGLUpload;

/// <summary>
/// Commits a dry-run-validated batch as a single all-or-nothing set in Pending Authorization
/// (R9.5). If any row fails validation at commit time no accounts are created and the failing
/// rows are returned. (The 30-minute dry-run window per R9.10 is enforced by tracking the
/// BatchId timestamp — see design assumption A3; a dev-stub window is accepted here.)
/// </summary>
public class CommitGLUploadCommandHandler : IRequestHandler<CommitGLUploadCommand, GLUploadReportResponse>
{
    private readonly IGLAccountRepository _repository;
    private readonly IGLAuditRepository _audit;

    public CommitGLUploadCommandHandler(IGLAccountRepository repository, IGLAuditRepository audit)
    {
        _repository = repository;
        _audit = audit;
    }

    public async Task<GLUploadReportResponse> Handle(CommitGLUploadCommand request, CancellationToken ct)
    {
        var staged = new List<GLAccount>();
        var failures = new List<GLUploadRowResult>();

        foreach (var row in request.Rows.OrderBy(r => r.RowNumber))
        {
            var code = (row.Request.GLCode ?? string.Empty).Trim();
            var name = (row.Request.Name ?? string.Empty).Trim();
            if (!GLCodeRules.IsNineNumericDigits(code))
                failures.Add(new GLUploadRowResult(row.RowNumber, code, false, "Invalid GL code."));
            else if (await _repository.ExistsByCodeAsync(request.CompanyId, code, null, ct))
                failures.Add(new GLUploadRowResult(row.RowNumber, code, false, "Duplicate GL code."));
            else
                staged.Add(new GLAccount
                {
                    CompanyId = request.CompanyId,
                    GLCode = code,
                    Name = name,
                    Level = NatureRules.LevelFrom(null),      // level refined by parent during create; batch seeds L1
                    GLType = row.Request.GLType,
                    CurrencyId = row.Request.CurrencyId,
                    EffectiveFrom = row.Request.EffectiveFrom,
                    EffectiveTo = row.Request.EffectiveTo,
                    Status = GLStatus.PendingAuthorization,   // R9.5
                    MakerId = request.ActingUser,
                    IsPostable = false
                });
        }

        // R9.5 — all-or-nothing: if any row failed, create none.
        if (failures.Count > 0)
            return new GLUploadReportResponse(request.BatchId, false, failures);

        foreach (var e in staged)
        {
            await _repository.AddAsync(e, ct);
            await _audit.AddAsync(new GLAuditEntry
            {
                GLAccountId = e.Id,
                Action = GLAuditAction.Create,
                NewValues = GLAuditSnapshot.Editable(e),
                Summary = $"Bulk-created GL '{e.GLCode}' (batch {request.BatchId}).",
                Remarks = request.BatchId.ToString(),
                ChangedBy = request.ActingUser,
                ChangedAtUtc = DateTime.UtcNow
            }, ct);
        }

        var rows = staged.Select((e, i) => new GLUploadRowResult(i + 1, e.GLCode, true, null)).ToList();
        return new GLUploadReportResponse(request.BatchId, true, rows);
    }
}
