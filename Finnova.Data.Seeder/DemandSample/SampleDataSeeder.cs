using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Finnova.Data.Seeder.DemandSample;

public sealed class SampleDataSeeder(FinnovaDbContext db, SampleDataOptions options, SampleOwnershipStore ownership, ILogger<SampleDataSeeder> logger)
{
    private SampleDataOptions Options => options;

    public async Task<string> SeedAsync(CancellationToken ct = default)
    {
        ValidateOptions();
        SampleDataCatalog.Validate();
        var india = await db.Locations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == SampleDataCatalog.IndiaId, ct);
        if (india is null || india.Code != "IND" || india.Name != "India" || india.Level != 1 || india.ParentId is not null || !india.IsActive) throw new SampleDataValidationException("The external IND location invariants failed.");
        await VerifyReferenceLocationsAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await SeedLookupsAsync(ct);
            var organization = await SeedOrganizationAsync(ct);
            var lob = await SeedLobAsync(ct);
            var locations = await SeedLocationsAsync(ct);
            var customers = await SeedCustomersAsync(organization.Id, ct);
            var loans = await SeedApplicationsAccountsSchedulesAsync(lob.Id, locations, customers, ct);
            await SeedReceiptsAsync(loans, ct);
            await SeedHistoryAsync(lob.Id, locations, loans, ct);
            await db.SaveChangesAsync(ct);
            await ValidateDatabaseAsync(lob.Id, locations, loans, ct);
            var fingerprint = await FingerprintAsync(ct);
            await ownership.RegisterAsync("Manifest", SampleDataCatalog.DatasetKey, SampleDataCatalog.StableId("Manifest", SampleDataCatalog.DatasetKey), fingerprint, ct);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            logger.LogInformation("Demand sample {DatasetKey} committed with fingerprint {Fingerprint}.", SampleDataCatalog.DatasetKey, fingerprint);
            return fingerprint;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private void ValidateOptions()
    {
        if (Options.SampleDataKey != SampleDataCatalog.DatasetKey) throw new SampleDataValidationException("SampleDataKey must be SAMPLE-DP-V1.");
        if (Options.SampleAsOfDate != SampleDataCatalog.AsOfDate) throw new SampleDataValidationException("SampleAsOfDate must be 2026-03-31.");
        if (Options.BusinessTimezone is not ("Asia/Kolkata" or "India Standard Time")) throw new SampleDataValidationException("BusinessTimezone must identify Asia/Kolkata.");
    }

    private async Task SeedLookupsAsync(CancellationToken ct)
    {
        await UpsertNationalityAsync(ct);
        var order = 1;
        foreach (var item in SampleDataCatalog.LookupRows) await UpsertLookupAsync(item.Type, item.Code, item.Value, order++, ct);
    }

    private async Task UpsertNationalityAsync(CancellationToken ct)
    {
        const string key = "IN"; var id = SampleDataCatalog.StableId("Nationality", key);
        var row = await db.Nationalities.SingleOrDefaultAsync(x => x.Id == id, ct) ?? await db.Nationalities.SingleOrDefaultAsync(x => x.Code == key, ct);
        if (row is not null) await RequireExistingAsync("Nationality", key, id, row.Id, ct); else row = new Nationality { Id = id };
        row.Code = key; row.Name = "Indian"; row.IsActive = true; row.CreatedAt = SampleDataCatalog.SeedTimestampUtc; row.UpdatedAt = SampleDataCatalog.SeedTimestampUtc;
        if (db.Entry(row).State == EntityState.Detached) db.Nationalities.Add(row);
        await ownership.RegisterAsync("Nationality", key, id, SampleDataCatalog.Hash(new { row.Id, row.Code, row.Name, row.IsActive }), ct);
    }

    private async Task UpsertLookupAsync(string type, string code, string value, int displayOrder, CancellationToken ct)
    {
        var key = $"{type}:{code}"; var id = SampleDataCatalog.StableId("Lookup", key);
        var row = await db.LookupValues.SingleOrDefaultAsync(x => x.Id == id, ct) ?? await db.LookupValues.SingleOrDefaultAsync(x => x.Module == "DemandProcessing" && x.LookupType == type && x.Code == code, ct);
        if (row is not null) await RequireExistingAsync("Lookup", key, id, row.Id, ct); else row = new LookupValue { Id = id };
        row.Module = "DemandProcessing"; row.LookupType = type; row.Code = code; row.Value = value; row.DisplayOrder = displayOrder; row.IsActive = true; row.IsSystemLocked = true; row.CreatedAt = SampleDataCatalog.SeedTimestampUtc; row.UpdatedAt = SampleDataCatalog.SeedTimestampUtc;
        if (db.Entry(row).State == EntityState.Detached) db.LookupValues.Add(row);
        await ownership.RegisterAsync("Lookup", key, id, SampleDataCatalog.Hash(new { row.Id, row.Module, row.LookupType, row.Code, row.Value, row.DisplayOrder, row.IsActive, row.IsSystemLocked }), ct);
    }

