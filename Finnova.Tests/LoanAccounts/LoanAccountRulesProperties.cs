using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Service.LoanAccounts.Rules;
using FsCheck.Xunit;
using Xunit;

namespace Finnova.Tests.LoanAccounts;

public sealed class LoanAccountRulesProperties
{
    [Property(MaxTest = 100)] // Feature: application-account-creation, Property 8: Account status transition rule
    public bool TransitionRule(AccountStatus from, AccountStatus to)
    {
        var allowed = (from, to) is (AccountStatus.Active, AccountStatus.Inactive) or (AccountStatus.Active, AccountStatus.Frozen)
                                  or (AccountStatus.Inactive, AccountStatus.Active) or (AccountStatus.Frozen, AccountStatus.Active);
        return AccountStatusRules.IsTransitionAllowed(from, to) == allowed
            && AccountStatusRules.IsUpdatable(from) == (from != AccountStatus.Closed);
    }

    [Property(MaxTest = 100)] // Feature: application-account-creation, Property 12: Sensitive value masking
    public bool MaskingKeepsLengthAndRevealsAtMostLastFour(string? value)
    {
        var v = value ?? string.Empty;
        var m = PiiMasker.Mask(v);
        if (m.Length != v.Length) return false;
        var hiddenPrefix = Math.Max(0, v.Length - 4);
        return m[..hiddenPrefix].All(c => c == '*') && (v.Length <= 4 ? m.All(c => c == '*') : m.EndsWith(v[^4..], StringComparison.Ordinal));
    }

    [Property(MaxTest = 100)] // Feature: application-account-creation, Property 14: Dead-letter reprocess
    public bool ReprocessOnlyFromDeadLetter(AccountRequestStatus status, byte attempts)
    {
        var request = new AccountCreationRequest { Status = status, AttemptCount = attempts % 4, ClaimedBy = "w" };
        var can = RequestRecoveryRules.CanReprocess(status);
        if (can) RequestRecoveryRules.ResetForReprocess(request, DateTime.UtcNow);
        return can == (status == AccountRequestStatus.DeadLetter)
            && (!can || (request.Status == AccountRequestStatus.Pending && request.AttemptCount == 0 && request.ClaimedBy is null));
    }

    [Property(MaxTest = 100)] // Feature: application-account-creation, Property 19: Resolution input validation (submission)
    public bool SubmissionRule(bool flagged, bool hasPending, string? reason)
    {
        var len = reason?.Trim().Length ?? 0;
        var expected = flagged && !hasPending && len is >= 10 and <= 500;
        return (KycResolutionRules.CheckSubmit(flagged, hasPending, reason) == SubmitCheck.Ok) == expected;
    }

    [Property(MaxTest = 100)] // Feature: application-account-creation, Property 19: Resolution input validation (decision)
    public bool DecisionRule(KycResolutionStatus status, bool sameUser, string? comment)
    {
        var maker = Guid.NewGuid();
        var checker = sameUser ? maker : Guid.NewGuid();
        var expected = status == KycResolutionStatus.Pending && !sameUser && !string.IsNullOrWhiteSpace(comment);
        return (KycResolutionRules.CheckDecision(status, maker, checker, comment) == DecisionCheck.Ok) == expected;
    }

    [Property(MaxTest = 100)]
    // Feature: application-account-creation, Property 13: Four-eyes and Passed only on approval
    // Feature: application-account-creation, Property 18: At most one Pending resolution request per Account
    public bool ResolutionSequencesKeepTheInvariants(byte[] kinds, byte[] users)
    {
        var people = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var account = new LoanAccount { KycStatus = KycStatus.Failed, KycFlagged = true, KycSource = KycSource.Internal };
        var requests = new List<AccountKycResolutionRequest>();
        AccountKycResolutionRequest? pending = null;
        var approvedByAnotherUser = false;

        for (var i = 0; i < Math.Min(kinds.Length, users.Length); i++)
        {
            var actor = people[users[i] % 3];
            switch (kinds[i] % 3)
            {
                case 0: // maker submits
                    var reason = (kinds[i] & 4) != 0 ? "a valid reason text" : "short";
                    if (KycResolutionRules.CheckSubmit(account.KycFlagged, pending is not null, reason) == SubmitCheck.Ok)
                    {
                        pending = new AccountKycResolutionRequest { MakerUserId = actor, Reason = reason, Status = KycResolutionStatus.Pending };
                        requests.Add(pending);
                    }
                    break;
                default: // checker approves (1) or rejects (2)
                    if (pending is null) break;
                    if (KycResolutionRules.CheckDecision(pending.Status, pending.MakerUserId, actor, "comment") != DecisionCheck.Ok) break;
                    pending.CheckerUserId = actor;
                    if (kinds[i] % 3 == 1)
                    {
                        pending.Status = KycResolutionStatus.Approved;
                        KycResolutionRules.ApplyApproval(account, DateTime.UtcNow);
                        approvedByAnotherUser = true;
                    }
                    else pending.Status = KycResolutionStatus.Rejected;
                    pending = null;
                    break;
            }
            if (requests.Count(r => r.Status == KycResolutionStatus.Pending) > 1) return false;   // Property 18
        }

        var passedManually = account.KycStatus == KycStatus.Passed && account.KycSource == KycSource.Manual;
        return passedManually == approvedByAnotherUser                                              // Property 13
            && requests.All(r => r.CheckerUserId is null || r.CheckerUserId != r.MakerUserId)
            && account.KycFlagged == !passedManually;
    }
}
