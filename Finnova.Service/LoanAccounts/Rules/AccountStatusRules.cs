using Finnova.Models.Domain.Enums;

namespace Finnova.Service.LoanAccounts.Rules;

public static class AccountStatusRules
{
    public static bool IsTransitionAllowed(AccountStatus from, AccountStatus to) =>
        (from, to) is (AccountStatus.Active, AccountStatus.Inactive)
                   or (AccountStatus.Active, AccountStatus.Frozen)
                   or (AccountStatus.Inactive, AccountStatus.Active)
                   or (AccountStatus.Frozen, AccountStatus.Active);

    /// <summary>A Closed account is never updatable (R8.15).</summary>
    public static bool IsUpdatable(AccountStatus status) => status != AccountStatus.Closed;
}