    private async Task<Organization> SeedOrganizationAsync(CancellationToken ct)
    {
        var id = SampleDataCatalog.StableId("Organization", SampleDataCatalog.OrganizationCode); var row = await db.Organizations.SingleOrDefaultAsync(x => x.Id == id, ct) ?? await db.Organizations.SingleOrDefaultAsync(x => x.Code == SampleDataCatalog.OrganizationCode, ct);
        if (row is not null) await RequireExistingAsync("Organization", SampleDataCatalog.OrganizationCode, id, row.Id, ct); else row = new Organization { Id = id };
        row.Code = SampleDataCatalog.OrganizationCode; row.Name = "Finnova Tamil Nadu Lending Sample"; row.ConstitutionType = ConstitutionType.PrivateLtd; row.AccountingCurrency = "INR"; row.Status = OrganizationStatus.Active; row.CorporateCity = "Chennai"; row.CorporateState = "Tamil Nadu"; row.CorporateCountry = "India"; row.CreatedAt = SampleDataCatalog.SeedTimestampUtc; row.UpdatedAt = SampleDataCatalog.SeedTimestampUtc;
        if (db.Entry(row).State == EntityState.Detached) db.Organizations.Add(row);
        await ownership.RegisterAsync("Organization", SampleDataCatalog.OrganizationCode, id, SampleDataCatalog.Hash(new { row.Id, row.Code, row.Name, row.ConstitutionType, row.AccountingCurrency, row.Status, row.CorporateCity, row.CorporateState, row.CorporateCountry }), ct); return row;
    }

    private async Task<LineOfBusiness> SeedLobAsync(CancellationToken ct)
    {
        var id = SampleDataCatalog.StableId("LineOfBusiness", SampleDataCatalog.LobKey); var row = await db.LinesOfBusiness.SingleOrDefaultAsync(x => x.Id == id, ct) ?? await db.LinesOfBusiness.SingleOrDefaultAsync(x => x.LOB_Description == SampleDataCatalog.LobKey, ct);
        if (row is not null) await RequireExistingAsync("LineOfBusiness", SampleDataCatalog.LobKey, id, row.Id, ct); else row = new LineOfBusiness { Id = id };
        row.LOB_Name = "Retail Lending Sample"; row.LOB_Description = SampleDataCatalog.LobKey; row.IsActive = true; row.CreatedAt = SampleDataCatalog.SeedTimestampUtc; row.UpdatedAt = SampleDataCatalog.SeedTimestampUtc;
        if (db.Entry(row).State == EntityState.Detached) db.LinesOfBusiness.Add(row);
        await ownership.RegisterAsync("LineOfBusiness", SampleDataCatalog.LobKey, id, SampleDataCatalog.Hash(new { row.Id, row.LOB_Name, row.LOB_Description, row.IsActive }), ct); return row;
    }

    private async Task<Dictionary<string, Location>> SeedLocationsAsync(CancellationToken ct)
    {
        var map = new Dictionary<string, Location>(StringComparer.Ordinal) { ["IND"] = await db.Locations.SingleAsync(x => x.Id == SampleDataCatalog.IndiaId, ct) };
        foreach (var item in SampleDataCatalog.Locations) { if (item.Level <= SampleDataCatalog.ReferenceLocationMaxLevel) { map[item.Code] = await RequireReferenceLocationAsync(item, map[item.ParentCode], ct); continue; } var id = SampleDataCatalog.StableId("Location", item.Code); var row = await db.Locations.SingleOrDefaultAsync(x => x.Id == id, ct) ?? await db.Locations.SingleOrDefaultAsync(x => x.Code == item.Code, ct); if (row is not null) await RequireExistingAsync("Location", item.Code, id, row.Id, ct); else row = new Location { Id = id }; var parent = map[item.ParentCode]; row.Code = item.Code; row.Name = item.Name; row.Level = item.Level; row.ParentId = parent.Id; row.Description = SampleDataCatalog.DatasetKey; row.IsActive = true; row.CreatedAt = SampleDataCatalog.SeedTimestampUtc; row.UpdatedAt = SampleDataCatalog.SeedTimestampUtc; if (db.Entry(row).State == EntityState.Detached) db.Locations.Add(row); map[item.Code] = row; await ownership.RegisterAsync("Location", item.Code, id, SampleDataCatalog.Hash(new { row.Id, row.Code, row.Name, row.Level, row.ParentId, row.IsActive }), ct); }
        foreach (var item in SampleDataCatalog.Branches) { var id = SampleDataCatalog.StableId("Location", item.Code); var row = await db.Locations.SingleOrDefaultAsync(x => x.Id == id, ct) ?? await db.Locations.SingleOrDefaultAsync(x => x.Code == item.Code, ct); if (row is not null) await RequireExistingAsync("Location", item.Code, id, row.Id, ct); else row = new Location { Id = id }; var parent = map[item.ZoneCode]; row.Code = item.Code; row.Name = item.Name; row.Level = 5; row.ParentId = parent.Id; row.Description = $"{SampleDataCatalog.DatasetKey}|{item.CityCode}|{item.ZoneCode}"; row.IsActive = true; row.CreatedAt = SampleDataCatalog.SeedTimestampUtc; row.UpdatedAt = SampleDataCatalog.SeedTimestampUtc; if (db.Entry(row).State == EntityState.Detached) db.Locations.Add(row); map[item.Code] = row; await ownership.RegisterAsync("Location", item.Code, id, SampleDataCatalog.Hash(new { row.Id, row.Code, row.Name, row.Level, row.ParentId, row.Description, row.IsActive }), ct); }
        return map;
    }

    private async Task VerifyReferenceLocationsAsync(CancellationToken ct)
    {
        var map = new Dictionary<string, Location>(StringComparer.Ordinal) { ["IND"] = await db.Locations.SingleAsync(x => x.Id == SampleDataCatalog.IndiaId, ct) };
        foreach (var item in SampleDataCatalog.Locations.Where(x => x.Level <= SampleDataCatalog.ReferenceLocationMaxLevel)) map[item.Code] = await RequireReferenceLocationAsync(item, map[item.ParentCode], ct);
    }

