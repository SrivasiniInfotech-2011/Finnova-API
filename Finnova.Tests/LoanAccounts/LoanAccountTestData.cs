using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Numbering;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Finnova.Tests.LoanAccounts;

internal static class LoanAccountTestData
{
    public static FinnovaDbContext NewDb() => new(new DbContextOptionsBuilder<FinnovaDbContext>()
        .UseInMemoryDatabase("loan-" + Guid.NewGuid())
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);

    public static Customer NewCustomer(string code, string firstName)
    {
        var c = new Customer();
        c.SetGeneratedCode(code);
        c.Initialize(
            Guid.NewGuid(), true, false, false, "IND",                                   // companyId, isCustomer, isGuarantor, isCoApplicant, customerTypeCode
            null, null, null, null, null, null, null, null, null, null, null, null,    // groupCode .. subSectorCode (12)
            firstName, null, null, null, null, null,                                    // firstName .. sixthName (6)
            null, null, null, null, null, null, null, null);                            // notes .. employerEconomicActivityCode (8)
        return c;
    }

    public static LoanAccount SeedLoan(FinnovaDbContext db, string applicationNumber, string accountNumber, string customerCode,
        string customerName, bool flagged = true, KycStatus kyc = KycStatus.Failed, DateTime? createdAt = null)
    {
        var customer = NewCustomer(customerCode, customerName);
        db.Customers.Add(customer);
        var app = new Application
        {
            ApplicationNumber = applicationNumber,
            CustomerId = customer.Id,
            LineOfBusinessId = Guid.NewGuid(),
            BranchId = Guid.NewGuid(),
            ProductCode = "HL",
            SchemeCode = "S1",
            FinanceAmount = 100000m,
            IrrPercent = 12m,
            Tenure = 12,
            RepaymentFrequency = RepaymentFrequency.Monthly,
            DisbursementDate = new DateOnly(2025, 1, 1),
            Status = ApplicationStatus.Processed
        };
        db.Applications.Add(app);
        var account = new LoanAccount
        {
            AccountNumber = accountNumber,
            AccountName = customerName + " " + applicationNumber,
            Type = AccountType.Loan,
            Status = AccountStatus.Active,
            ApplicationId = app.Id,
            CustomerId = customer.Id,
            LineOfBusinessId = app.LineOfBusinessId,
            BranchId = app.BranchId,
            ProductCode = "HL",
            SchemeCode = "S1",
            CustomerNameSnapshot = customerName,
            KycStatus = kyc,
            KycFlagged = flagged,
            KycSource = KycSource.Internal,
            CreatedAt = createdAt ?? DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };
        db.LoanAccounts.Add(account);
        db.SaveChanges();
        return account;
    }

    public static string RowVersionOf(FinnovaDbContext db, Guid accountId) =>
        Convert.ToBase64String(db.LoanAccounts.AsNoTracking().First(a => a.Id == accountId).RowVersion ?? Array.Empty<byte>());
}

internal sealed class FakeNumberingReferences : INumberingReferenceRepository
{
    public Task<string?> GetLobTokenAsync(Guid id, CancellationToken ct) => Task.FromResult<string?>("LOB");
    public Task<string?> GetBranchCodeAsync(Guid id, CancellationToken ct) => Task.FromResult<string?>("BR");
}
