using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;

namespace Finnova.Service.LoanAccounts.Rules;

public enum SubmitCheck { Ok, NotFlagged, PendingExists, ReasonInvalid }
public enum DecisionCheck { Ok, NotPending, SameUser, CommentRequired }

/// <summary>Pure maker-checker rules shared by the handlers and the property tests (Properties 13, 18, 19).</summary>
public static class KycResolutionRules
{
    public const int ReasonMin = 10;
    public const int ReasonMax = 500;

    public static SubmitCheck CheckSubmit(bool flagged, bool hasPending, string? reason)
    {
        var r = reason?.Trim() ?? string.Empty;
        if (!flagged) return SubmitCheck.NotFlagged;
        if (hasPending) return SubmitCheck.PendingExists;
        if (r.Length < ReasonMin || r.Length > ReasonMax) return SubmitCheck.ReasonInvalid;
        return SubmitCheck.Ok;
    }

    public static DecisionCheck CheckDecision(KycResolutionStatus status, Guid makerId, Guid checkerId, string? comment)
    {
        if (status != KycResolutionStatus.Pending) return DecisionCheck.NotPending;
        if (makerId == checkerId) return DecisionCheck.SameUser;
        if (string.IsNullOrWhiteSpace(comment)) return DecisionCheck.CommentRequired;
        return DecisionCheck.Ok;
    }

    /// <summary>Approval is the only path that turns an Account's KYC into Passed/Manual (R9.2).</summary>
    public static void ApplyApproval(LoanAccount account, DateTime utcNow)
    {
        account.KycStatus = KycStatus.Passed;
        account.KycFlagged = false;
        account.KycSource = KycSource.Manual;
        account.KycCheckedAtUtc = utcNow;
        account.UpdatedAt = utcNow;
    }
}