    private async Task<Location> RequireReferenceLocationAsync(SampleLocation item, Location parent, CancellationToken ct)
    {
        var row = await db.Locations.SingleOrDefaultAsync(x => x.Code == item.Code && x.ParentId == parent.Id, ct);
        if (row is null || row.Level != item.Level || row.ParentId != parent.Id || !row.IsActive) throw new SampleDataValidationException($"Reference location {item.Code} is missing or invalid. Apply the reference-data migration first.");
        return row;
    }

    private async Task<Dictionary<string, Customer>> SeedCustomersAsync(Guid organizationId, CancellationToken ct)
    {
        var result = new Dictionary<string, Customer>(StringComparer.Ordinal);
        foreach (var item in SampleDataCatalog.Customers)
        {
            var id = SampleDataCatalog.StableId("Customer", item.Key); var row = await db.Customers.SingleOrDefaultAsync(x => x.Id == id, ct) ?? await db.Customers.SingleOrDefaultAsync(x => x.CustomerCode == item.Code, ct); if (row is not null) await RequireExistingAsync("Customer", item.Key, id, row.Id, ct); else row = Customer.CreateWithId(id);
            row.SetGeneratedCode(item.Code); row.Initialize(organizationId, true, false, false, "INDIVIDUAL", null, null, null, null, "PRIVATE", "IN", "MR", "MARRIED", null, null, null, null, item.FirstName, null, item.LastName, null, null, null, null, false, false, null, "Owned", false, null, null); row.SetFixtureTimestamps(SampleDataCatalog.SeedTimestampUtc, SampleDataCatalog.SeedTimestampUtc); if (db.Entry(row).State == EntityState.Detached) db.Customers.Add(row); await ownership.RegisterAsync("Customer", item.Key, id, SampleDataCatalog.Hash(new { row.Id, row.CustomerCode, row.CompanyId, row.CustomerTypeCode, row.NationalityCode, row.TitleCode, row.MaritalStatusCode, row.FirstName, row.ThirdName, row.Status, row.CreatedAtUtc, row.UpdatedAtUtc }), ct);
            await SeedCustomerChildrenAsync(row, item, ct); result[item.Key] = row;
        }
        return result;
    }

    private async Task SeedCustomerChildrenAsync(Customer customer, SampleCustomer item, CancellationToken ct)
    {
        var addressId = SampleDataCatalog.StableId("CustomerAddress", item.Key); var address = await db.CustomerAddresses.SingleOrDefaultAsync(x => x.Id == addressId, ct); if (address is not null) await RequireExistingAsync("CustomerAddress", item.Key, addressId, address.Id, ct); else address = CustomerAddress.CreateWithId(addressId); address.SetFixtureIdentity(customer.Id); address.Initialize(CustomerAddressType.Permanent, "12", null, item.PostalCode, item.City, "Tamil Nadu", "India", null, "Sample Road", null, null, $"900000000{item.Key[^1]}", null, $"{item.Key.ToLowerInvariant()}@sample.finnova.in", null, null, null, null, null); address.SetFixtureTimestamps(SampleDataCatalog.SeedTimestampUtc, SampleDataCatalog.SeedTimestampUtc); if (db.Entry(address).State == EntityState.Detached) db.CustomerAddresses.Add(address); await ownership.RegisterAsync("CustomerAddress", item.Key, addressId, SampleDataCatalog.Hash(new { address.Id, address.CustomerId, address.AddressType, address.PostalCode, address.City, address.State, address.Country, address.Mobile, address.Email, address.CreatedAtUtc, address.UpdatedAtUtc }), ct);
        var detailsId = SampleDataCatalog.StableId("IndividualCustomerDetails", item.Key); var details = await db.IndividualCustomerDetails.SingleOrDefaultAsync(x => x.Id == detailsId, ct); if (details is not null) await RequireExistingAsync("IndividualCustomerDetails", item.Key, detailsId, details.Id, ct); else details = IndividualCustomerDetails.CreateWithId(detailsId); details.SetFixtureIdentity(customer.Id); details.Initialize("M", new DateOnly(1985, 5, 10), null, 1, 2, null, "Flat", true, 2500000m, 500000m, 2000000m, "Graduate", "Salaried employee", null, null, null, null); details.SetFixtureTimestamps(SampleDataCatalog.SeedTimestampUtc, SampleDataCatalog.SeedTimestampUtc); if (db.Entry(details).State == EntityState.Detached) db.IndividualCustomerDetails.Add(details); await ownership.RegisterAsync("IndividualCustomerDetails", item.Key, detailsId, SampleDataCatalog.Hash(new { details.Id, details.CustomerId, details.GenderCode, details.DateOfBirth, details.Qualification, details.Profession, details.CreatedAtUtc, details.UpdatedAtUtc }), ct);
    }

