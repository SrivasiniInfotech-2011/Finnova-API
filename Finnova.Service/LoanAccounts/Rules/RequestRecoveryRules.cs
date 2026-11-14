using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;

namespace Finnova.Service.LoanAccounts.Rules;

public static class RequestRecoveryRules
{
    public static bool CanReprocess(AccountRequestStatus status) => status == AccountRequestStatus.DeadLetter;

    public static void ResetForReprocess(AccountCreationRequest request, DateTime utcNow)
    {
        request.Status = AccountRequestStatus.Pending;
        request.AttemptCount = 0;
        request.NextAttemptAtUtc = null;
        request.ClaimedBy = null;
        request.LeaseExpiresAtUtc = null;
        request.UpdatedAtUtc = utcNow;
    }
}