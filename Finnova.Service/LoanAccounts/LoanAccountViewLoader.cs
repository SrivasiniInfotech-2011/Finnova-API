using Finnova.Models.Contracts.LoanAccounts;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;

namespace Finnova.Service.LoanAccounts;

public static class LoanAccountViewLoader
{
    public static async Task<LoanAccountResponse> LoadAsync(
        ILoanAccountRepository accounts, AccountCreationKycOptions options, Guid id, CancellationToken ct)
    {
        var data = await accounts.GetViewDataAsync(id, options.PanDocumentCode, options.AadhaarDocumentCode, ct)
                   ?? throw LoanAccountNotFoundException.ForAccount(id);
        return data.ToResponse();
    }

    public static AccountAuditEntryBuilder Audit => new();
}

public sealed class AccountAuditEntryBuilder
{
    public Finnova.Models.Domain.Entities.AccountAuditEntry User(
        Guid? accountId, Guid? requestId, string action, string actor, string correlationId, string summary) => new()
        {
            AccountId = accountId,
            RequestId = requestId,
            Action = action,
            Source = "User",
            Actor = actor,
            CorrelationId = correlationId,
            ChangeSummary = summary,
            OccurredAtUtc = DateTime.UtcNow
        };
}