    private async Task<Dictionary<string, LoanAccount>> SeedApplicationsAccountsSchedulesAsync(Guid lobId, Dictionary<string, Location> locations, Dictionary<string, Customer> customers, CancellationToken ct)
    {
        var result = new Dictionary<string, LoanAccount>(StringComparer.Ordinal);
        foreach (var item in SampleDataCatalog.Loans)
        {
            var customer = customers[item.CustomerKey]; var branch = locations[item.BranchCode]; var appId = SampleDataCatalog.StableId("Application", item.ApplicationNumber); var app = await db.Applications.SingleOrDefaultAsync(x => x.Id == appId, ct) ?? await db.Applications.SingleOrDefaultAsync(x => x.ApplicationNumber == item.ApplicationNumber, ct); if (app is not null) await RequireExistingAsync("Application", item.ApplicationNumber, appId, app.Id, ct); else app = new Application { Id = appId }; app.ApplicationNumber = item.ApplicationNumber; app.LineOfBusinessId = lobId; app.CustomerId = customer.Id; app.BranchId = branch.Id; app.ProductCode = "SAMPLE-PL"; app.SchemeCode = "TN-MONTHLY"; app.FinanceAmount = item.FinanceAmount; app.IrrPercent = 12m; app.Tenure = item.Tenure; app.RepaymentFrequency = RepaymentFrequency.Monthly; app.DisbursementDate = new DateOnly(2025, 9, 15); app.ProcessingCharges = 0m; app.Status = ApplicationStatus.Processed; app.CreatedAtUtc = SampleDataCatalog.SeedTimestampUtc; app.UpdatedAtUtc = SampleDataCatalog.SeedTimestampUtc; if (db.Entry(app).State == EntityState.Detached) db.Applications.Add(app); await ownership.RegisterAsync("Application", item.ApplicationNumber, appId, SampleDataCatalog.Hash(new { app.Id, app.ApplicationNumber, app.LineOfBusinessId, app.CustomerId, app.BranchId, app.ProductCode, app.SchemeCode, app.FinanceAmount, app.IrrPercent, app.Tenure, app.RepaymentFrequency, app.DisbursementDate, app.Status }), ct);
            var genericId = SampleDataCatalog.StableId("Account", item.AccountNumber); var generic = await db.Accounts.SingleOrDefaultAsync(x => x.Id == genericId, ct) ?? await db.Accounts.SingleOrDefaultAsync(x => x.AccountNumber == item.AccountNumber, ct); if (generic is not null) await RequireExistingAsync("Account", item.AccountNumber, genericId, generic.Id, ct); else generic = new Account { Id = genericId }; generic.AccountNumber = item.AccountNumber; generic.AccountName = $"{item.AccountNumber} {customer.FirstName} {customer.ThirdName}"; generic.Type = AccountType.Loan; generic.Status = AccountStatus.Active; generic.Balance = item.FinanceAmount; generic.Currency = "INR"; generic.OrganizationId = customer.CompanyId; generic.BranchCode = item.BranchCode; generic.OpenedAt = new DateTime(2025, 9, 15, 0, 0, 0, DateTimeKind.Utc); generic.CreatedAt = SampleDataCatalog.SeedTimestampUtc; generic.UpdatedAt = SampleDataCatalog.SeedTimestampUtc; if (db.Entry(generic).State == EntityState.Detached) db.Accounts.Add(generic); await ownership.RegisterAsync("Account", item.AccountNumber, genericId, SampleDataCatalog.Hash(new { generic.Id, generic.AccountNumber, generic.AccountName, generic.Type, generic.Status, generic.Balance, generic.Currency, generic.OrganizationId, generic.BranchCode, generic.OpenedAt }), ct);
            var loanId = SampleDataCatalog.StableId("LoanAccount", item.AccountNumber); var loan = await db.LoanAccounts.SingleOrDefaultAsync(x => x.Id == loanId, ct) ?? await db.LoanAccounts.SingleOrDefaultAsync(x => x.AccountNumber == item.AccountNumber, ct); if (loan is not null) await RequireExistingAsync("LoanAccount", item.AccountNumber, loanId, loan.Id, ct); else loan = new LoanAccount { Id = loanId }; loan.AccountNumber = item.AccountNumber; loan.AccountName = generic.AccountName; loan.Type = AccountType.Loan; loan.Status = AccountStatus.Active; loan.Balance = item.FinanceAmount; loan.Currency = "INR"; loan.BranchCode = item.BranchCode; loan.OpenedAt = generic.OpenedAt; loan.CreatedAt = SampleDataCatalog.SeedTimestampUtc; loan.UpdatedAt = SampleDataCatalog.SeedTimestampUtc; loan.ApplicationId = app.Id; loan.CustomerId = customer.Id; loan.LineOfBusinessId = lobId; loan.BranchId = branch.Id; loan.ProductCode = app.ProductCode; loan.SchemeCode = app.SchemeCode; loan.CustomerNameSnapshot = $"{customer.FirstName} {customer.ThirdName}"; loan.CreationNotes = SampleDataCatalog.DatasetKey; loan.KycStatus = KycStatus.Passed; loan.KycFlagged = false; if (db.Entry(loan).State == EntityState.Detached) db.LoanAccounts.Add(loan); await ownership.RegisterAsync("LoanAccount", item.AccountNumber, loanId, SampleDataCatalog.Hash(new { loan.Id, loan.AccountNumber, loan.Type, loan.Status, loan.Balance, loan.Currency, loan.ApplicationId, loan.CustomerId, loan.LineOfBusinessId, loan.BranchId, loan.ProductCode, loan.SchemeCode, loan.KycStatus, loan.KycFlagged }), ct);
            await SeedScheduleAsync(app, item, ct); result[item.Key] = loan;
        }
        return result;
    }

