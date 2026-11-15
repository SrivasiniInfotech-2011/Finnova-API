using Finnova.Models.Contracts.LoanAccounts;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Context;
using Finnova.Repository.Repositories;
using Finnova.Service.Applications.Commands;
using Finnova.Service.Applications.Financial;
using Finnova.Service.Applications.Ports;
using Finnova.Service.LoanAccounts;
using Finnova.Service.LoanAccounts.Commands;
using Finnova.Service.LoanAccounts.Queries;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using static Finnova.Tests.LoanAccounts.LoanAccountTestData;

namespace Finnova.Tests.LoanAccounts;

public sealed class LoanAccountHandlerTests
{
    private static readonly IOptions<AccountCreationKycOptions> Opts = Options.Create(new AccountCreationKycOptions());

    private static UpdateLoanAccountCommandHandler UpdateHandler(FinnovaDbContext db) =>
        new(new LoanAccountRepository(db), new AccountAuditRepository(db), new ApplicationUnitOfWork(db), Opts);
    private static SubmitKycResolutionCommandHandler SubmitHandler(FinnovaDbContext db) =>
        new(new LoanAccountRepository(db), new AccountKycResolutionRepository(db), new AccountAuditRepository(db), new ApplicationUnitOfWork(db));
    private static DecideKycResolutionCommandHandler DecideHandler(FinnovaDbContext db) =>
        new(new LoanAccountRepository(db), new AccountKycResolutionRepository(db), new AccountKycHistoryRepository(db),
            new AccountAuditRepository(db), new ApplicationUnitOfWork(db), Opts);
    private static GetLoanAccountsQueryHandler ListHandler(FinnovaDbContext db) => new(new LoanAccountRepository(db));
    private static LoanAccountSearchRequest Search(string? term = null, int page = 1, int size = 20,
        KycStatus? kyc = null, bool? flagged = null, AccountStatus? status = null, bool? pending = null) =>
        new(term, kyc, flagged, status, null, null, null, null, pending, page, size);

    // ------------------------------------------------------------------ update (Property 9)
    [Fact]
    public async Task Update_changes_name_and_refreshes_UpdatedAt_and_writes_audit()
    {
        using var db = NewDb();
        var acc = SeedLoan(db, "APP-1", "LN-1", "C-1", "Asha Rao");
        var before = acc.UpdatedAt;
        var result = await UpdateHandler(db).Handle(new UpdateLoanAccountCommand(acc.Id, "New Name", null, RowVersionOf(db, acc.Id), "u1", "c1"), default);
        Assert.Equal("New Name", result.AccountName);
        Assert.True(db.LoanAccounts.AsNoTracking().First(a => a.Id == acc.Id).UpdatedAt > before);
        Assert.Single(db.AccountAuditEntries);
    }

    [Fact]
    public async Task Update_with_equal_values_is_a_noop()
    {
        using var db = NewDb();
        var acc = SeedLoan(db, "APP-1", "LN-1", "C-1", "Asha Rao");
        var before = acc.UpdatedAt;
        await UpdateHandler(db).Handle(new UpdateLoanAccountCommand(acc.Id, acc.AccountName, "Active", RowVersionOf(db, acc.Id), "u1", "c1"), default);
        Assert.Equal(before, db.LoanAccounts.AsNoTracking().First(a => a.Id == acc.Id).UpdatedAt);
        Assert.Empty(db.AccountAuditEntries);
    }

    [Fact]
    public async Task Update_never_changes_immutable_fields()
    {
        using var db = NewDb();
        var acc = SeedLoan(db, "APP-1", "LN-1", "C-1", "Asha Rao");
        await UpdateHandler(db).Handle(new UpdateLoanAccountCommand(acc.Id, "Other", "Frozen", RowVersionOf(db, acc.Id), "u1", "c1"), default);
        var stored = db.LoanAccounts.AsNoTracking().First(a => a.Id == acc.Id);
        Assert.Equal("LN-1", stored.AccountNumber);
        Assert.Equal(AccountType.Loan, stored.Type);
        Assert.Equal(acc.ApplicationId, stored.ApplicationId);
        Assert.Equal(0m, stored.Balance);
        Assert.Equal("INR", stored.Currency);
    }

