using Finnova.Models.Contracts.Receipts;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Receipts;
using Finnova.Repository.Context;

namespace Finnova.Tests.Receipts;

internal sealed record ReceiptSeed(
    Guid LobId, Guid BranchId, Guid OfficerId, Guid CustomerId, Guid CompanyId, Guid GlId,
    Guid DraweeBankId, Guid DraweeBranchId, Guid FaBankId,
    string CustomerCode, string AccountNumber, string ClosedAccountNumber, string OtherCustomerAccountNumber);

internal static class ReceiptTestData
{
    public const string GlCode = "110100101";

    private static Customer NewCustomer(Guid companyId, string code, string firstName, Guid? glId)
    {
        var c = new Customer();
        c.SetGeneratedCode(code);
        c.Initialize(companyId, true, false, false, "IND",
            null, null, null, glId, null, null, null, null, null, null, null, null,   // groupCode .. subSectorCode (12)
            firstName, null, null, null, null, null,                                   // firstName .. sixthName (6)
            null, null, null, null, null, null, null, null);                           // notes .. employerEconomicActivityCode (8)
        return c;
    }

    public static ReceiptSeed Seed(FinnovaDbContext db)
    {
        // Unique codes per call so several seeds can share one in-memory database (API tests).
        var tag = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var customerCode = "CUST-" + tag;
        var accountNo = "ACC-" + tag;
        var closedNo = "ACC-C-" + tag;
        var otherNo = "ACC-O-" + tag;
        var companyId = Guid.NewGuid();
        var gl = new GLAccount
        {
            CompanyId = companyId,
            GLCode = GlCode,
            Name = "Loan Receivable",
            Level = GLLevel.L4,
            Status = GLStatus.Active,
            AuthorizedVersionNumber = 1,
            IsPostable = true
        };
        var customer = NewCustomer(companyId, customerCode, "Ravi Kumar", gl.Id);
        var other = NewCustomer(companyId, "CUSTO-" + tag, "Asha Rao", gl.Id);
        var lob = new LineOfBusiness { LOB_Name = "Vehicle", LOB_Description = "Vehicle finance" };
        var branch = new Location { Code = "BR001", Name = "Mumbai Fort Branch", Level = 5, IsActive = true };
        var officer = new UserAccount { UserCode = "MO001", UserName = "mo1", Name = "Meera Shah", IsActive = true };
        var bank = new DraweeBank { BankCode = "HDFC", BankName = "HDFC Bank Ltd" };
        var place = new DraweeBranch
        {
            DraweeBankId = bank.Id,
            PlaceCode = "MUM",
            PlaceName = "Mumbai",
            PostalCode = "400001",
            Address = "Fort",
            StartDate = new DateTime(2020, 1, 1)
        };
        var fa = new FaBankAccount
        {
            BankName = "HDFC Bank Ltd",
            BankBranch = "Mumbai Fort",
            AccountNumber = "50200012345678",
            MicrCode = "400240002",
            GlCode = "110100101",
            SlCode = "SL01"
        };

        db.GLAccounts.Add(gl);
        db.Customers.AddRange(customer, other);
        db.LinesOfBusiness.Add(lob);
        db.Locations.Add(branch);
        db.UserAccounts.Add(officer);
        db.DraweeBanks.Add(bank);
        db.DraweeBranches.Add(place);
        db.FaBankAccounts.Add(fa);
        db.LoanAccounts.AddRange(
            new LoanAccount { AccountNumber = accountNo, AccountName = "Vehicle Loan - Ravi Kumar", CustomerId = customer.Id, Status = AccountStatus.Active },
            new LoanAccount { AccountNumber = closedNo, AccountName = "Closed Loan", CustomerId = customer.Id, Status = AccountStatus.Closed, ClosedAt = DateTime.UtcNow },
            new LoanAccount { AccountNumber = otherNo, AccountName = "Other Loan", CustomerId = other.Id, Status = AccountStatus.Active });
        if (!db.LookupValues.Any(x => x.Module == ReceiptLookup.Module && x.LookupType == ReceiptLookup.ComponentType))
            db.LookupValues.AddRange(
                new LookupValue { Module = ReceiptLookup.Module, LookupType = ReceiptLookup.ComponentType, Code = "INSTALLMENT_INFLOW", Value = "Installment Inflow" },
                new LookupValue { Module = ReceiptLookup.Module, LookupType = ReceiptLookup.ComponentType, Code = "ODI", Value = "Overdue Interest (ODI)" });
        db.SaveChanges();
        return new ReceiptSeed(lob.Id, branch.Id, officer.Id, customer.Id, companyId, gl.Id, bank.Id, place.Id, fa.Id,
            customerCode, accountNo, closedNo, otherNo);
    }

    /// <summary>Gherkin scenario 1: Regular Cheque receipt, 3200.00 Installment Inflow + 300.00 ODI, GL Based = Yes.</summary>
    public static CreateReceiptRequest ChequeRequest(ReceiptSeed s)
    {
        var today = ReceiptClock.TodayIst();
        return new CreateReceiptRequest(
            Guid.NewGuid(), ReceiptEntryMode.Online, null, ReceiptType.Regular, today, today,
            s.LobId, s.BranchId, s.OfficerId, ReceiptMode.Cheque, ReceiptFrom.Customer, true, s.CustomerCode, null, 3500m,
            new[]
            {
                new ReceiptLineRequest(s.AccountNumber, null, "INSTALLMENT_INFLOW", GlCode, null, 3200m),
                new ReceiptLineRequest(s.AccountNumber, null, "ODI", GlCode, null, 300m)
            },
            null, null, new ChequeDetailRequest("458921", today, s.DraweeBankId, s.DraweeBranchId, null, null), null);
    }

    /// <summary>Cash receipt of 500.00 with one 500 note.</summary>
    public static CreateReceiptRequest CashRequest(ReceiptSeed s) => ChequeRequest(s) with
    {
        Mode = ReceiptMode.Cash,
        DocAmount = 500m,
        Cheque = null,
        Lines = new[] { new ReceiptLineRequest(s.AccountNumber, null, "INSTALLMENT_INFLOW", GlCode, null, 500m) },
        Denominations = new[] { new CashDenominationRequest(DenominationKind.Note, 500m, 1) }
    };
}