    private async Task SeedScheduleAsync(Application app, SampleLoan item, CancellationToken ct)
    {
        var scheduleId = SampleDataCatalog.StableId("ApplicationSchedule", item.ApplicationNumber); var schedule = await db.ApplicationSchedules.Include(x => x.Installments).SingleOrDefaultAsync(x => x.Id == scheduleId, ct); if (schedule is not null) await RequireExistingAsync("ApplicationSchedule", item.ApplicationNumber, scheduleId, schedule.Id, ct); else schedule = new ApplicationSchedule { Id = scheduleId }; var principal = SampleDataCatalog.Money(item.Emi * .72m); var interest = SampleDataCatalog.Money(item.Emi - principal); schedule.ApplicationId = app.Id; schedule.IsFinalized = true; schedule.GeneratedAtUtc = SampleDataCatalog.SeedTimestampUtc; schedule.FinanceAmount = item.FinanceAmount; schedule.IrrPercent = 12m; schedule.Tenure = item.Tenure; schedule.RepaymentFrequency = RepaymentFrequency.Monthly; schedule.DisbursementDate = app.DisbursementDate; schedule.PeriodicInterestRate = 1m; schedule.EmiAmount = item.Emi; schedule.TotalInstallmentAmount = SampleDataCatalog.Money(item.Emi * 6m); schedule.TotalPrincipalAmount = SampleDataCatalog.Money(principal * 6m); schedule.TotalInterestAmount = SampleDataCatalog.Money(interest * 6m); if (db.Entry(schedule).State == EntityState.Detached) db.ApplicationSchedules.Add(schedule);
        foreach (var (period, index) in SampleDataCatalog.Periods.Select((p, i) => (p, i + 1))) { var id = SampleDataCatalog.StableId("ApplicationInstallment", $"{item.ApplicationNumber}:{period.Key}"); var installment = await db.ApplicationInstallments.SingleOrDefaultAsync(x => x.Id == id, ct); if (installment is not null) await RequireExistingAsync("ApplicationInstallment", $"{item.ApplicationNumber}:{period.Key}", id, installment.Id, ct); else installment = new ApplicationInstallment { Id = id }; installment.ScheduleId = schedule.Id; installment.InstallmentNumber = index; installment.DueDate = period.DueDate; installment.OpeningBalance = SampleDataCatalog.Money(item.FinanceAmount - principal * (index - 1)); installment.PrincipalPortion = principal; installment.InterestPortion = interest; installment.InstallmentAmount = item.Emi; installment.ClosingBalance = SampleDataCatalog.Money(installment.OpeningBalance - principal); if (db.Entry(installment).State == EntityState.Detached) db.ApplicationInstallments.Add(installment); await ownership.RegisterAsync("ApplicationInstallment", $"{item.ApplicationNumber}:{period.Key}", id, SampleDataCatalog.Hash(new { installment.Id, installment.ScheduleId, installment.InstallmentNumber, installment.DueDate, installment.OpeningBalance, installment.InterestPortion, installment.PrincipalPortion, installment.InstallmentAmount, installment.ClosingBalance }), ct); }
        await ownership.RegisterAsync("ApplicationSchedule", item.ApplicationNumber, scheduleId, SampleDataCatalog.Hash(new { schedule.Id, schedule.ApplicationId, schedule.IsFinalized, schedule.FinanceAmount, schedule.EmiAmount, schedule.TotalInstallmentAmount, schedule.TotalPrincipalAmount, schedule.TotalInterestAmount, InstallmentCount = 6 }), ct);
    }

    private async Task SeedReceiptsAsync(Dictionary<string, LoanAccount> loans, CancellationToken ct)
    {
        foreach (var item in SampleDataCatalog.Receipts) { var loan = loans[item.LoanKey]; var generic = await db.Accounts.SingleAsync(x => x.AccountNumber == loan.AccountNumber, ct); var id = SampleDataCatalog.StableId("Transaction", item.Key); var row = await db.Transactions.SingleOrDefaultAsync(x => x.Id == id, ct); if (row is not null) await RequireExistingAsync("Transaction", item.Key, id, row.Id, ct); else row = new Transaction { Id = id }; row.AccountId = generic.Id; row.Type = item.IsDeposit ? TransactionType.Deposit : TransactionType.Withdrawal; row.Status = item.IsCompleted ? TransactionStatus.Completed : TransactionStatus.Pending; row.Amount = item.EntityAmount; row.BalanceAfter = generic.Balance; row.ReferenceNumber = item.Reference; row.Description = item.Description; row.TransactionDate = item.TransactionDateUtc; row.CreatedAt = SampleDataCatalog.SeedTimestampUtc; if (db.Entry(row).State == EntityState.Detached) db.Transactions.Add(row); await ownership.RegisterAsync("Transaction", item.Key, id, SampleDataCatalog.Hash(new { row.Id, row.AccountId, row.Type, row.Status, row.Amount, row.BalanceAfter, row.ReferenceNumber, row.Description, row.TransactionDate, row.CreatedAt }), ct); }
    }