    [Fact]
    public async Task Update_rejects_closed_account_and_forbidden_transition_and_stale_row_version()
    {
        using var db = NewDb();
        var closed = SeedLoan(db, "APP-1", "LN-1", "C-1", "A");
        closed.Status = AccountStatus.Closed; db.SaveChanges();
        await Assert.ThrowsAsync<LoanAccountConflictException>(() =>
            UpdateHandler(db).Handle(new UpdateLoanAccountCommand(closed.Id, "X", null, RowVersionOf(db, closed.Id), "u", "c"), default));

        var active = SeedLoan(db, "APP-2", "LN-2", "C-2", "B");
        await Assert.ThrowsAsync<LoanAccountConflictException>(() =>
            UpdateHandler(db).Handle(new UpdateLoanAccountCommand(active.Id, null, "Closed", RowVersionOf(db, active.Id), "u", "c"), default));
        await Assert.ThrowsAsync<LoanAccountConflictException>(() =>
            UpdateHandler(db).Handle(new UpdateLoanAccountCommand(active.Id, "X", null, Convert.ToBase64String(new byte[] { 1, 2, 3 }), "u", "c"), default));
        Assert.Equal(AccountStatus.Active, db.LoanAccounts.AsNoTracking().First(a => a.Id == active.Id).Status);
    }

    [Fact]
    public async Task Unknown_account_is_not_found()
        => await Assert.ThrowsAsync<LoanAccountNotFoundException>(() =>
            new GetLoanAccountByIdQueryHandler(new LoanAccountRepository(NewDb()), Opts).Handle(new GetLoanAccountByIdQuery(Guid.NewGuid()), default));

    // ------------------------------------------------------------------ list (Properties 10 and 11)
    [Fact]
    public async Task List_searches_case_insensitively_and_combines_filters()
    {
        using var db = NewDb();
        SeedLoan(db, "APP-100", "LN-AAA", "CUST-1", "Asha Rao", flagged: true, kyc: KycStatus.Failed);
        SeedLoan(db, "APP-200", "LN-BBB", "CUST-2", "Vikram Shah", flagged: false, kyc: KycStatus.Passed);
        var h = ListHandler(db);

        Assert.Single((await h.Handle(new GetLoanAccountsQuery(Search("app-100")), default)).Data);
        Assert.Single((await h.Handle(new GetLoanAccountsQuery(Search("ln-bbb")), default)).Data);
        Assert.Single((await h.Handle(new GetLoanAccountsQuery(Search("vikram")), default)).Data);
        Assert.Single((await h.Handle(new GetLoanAccountsQuery(Search("cust-1", flagged: true)), default)).Data);
        Assert.Empty((await h.Handle(new GetLoanAccountsQuery(Search("cust-1", flagged: false)), default)).Data);
        Assert.Single((await h.Handle(new GetLoanAccountsQuery(Search(kyc: KycStatus.Passed)), default)).Data);
    }

    [Property(MaxTest = 100)] // Feature: application-account-creation, Property 11: Pagination and ordering
    public bool PaginationOrdersByCreatedDescendingThenAccountNumber(byte count, byte pageSeed, byte sizeSeed)
    {
        var n = count % 12; var page = pageSeed % 5 + 1; var size = sizeSeed % 5 + 1;
        using var db = NewDb();
        var baseTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < n; i++)
            SeedLoan(db, $"APP-{i:D3}", $"LN-{i:D3}", $"C-{i:D3}", $"Name {i}", createdAt: baseTime.AddMinutes(i % 4));   // ties exercise the second key

