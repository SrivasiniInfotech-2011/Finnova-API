namespace Finnova.Models.Contracts.JVPosting;

public sealed record JvPostingQueueBucket(string Status, long Count, long? OldestRecordAgeSeconds);
public sealed record JvPostingRecoveryRequest(string Reason);
public sealed record JvPostingDetailResponse(Guid RequestId, string SourceSystem, Guid CompanyId, Guid BranchId, string Status, int AttemptCount, Guid? JournalId, Guid? PostingUserId, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, string? LastErrorCategory, string? LastErrorCode);