    private async Task SeedHistoryAsync(Guid lobId, Dictionary<string, Location> locations, Dictionary<string, LoanAccount> loans, CancellationToken ct)
    {
        foreach (var period in SampleDataCatalog.HistoryPeriods)
        {
            var run = new DemandRun { Id = SampleDataCatalog.StableId("DemandRun", period.Key), LineOfBusinessId = lobId, Frequency = SampleDataCatalog.Frequency, PeriodYear = period.Year, PeriodMonth = period.Month, BusinessTimezone = "Asia/Kolkata", LogicalScopeKey = $"{SampleDataCatalog.DatasetKey}|{lobId:N}|{period.Key}", ProcessingCutoffReference = SampleDataCatalog.SnapshotReference, Status = DemandRunStatus.Completed, BillingStatus = BillingStageStatus.Succeeded, Source = DemandSource.Automizer, ActorId = "sample-seeder", CorrelationId = $"{SampleDataCatalog.DatasetKey}:{period.Key}", CurrentRevisionNumber = 1, RequestedAtUtc = SampleDataCatalog.SeedTimestampUtc, CompletedAtUtc = SampleDataCatalog.SeedTimestampUtc };
            run = await UpsertRunAsync(run, period, locations, ct);
            var revision = new DemandRunRevision { Id = SampleDataCatalog.StableId("DemandRevision", period.Key), DemandRunId = run.Id, RevisionNumber = 1, Status = DemandRunStatus.Completed, CreatedAtUtc = SampleDataCatalog.SeedTimestampUtc, CompletedAtUtc = SampleDataCatalog.SeedTimestampUtc, DemandRun = run };
            var stage = new BillingStageRun { Id = SampleDataCatalog.StableId("BillingStage", period.Key), DemandRunId = run.Id, RevisionNumber = 1, LogicalBillingScopeKey = run.LogicalScopeKey, Status = BillingStageStatus.Succeeded, ProcessingCutoffReference = SampleDataCatalog.SnapshotReference, AllocationPolicyReference = "ConfiguredAllocationPolicy", ChargePolicyReference = "Default", ChargePolicyKind = ChargePolicyKind.Default, CreatedAtUtc = SampleDataCatalog.SeedTimestampUtc, CompletedAtUtc = SampleDataCatalog.SeedTimestampUtc, CorrelationId = run.CorrelationId, DemandRun = run };
            var billing = new BillingSnapshot { Id = SampleDataCatalog.StableId("BillingSnapshot", period.Key), BillingStageRunId = stage.Id, RevisionNumber = 1, Version = $"{SampleDataCatalog.DatasetKey}:B:{period.Key}", LogicalScopeKey = run.LogicalScopeKey, ProcessingCutoffReference = SampleDataCatalog.SnapshotReference, IsPublished = true, ActorId = "sample-seeder", Source = "SampleData", CorrelationId = run.CorrelationId, PublishedAtUtc = SampleDataCatalog.SeedTimestampUtc };
            var demand = new DemandSnapshot { Id = SampleDataCatalog.StableId("DemandSnapshot", period.Key), DemandRunId = run.Id, RevisionNumber = 1, BillingSnapshotId = billing.Id, Version = $"{SampleDataCatalog.DatasetKey}:D:{period.Key}", IsPublished = true, PublishedAtUtc = SampleDataCatalog.SeedTimestampUtc, DemandRun = run };
            run.BillingStageRunId = stage.Id; run.BillingSnapshotId = billing.Id; run.DemandSnapshotId = demand.Id; revision.BillingSnapshotId = billing.Id; revision.DemandSnapshotId = demand.Id;
            db.DemandRunRevisions.Update(revision); db.BillingStageRuns.Update(stage); db.BillingSnapshots.Update(billing); db.DemandSnapshots.Update(demand);
            var rows = new List<DemandRow>();
            foreach (var item in SampleDataCatalog.Loans) { var loan = loans[item.Key]; var branch = locations[item.BranchCode]; var arrears = period.Key == "2026-02" && item.Key == "A002" ? 12000m : period.Key == "2026-02" && item.Key == "A004" ? 8000m : 0m; var resultId = SampleDataCatalog.StableId("BillingResult", $"{period.Key}:{item.Key}"); var result = new BillingPeriodResult { Id = resultId, BillingStageRunId = stage.Id, AccountId = loan.Id, AccountDisplay = item.AccountNumber, CustomerDisplay = item.CustomerKey, CustomerId = loan.CustomerId!.Value, LineOfBusinessId = lobId, BranchId = branch.Id, AccountStatusAtCutoff = "Active", IsEligible = true, Status = BillingPeriodResultStatus.Succeeded, ScheduleReference = $"{SampleDataCatalog.DatasetKey}|SCHEDULE|{item.ApplicationNumber}|{period.Key}", ScheduledPrincipal = SampleDataCatalog.Money(item.Emi * .72m), ScheduledInterestOrEmi = SampleDataCatalog.Money(item.Emi * .28m), ScheduledInstallment = item.Emi, CurrentDue = item.Emi, Arrears = arrears, Odi = 0m, OtherCharges = 0m, AllocationPolicyReference = "ConfiguredAllocationPolicy", ChargePolicyReference = "Default", ProcessingCutoffReference = SampleDataCatalog.SnapshotReference, CreatedAtUtc = SampleDataCatalog.SeedTimestampUtc }; db.BillingPeriodResults.Update(result); var row = new DemandRow { Id = SampleDataCatalog.StableId("DemandRow", $"{period.Key}:{item.Key}"), DemandRunRevisionId = revision.Id, DemandSnapshotId = demand.Id, BillingPeriodResultId = result.Id, AccountId = loan.Id, CustomerId = loan.CustomerId.Value, LineOfBusinessId = lobId, BranchId = branch.Id, AccountDisplay = item.AccountNumber, CustomerDisplay = item.CustomerKey, Status = DemandRowStatus.Completed, CurrentDue = item.Emi, Arrears = arrears, Odi = 0m, OtherCharges = 0m, TotalCharges = 0m, TotalDemand = SampleDataCatalog.Money(item.Emi + arrears), ScheduleReference = result.ScheduleReference, ProcessingCutoffReference = SampleDataCatalog.SnapshotReference, OrderingKey = loan.Id.ToString("N") }; rows.Add(row); db.DemandRows.Update(row); var evidence = new BillingAllocationEvidence { Id = SampleDataCatalog.StableId("BillingEvidence", $"{period.Key}:{item.Key}"), BillingStageRunId = stage.Id, BillingPeriodResultId = result.Id, EventType = AllocationEventType.OtherCharge, SourceEventId = result.Id, AllocationPolicyReference = "ConfiguredAllocationPolicy", AllocationSequence = 0, AllocatedAmount = 0m, TargetComponent = "CurrentDue", Outcome = AllocationOutcome.Excluded, SafeReason = "Historical fixture row has no source allocation event.", ApplicationKey = $"{SampleDataCatalog.DatasetKey}:{period.Key}:{item.Key}" }; db.BillingAllocationEvidence.Update(evidence); await ownership.RegisterAsync("BillingResult", $"{period.Key}:{item.Key}", resultId, SampleDataCatalog.Hash(new { result.Id, result.BillingStageRunId, result.AccountId, result.CustomerId, result.LineOfBusinessId, result.BranchId, result.CurrentDue, result.Arrears, result.Status }), ct); await ownership.RegisterAsync("DemandRow", $"{period.Key}:{item.Key}", row.Id, SampleDataCatalog.Hash(new { row.Id, row.DemandSnapshotId, row.AccountId, row.CustomerId, row.LineOfBusinessId, row.BranchId, row.CurrentDue, row.Arrears, row.TotalDemand }), ct); await ownership.RegisterAsync("BillingEvidence", $"{period.Key}:{item.Key}", evidence.Id, SampleDataCatalog.Hash(new { evidence.Id, evidence.BillingStageRunId, evidence.BillingPeriodResultId, evidence.EventType, evidence.AllocatedAmount, evidence.Outcome }), ct); }
            foreach (var branch in SampleDataCatalog.Branches) { var batchId = SampleDataCatalog.StableId("DemandBatch", $"{period.Key}:{branch.Code}"); var batch = new DemandBatch { Id = batchId, DemandRunRevisionId = revision.Id, DemandSnapshotId = demand.Id, DemandRunId = run.Id, RevisionNumber = 1, BranchId = locations[branch.Code].Id, LineOfBusinessId = lobId, Frequency = SampleDataCatalog.Frequency, PeriodYear = period.Year, PeriodMonth = period.Month, Status = branch.Code == "MDU-B02" ? DemandBatchStatus.NoDemand : DemandBatchStatus.ReadyToSpool, SpoolIdempotencyKey = $"{run.Id:N}:1:{locations[branch.Code].Id:N}" }; foreach (var row in rows.Where(x => x.BranchId == batch.BranchId)) row.DemandBatchId = batch.Id; var branchRows = rows.Where(x => x.BranchId == batch.BranchId).ToArray(); batch.RowCount = branchRows.Length; batch.CurrentDueTotal = SampleDataCatalog.Money(branchRows.Sum(x => x.CurrentDue)); batch.ArrearsTotal = SampleDataCatalog.Money(branchRows.Sum(x => x.Arrears)); batch.TotalCharges = 0m; batch.TotalDemand = SampleDataCatalog.Money(branchRows.Sum(x => x.TotalDemand)); db.DemandBatches.Update(batch); await ownership.RegisterAsync("DemandBatch", $"{period.Key}:{branch.Code}", batchId, SampleDataCatalog.Hash(new { batch.Id, batch.DemandRunId, batch.RevisionNumber, batch.BranchId, batch.LineOfBusinessId, batch.RowCount, batch.Status, batch.CurrentDueTotal, batch.ArrearsTotal, batch.TotalDemand }), ct); }
            run.RowCount = rows.Count; run.BatchCount = SampleDataCatalog.Branches.Count; run.CurrentDueTotal = SampleDataCatalog.Money(rows.Sum(x => x.CurrentDue)); run.ArrearsTotal = SampleDataCatalog.Money(rows.Sum(x => x.Arrears)); run.TotalCharges = 0m; run.TotalDemand = SampleDataCatalog.Money(rows.Sum(x => x.TotalDemand)); revision.RowCount = run.RowCount; revision.BatchCount = run.BatchCount; revision.CurrentDueTotal = run.CurrentDueTotal; revision.ArrearsTotal = run.ArrearsTotal; revision.TotalCharges = 0m; revision.TotalDemand = run.TotalDemand; stage.EligibleAccountCount = rows.Count; stage.SuccessfulResultCount = rows.Count; billing.ResultCount = rows.Count; billing.CurrentDueTotal = run.CurrentDueTotal; billing.ArrearsTotal = run.ArrearsTotal; billing.OdiTotal = 0m; billing.OtherChargesTotal = 0m; stage.BillingSnapshotId = billing.Id; stage.BillingSnapshotVersion = billing.Version; demand.RowCount = rows.Count; demand.BatchCount = run.BatchCount; demand.TotalDemand = run.TotalDemand;
            await ownership.RegisterAsync("DemandRun", period.Key, run.Id, SampleDataCatalog.Hash(new { run.Id, run.LineOfBusinessId, run.Frequency, run.PeriodYear, run.PeriodMonth, run.BusinessTimezone, run.LogicalScopeKey, run.ProcessingCutoffReference, run.Status, run.BillingStatus, run.CurrentRevisionNumber, run.BillingStageRunId, run.BillingSnapshotId, run.DemandSnapshotId, run.RowCount, run.BatchCount, run.CurrentDueTotal, run.ArrearsTotal, run.TotalCharges, run.TotalDemand }), ct);
            await ownership.RegisterAsync("DemandRevision", period.Key, revision.Id, SampleDataCatalog.Hash(new { revision.Id, revision.DemandRunId, revision.RevisionNumber, revision.Status, revision.BillingSnapshotId, revision.DemandSnapshotId, revision.RowCount, revision.BatchCount, revision.CurrentDueTotal, revision.ArrearsTotal, revision.TotalCharges, revision.TotalDemand }), ct);
            await ownership.RegisterAsync("BillingStage", period.Key, stage.Id, SampleDataCatalog.Hash(new { stage.Id, stage.DemandRunId, stage.RevisionNumber, stage.Status, stage.BillingSnapshotId, stage.EligibleAccountCount, stage.SuccessfulResultCount }), ct);
            await ownership.RegisterAsync("BillingSnapshot", period.Key, billing.Id, SampleDataCatalog.Hash(new { billing.Id, billing.BillingStageRunId, billing.RevisionNumber, billing.Version, billing.LogicalScopeKey, billing.ProcessingCutoffReference, billing.ResultCount, billing.CurrentDueTotal, billing.ArrearsTotal, billing.OdiTotal, billing.OtherChargesTotal, billing.IsPublished }), ct);
            await ownership.RegisterAsync("DemandSnapshot", period.Key, demand.Id, SampleDataCatalog.Hash(new { demand.Id, demand.DemandRunId, demand.RevisionNumber, demand.BillingSnapshotId, demand.Version, demand.RowCount, demand.BatchCount, demand.TotalDemand, demand.IsPublished }), ct);
            var audit = new DemandAuditEntry { Id = SampleDataCatalog.StableId("DemandAudit", period.Key), DemandRunId = run.Id, RevisionNumber = 1, Scope = run.LogicalScopeKey, ActorId = "sample-seeder", Source = DemandSource.Automizer, CorrelationId = run.CorrelationId, NewStatus = DemandRunStatus.Completed.ToString(), OccurredAtUtc = SampleDataCatalog.SeedTimestampUtc, OutcomeCode = "DEMAND_COMPLETED", SafeReferences = $"billingSnapshot={billing.Id:N};demandSnapshot={demand.Id:N}" }; db.DemandAuditEntries.Update(audit); await ownership.RegisterAsync("DemandAudit", period.Key, audit.Id, SampleDataCatalog.Hash(new { audit.Id, audit.DemandRunId, audit.RevisionNumber, audit.Scope, audit.NewStatus, audit.OutcomeCode }), ct);
        }
    }