        var result = ListHandler(db).Handle(new GetLoanAccountsQuery(Search(page: page, size: size)), default).GetAwaiter().GetResult();
        var expected = Enumerable.Range(0, n).Select(i => (Created: baseTime.AddMinutes(i % 4), Number: $"LN-{i:D3}"))
            .OrderByDescending(x => x.Created).ThenBy(x => x.Number, StringComparer.Ordinal)
            .Skip((page - 1) * size).Take(size).Select(x => x.Number).ToList();
        return result.Data.Select(d => d.AccountNumber).SequenceEqual(expected)
            && result.Total == n && result.Page == page && result.PageSize == size && result.Data.Count <= size;
    }

    [Property(MaxTest = 100)] // Feature: application-account-creation, Property 10: List search and filters
    public bool SearchReturnsExactlyTheMatchingAccounts(string? term)
    {
        // ASCII only: SQL Server collation and .NET invariant casing differ for exotic Unicode, which is not what this property tests.
        var t = new string((term ?? string.Empty).Where(ch => ch < 128 && !char.IsControl(ch)).ToArray()).Trim();
        if (t.Length > 100) t = t[..100].Trim();
        using var db = NewDb();
        var seeded = new[]
        {
            SeedLoan(db, "APP-ONE", "LN-ONE", "CUST-A", "Asha Rao"),
            SeedLoan(db, "APP-TWO", "LN-TWO", "CUST-B", "Vikram Shah"),
            SeedLoan(db, "APP-3", "LN-3", "CUST-C", "Priya Nair")
        };
        var result = ListHandler(db).Handle(new GetLoanAccountsQuery(Search(t)), default).GetAwaiter().GetResult();
        var apps = db.Applications.AsNoTracking().ToDictionary(a => a.Id, a => a.ApplicationNumber);
        var customers = db.Customers.AsNoTracking().ToDictionary(c => c.Id, c => c.CustomerCode);
        var expected = seeded.Where(a => t.Length == 0
            || apps[a.ApplicationId!.Value].Contains(t, StringComparison.OrdinalIgnoreCase)
            || a.AccountNumber.Contains(t, StringComparison.OrdinalIgnoreCase)
            || customers[a.CustomerId!.Value].Contains(t, StringComparison.OrdinalIgnoreCase)
            || (a.CustomerNameSnapshot ?? "").Contains(t, StringComparison.OrdinalIgnoreCase))
            .Select(a => a.AccountNumber).OrderBy(x => x).ToList();
        return result.Data.Select(d => d.AccountNumber).OrderBy(x => x).SequenceEqual(expected);
    }

    // ------------------------------------------------------------------ maker-checker
    [Fact]
    public async Task Maker_checker_flow_clears_the_flag_only_on_approval_by_another_user()
    {
        using var db = NewDb();
        var acc = SeedLoan(db, "APP-1", "LN-1", "C-1", "Asha Rao");
        var maker = Guid.NewGuid(); var checker = Guid.NewGuid();

        var created = await SubmitHandler(db).Handle(new SubmitKycResolutionCommand(acc.Id, "Verified originals at the branch", maker, "Maker One", "c"), default);
        Assert.Equal("Pending", created.Status);
        var stored = db.LoanAccounts.AsNoTracking().First(a => a.Id == acc.Id);
        Assert.True(stored.KycFlagged);                       // unchanged by the maker step (R9.1)
        Assert.Equal(KycStatus.Failed, stored.KycStatus);

        await Assert.ThrowsAsync<LoanAccountConflictException>(() =>        // second pending request refused (R9.9)
            SubmitHandler(db).Handle(new SubmitKycResolutionCommand(acc.Id, "Another valid reason here", maker, "Maker One", "c"), default));
        await Assert.ThrowsAsync<KycResolutionForbiddenException>(() =>     // four-eyes (R9.4)
            DecideHandler(db).Handle(new DecideKycResolutionCommand(acc.Id, created.Id, true, "ok", maker, "Maker One", "c"), default));

        var approved = await DecideHandler(db).Handle(new DecideKycResolutionCommand(acc.Id, created.Id, true, "Documents match", checker, "Checker One", "c"), default);
        Assert.Equal("Passed", approved.KycStatus);
        Assert.False(approved.KycFlagged);
        Assert.Equal("Manual", approved.KycSource);
        Assert.Null(approved.PendingKycResolution);
        Assert.Single(approved.KycHistory);
        Assert.Equal("Approved", Assert.Single(approved.KycResolutionRequests).Status);

        await Assert.ThrowsAsync<LoanAccountConflictException>(() =>
            DecideHandler(db).Handle(new DecideKycResolutionCommand(acc.Id, created.Id, false, "again", checker, "Checker One", "c"), default));
    }

    [Fact]
    public async Task Rejection_keeps_the_flag_and_allows_a_new_request()
    {
        using var db = NewDb();
        var acc = SeedLoan(db, "APP-1", "LN-1", "C-1", "Asha Rao");
        var created = await SubmitHandler(db).Handle(new SubmitKycResolutionCommand(acc.Id, "Verified originals at the branch", Guid.NewGuid(), "M", "c"), default);
        var view = await DecideHandler(db).Handle(new DecideKycResolutionCommand(acc.Id, created.Id, false, "Not convincing", Guid.NewGuid(), "C", "c"), default);

        Assert.True(view.KycFlagged);
        Assert.Equal("Failed", view.KycStatus);
        Assert.Equal("Rejected", Assert.Single(view.KycResolutionRequests).Status);
        await SubmitHandler(db).Handle(new SubmitKycResolutionCommand(acc.Id, "Second attempt with documents", Guid.NewGuid(), "M", "c"), default);
    }

    [Fact]
    public async Task Submission_is_refused_for_unflagged_accounts_and_short_reasons()
    {
        using var db = NewDb();
        var clean = SeedLoan(db, "APP-1", "LN-1", "C-1", "A", flagged: false, kyc: KycStatus.Passed);
        await Assert.ThrowsAsync<LoanAccountConflictException>(() =>
            SubmitHandler(db).Handle(new SubmitKycResolutionCommand(clean.Id, "A perfectly valid reason", Guid.NewGuid(), "M", "c"), default));
        var flagged = SeedLoan(db, "APP-2", "LN-2", "C-2", "B");
        await Assert.ThrowsAsync<LoanAccountValidationException>(() =>
            SubmitHandler(db).Handle(new SubmitKycResolutionCommand(flagged.Id, "short", Guid.NewGuid(), "M", "c"), default));
    }

    [Fact]
    public async Task Recheck_rearms_the_kyc_job_for_flagged_accounts_only()
    {
        using var db = NewDb();
        var flagged = SeedLoan(db, "APP-1", "LN-1", "C-1", "A");
        var handler = new RequestKycRecheckCommandHandler(new LoanAccountRepository(db), new AccountKycJobRepository(db), new AccountAuditRepository(db), new ApplicationUnitOfWork(db));
        await handler.Handle(new RequestKycRecheckCommand(flagged.Id, "documents updated", "u1", "c"), default);
        var job = Assert.Single(db.AccountKycJobs);
        Assert.Equal(AccountRequestStatus.Pending, job.Status);

        var clean = SeedLoan(db, "APP-2", "LN-2", "C-2", "B", flagged: false, kyc: KycStatus.Passed);
        await Assert.ThrowsAsync<LoanAccountConflictException>(() => handler.Handle(new RequestKycRecheckCommand(clean.Id, null, "u1", "c"), default));
    }

    [Fact]
    public async Task Reprocess_resets_only_dead_lettered_requests()
    {
        using var db = NewDb();
        var dead = new AccountCreationRequest { ApplicationId = Guid.NewGuid(), ApplicationNumber = "A1", CorrelationId = "c", Status = AccountRequestStatus.DeadLetter, AttemptCount = 3 };
        var pending = new AccountCreationRequest { ApplicationId = Guid.NewGuid(), ApplicationNumber = "A2", CorrelationId = "c", Status = AccountRequestStatus.Pending };
        db.AccountCreationRequests.AddRange(dead, pending); db.SaveChanges();
        var handler = new ReprocessAccountRequestCommandHandler(new AccountCreationRequestRepository(db), new AccountAuditRepository(db), new ApplicationUnitOfWork(db));

        await handler.Handle(new ReprocessAccountRequestCommand(dead.Id, "scheme created", "ops", "c"), default);
        Assert.Equal(AccountRequestStatus.Pending, db.AccountCreationRequests.AsNoTracking().First(x => x.Id == dead.Id).Status);
        Assert.Equal(0, db.AccountCreationRequests.AsNoTracking().First(x => x.Id == dead.Id).AttemptCount);
        await Assert.ThrowsAsync<LoanAccountConflictException>(() =>
            handler.Handle(new ReprocessAccountRequestCommand(pending.Id, "x", "ops", "c"), default));
    }

    // ------------------------------------------------------------------ outbox (R1)
    [Fact]
    public async Task Processing_an_application_inserts_exactly_one_account_creation_request()
    {
        using var db = NewDb();
        var app = new Application
        {
            ApplicationNumber = "APP-9",
            CustomerId = Guid.NewGuid(),
            LineOfBusinessId = Guid.NewGuid(),
            BranchId = Guid.NewGuid(),
            ProductCode = "HL",
            SchemeCode = "S1",
            FinanceAmount = 100000m,
            IrrPercent = 12m,
            Tenure = 12,
            RepaymentFrequency = RepaymentFrequency.Monthly,
            DisbursementDate = new DateOnly(2025, 1, 31),
            Status = ApplicationStatus.Submitted
        };
        db.Applications.Add(app); db.SaveChanges();
        var handler = new ProcessApplicationCommandHandler(
            new ApplicationRepository(db), new ApplicationScheduleRepository(db), new ApplicationStatusAuditRepository(db),
            new ApplicationUnitOfWork(db), new Mock<IApplicationReferenceValidator>().Object, new AmortizationCalculator(),
            new AccountCreationRequestRepository(db));
        var user = Guid.NewGuid();

        await handler.Handle(new ProcessApplicationCommand(app.Id, user.ToString()), default);
        var request = Assert.Single(db.AccountCreationRequests);
        Assert.Equal(app.Id, request.ApplicationId);
        Assert.Equal("APP-9", request.ApplicationNumber);
        Assert.Equal(AccountRequestStatus.Pending, request.Status);
        Assert.Equal(0, request.AttemptCount);
        Assert.Equal(user, request.InitiatedByUserId);

        await Assert.ThrowsAsync<ApplicationAlreadyProcessedException>(() => handler.Handle(new ProcessApplicationCommand(app.Id, user.ToString()), default));
        Assert.Single(db.AccountCreationRequests);
    }

    [Fact]
    public void Account_creation_request_has_a_unique_application_index_and_pending_resolution_index_is_filtered()
    {
        using var db = NewDb();
        var request = db.Model.FindEntityType(typeof(AccountCreationRequest))!;
        Assert.Contains(request.GetIndexes(), i => i.IsUnique && i.Properties.Count == 1 && i.Properties[0].Name == nameof(AccountCreationRequest.ApplicationId));
        var resolution = db.Model.FindEntityType(typeof(AccountKycResolutionRequest))!;
        Assert.Contains(resolution.GetIndexes(), i => i.IsUnique && i.GetFilter() == "[Status] = 'Pending'");
    }
}
