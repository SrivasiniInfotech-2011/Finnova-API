using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Finnova.Models.Contracts.JVPosting;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Context;
using Microsoft.EntityFrameworkCore;

namespace Finnova.Service.JVPosting;

public sealed class JvPostingSubmissionService(FinnovaDbContext db)
{
    public async Task<JvPostingSubmissionResponse> SubmitAsync(
        JvPostingSubmissionRequest request,
        ClaimsPrincipal caller,
        CancellationToken cancellationToken = default)
    {
        Validate(request, caller);
        var rawPayload = JsonSerializer.SerializeToUtf8Bytes(request.Data);
        var payloadHash = SHA256.HashData(Encoding.UTF8.GetBytes($"{request.RequestId}|{request.CompanyId}|{request.BranchId}|{request.SourceSystem}|{Convert.ToBase64String(rawPayload)}"));
        var existing = await db.JvPostingInboxes.SingleOrDefaultAsync(x => x.RequestId == request.RequestId, cancellationToken);
        if (existing is not null)
        {
            if (!existing.PayloadHash.SequenceEqual(payloadHash) || existing.CompanyId != request.CompanyId || existing.BranchId != request.BranchId)
                throw new InvalidOperationException("JVE-IDEMPOTENCY-PAYLOAD-CONFLICT");
            return new(request.RequestId, existing.Status.ToString(), true);
        }

        var person = string.Equals(request.InitiationMode, "Person", StringComparison.OrdinalIgnoreCase);
        db.JvPostingInboxes.Add(new JvPostingInbox
        {
            RequestId = request.RequestId,
            SourceSystem = request.SourceSystem,
            EventType = request.EventType,
            RuleKey = request.RuleKey,
            SchemaVersion = "1.0",
            CompanyId = request.CompanyId,
            BranchId = request.BranchId,
            ValueDate = request.ValueDate,
            PostingDate = request.PostingDate,
            CurrencyCode = request.CurrencyCode?.ToUpperInvariant(),
            Description = request.Description,
            RawPayload = rawPayload,
            PayloadHash = payloadHash,
            InitiationMode = person ? "Person" : "Automation",
            InitiatingUserId = person ? request.InitiatingUserId : null,
            AutomationContext = person ? null : request.AutomationContext,
            Status = JvPostingStatus.Ready,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        return new(request.RequestId, JvPostingStatus.Ready.ToString(), false);
    }

    private static void Validate(JvPostingSubmissionRequest request, ClaimsPrincipal caller)
    {
        if (request.RequestId == Guid.Empty || string.IsNullOrWhiteSpace(request.SourceSystem) || request.CompanyId == Guid.Empty || request.BranchId == Guid.Empty || (request.EventType is null && request.RuleKey is null))
            throw new ArgumentException("JVE-SUBMISSION-REQUIRED-FIELD");
        var person = string.Equals(request.InitiationMode, "Person", StringComparison.OrdinalIgnoreCase);
        var automation = string.Equals(request.InitiationMode, "Automation", StringComparison.OrdinalIgnoreCase);
        if (!person && !automation) throw new ArgumentException("JVE-ACTOR-INITIATION-MODE-INVALID");
        if (person && request.InitiatingUserId is null) throw new ArgumentException("JVE-ACTOR-INITIATING-USER-REQUIRED");
        if (automation && string.IsNullOrWhiteSpace(request.AutomationContext)) throw new ArgumentException("JVE-ACTOR-AUTOMATION-CONTEXT-REQUIRED");
        if (person && request.InitiatingUserId is not null && request.InitiatingUserId != Guid.Parse(caller.FindFirstValue(ClaimTypes.NameIdentifier) ?? Guid.Empty.ToString()))
            throw new UnauthorizedAccessException("JVE-ACTOR-INITIATING-USER-MISMATCH");
    }
}