    private async Task<DemandRun> UpsertRunAsync(DemandRun run, SamplePeriod period, Dictionary<string, Location> locations, CancellationToken ct)
    {
        var existing = await db.DemandRuns.SingleOrDefaultAsync(x => x.Id == run.Id, ct); if (existing is not null) { await RequireExistingAsync("DemandRun", period.Key, run.Id, existing.Id, ct); db.Entry(existing).CurrentValues.SetValues(run); run = existing; } else db.DemandRuns.Add(run);
        foreach (var (branch, index) in SampleDataCatalog.Branches.Select((x, i) => (x, i))) { var id = SampleDataCatalog.StableId("DemandRunBranchScope", $"{period.Key}:{branch.Code}"); var scope = await db.DemandRunBranchScopes.SingleOrDefaultAsync(x => x.Id == id, ct); if (scope is not null) await RequireExistingAsync("DemandRunBranchScope", $"{period.Key}:{branch.Code}", id, scope.Id, ct); else scope = new DemandRunBranchScope { Id = id }; scope.DemandRunId = run.Id; scope.RevisionNumber = 1; scope.BranchId = locations[branch.Code].Id; scope.SortOrder = index; if (db.Entry(scope).State == EntityState.Detached) db.DemandRunBranchScopes.Add(scope); await ownership.RegisterAsync("DemandRunBranchScope", $"{period.Key}:{branch.Code}", id, SampleDataCatalog.Hash(new { scope.Id, scope.DemandRunId, scope.RevisionNumber, scope.BranchId, scope.SortOrder }), ct); }
        return run;
    }

    private async Task ValidateDatabaseAsync(Guid lobId, Dictionary<string, Location> locations, Dictionary<string, LoanAccount> loans, CancellationToken ct)
    {
        if (locations.Count != 1 + SampleDataCatalog.Locations.Count + SampleDataCatalog.Branches.Count || loans.Count != 7) throw new SampleDataValidationException("Source fixture count validation failed.");
        foreach (var period in SampleDataCatalog.HistoryPeriods) { var run = await db.DemandRuns.SingleAsync(x => x.Id == SampleDataCatalog.StableId("DemandRun", period.Key), ct); if (run.BranchScopes.Count != 8 || run.RowCount != 7 || run.BatchCount != 8 || run.DemandSnapshotId is null || run.BillingSnapshotId is null) { await db.Entry(run).Collection(x => x.BranchScopes).LoadAsync(ct); if (run.BranchScopes.Count != 8) throw new SampleDataValidationException($"History branch scope validation failed for {period.Key}."); } }
    }

    private async Task RequireExistingAsync(string entityType, string logicalKey, Guid expectedId, Guid actualId, CancellationToken ct)
    {
        if (expectedId != actualId) { await ownership.RequireAsync(entityType, logicalKey, actualId, ct); return; }
        await ownership.RequireAsync(entityType, logicalKey, actualId, ct);
    }

    private async Task<string> FingerprintAsync(CancellationToken ct)
    {
        var values = await db.LookupValues.AsNoTracking().Where(x => x.Module == SampleDataCatalog.OwnershipModule && x.LookupType == SampleDataCatalog.OwnershipType && x.Code != $"Manifest:{SampleDataCatalog.DatasetKey}").OrderBy(x => x.Code).Select(x => x.Code + "=" + x.Value).ToListAsync(ct);
        return SampleDataCatalog.Hash(string.Join("\n", values));
    }
}
