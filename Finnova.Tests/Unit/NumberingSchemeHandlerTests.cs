using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Service.DocumentNumberControl.Commands.CreateNumberingScheme;
using Finnova.Service.DocumentNumberControl.Commands.IssueNumber;
using Finnova.Service.DocumentNumberControl.Commands.SetSchemeActive;
using Finnova.Service.DocumentNumberControl.Commands.UpdateNumberingScheme;
using Finnova.Service.DocumentNumberControl.Queries.GetNumberingSchemeById;
using Finnova.Service.DocumentNumberControl.Queries.GetSchemeAuditTrail;
using Finnova.Tests.Infrastructure;
using Xunit;

namespace Finnova.Tests.Unit;

/// <summary>
/// Example/edge unit tests for the DCN command/query handlers over the in-memory repositories
/// (FINNOVA-7 R1-R7). Complements the pure-helper tests in <see cref="NumberFormatterTests"/>.
/// </summary>
public class NumberingSchemeHandlerTests
{
    private const string Admin = "admin-1";

    private static (InMemoryNumberingSchemeRepository repo, InMemoryNumberingSchemeAuditRepository audit) NewStores()
        => (new InMemoryNumberingSchemeRepository(), new InMemoryNumberingSchemeAuditRepository());

    private static CreateNumberingSchemeCommand ValidCreate(
        string code = "INV", string docType = "Invoice", string template = "INV-{SEQ:5}",
        NumberResetRule reset = NumberResetRule.Never, NumberScope? scope = null, Guid? scopeId = null,
        long? start = null, int? increment = null, int? padding = null, bool? active = null)
        => new(code, "Display " + code, docType, template, null, null, start, increment, padding, reset, scope, scopeId, active, Admin);

    // ---- Create ----

    [Fact]
    public async Task Create_PersistsWithResolvedDefaultsAndWritesCreateAudit()
    {
        var (repo, audit) = NewStores();
        var handler = new CreateNumberingSchemeCommandHandler(repo, audit);

        var result = await handler.Handle(ValidCreate(), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("INV", result.Code);
        Assert.Equal(1, result.SeqStart);        // default
        Assert.Equal(1, result.SeqIncrement);    // default
        Assert.Equal(1, result.SeqPadding);      // default
        Assert.Equal(NumberScope.Global, result.Scope);  // default
        Assert.True(result.IsActive);            // default
        Assert.Equal(0, result.CurrentValue);
        var entry = Assert.Single(audit.Snapshot());
        Assert.Equal("Create", entry.Action.ToString());
    }

    [Fact]
    public async Task Create_DuplicateCode_Throws()
    {
        var (repo, audit) = NewStores();
        var handler = new CreateNumberingSchemeCommandHandler(repo, audit);
        await handler.Handle(ValidCreate(code: "INV", docType: "Invoice"), CancellationToken.None);

        await Assert.ThrowsAsync<NumberingSchemeDuplicateCodeException>(() =>
            handler.Handle(ValidCreate(code: "inv", docType: "Other"), CancellationToken.None));
        Assert.Single(audit.Snapshot());   // no audit for the rejected create
    }

    [Fact]
    public async Task Create_ScopeConflict_Throws()
    {
        var (repo, audit) = NewStores();
        var handler = new CreateNumberingSchemeCommandHandler(repo, audit);
        await handler.Handle(ValidCreate(code: "A", docType: "Invoice"), CancellationToken.None);

        await Assert.ThrowsAsync<NumberingSchemeScopeConflictException>(() =>
            handler.Handle(ValidCreate(code: "B", docType: "Invoice"), CancellationToken.None));
    }

    [Fact]
    public async Task Create_InvalidTemplate_ThrowsValidation()
    {
        var (repo, audit) = NewStores();
        var handler = new CreateNumberingSchemeCommandHandler(repo, audit);

        await Assert.ThrowsAsync<NumberingSchemeValidationException>(() =>
            handler.Handle(ValidCreate(template: "INV-0001"), CancellationToken.None)); // no SEQ token
    }

    [Theory]
    [InlineData(NumberScope.Global, true)]     // Global + scopeId -> invalid
    [InlineData(NumberScope.Branch, false)]    // Branch without scopeId -> invalid
    public async Task Create_ScopeIdConsistency_ThrowsValidation(NumberScope scope, bool provideScopeId)
    {
        var (repo, audit) = NewStores();
        var handler = new CreateNumberingSchemeCommandHandler(repo, audit);
        var scopeId = provideScopeId ? Guid.NewGuid() : (Guid?)null;

        await Assert.ThrowsAsync<NumberingSchemeValidationException>(() =>
            handler.Handle(ValidCreate(scope: scope, scopeId: scopeId), CancellationToken.None));
    }

    // ---- Update ----

    [Fact]
    public async Task Update_NoOp_ReturnsUnchangedAndWritesNoAudit()
    {
        var (repo, audit) = NewStores();
        var create = new CreateNumberingSchemeCommandHandler(repo, audit);
        var created = await create.Handle(ValidCreate(), CancellationToken.None);
        var auditCountAfterCreate = audit.Count;

        var update = new UpdateNumberingSchemeCommandHandler(repo, audit);
        var cmd = new UpdateNumberingSchemeCommand(
            created.Id, created.Name, created.FormatTemplate, created.Prefix, created.Suffix,
            created.SeqIncrement, created.SeqPadding, created.ResetRule, created.IsActive, Admin);

        await update.Handle(cmd, CancellationToken.None);
        Assert.Equal(auditCountAfterCreate, audit.Count);   // no new audit
    }

    [Fact]
    public async Task Update_Changes_PersistsAndWritesUpdateAudit()
    {
        var (repo, audit) = NewStores();
        var create = new CreateNumberingSchemeCommandHandler(repo, audit);
        var created = await create.Handle(ValidCreate(), CancellationToken.None);

        var update = new UpdateNumberingSchemeCommandHandler(repo, audit);
        var cmd = new UpdateNumberingSchemeCommand(
            created.Id, "Renamed", "INV-{SEQ:6}", null, null, 2, 6, NumberResetRule.Monthly, true, Admin);

        var result = await update.Handle(cmd, CancellationToken.None);
        Assert.Equal("Renamed", result.Name);
        Assert.Equal(6, result.SeqPadding);
        Assert.Equal(NumberResetRule.Monthly, result.ResetRule);
        Assert.Equal(2, audit.Count);   // Create + Update
        var actions = audit.Snapshot().Select(a => a.Action.ToString()).ToList();
        Assert.Contains("Create", actions);
        Assert.Contains("Update", actions);
        var updateEntry = audit.Snapshot().Single(a => a.Action.ToString() == "Update");
        Assert.Equal("INV-{SEQ:6}", System.Text.Json.JsonDocument.Parse(updateEntry.NewValues).RootElement.GetProperty("FormatTemplate").GetString());
    }

    [Fact]
    public async Task Update_UnknownId_Throws()
    {
        var (repo, audit) = NewStores();
        var update = new UpdateNumberingSchemeCommandHandler(repo, audit);
        var cmd = new UpdateNumberingSchemeCommand(
            Guid.NewGuid(), "X", "{SEQ}", null, null, 1, 1, NumberResetRule.Never, true, Admin);

        await Assert.ThrowsAsync<NumberingSchemeNotFoundException>(() => update.Handle(cmd, CancellationToken.None));
    }

    // ---- Issue ----

    [Fact]
    public async Task Issue_IncrementsWithinPeriodAndFormats()
    {
        var (repo, audit) = NewStores();
        var create = new CreateNumberingSchemeCommandHandler(repo, audit);
        await create.Handle(ValidCreate(template: "INV-{SEQ:5}", reset: NumberResetRule.Never), CancellationToken.None);

        var issue = new IssueNumberCommandHandler(repo);
        var first = await issue.Handle(new IssueNumberCommand("Invoice", NumberScope.Global, null, Admin), CancellationToken.None);
        var second = await issue.Handle(new IssueNumberCommand("Invoice", NumberScope.Global, null, Admin), CancellationToken.None);

        Assert.Equal(1, first.SequenceValue);
        Assert.Equal("INV-00001", first.Number);
        Assert.Equal(2, second.SequenceValue);
        Assert.Equal("INV-00002", second.Number);
        Assert.Equal(1, audit.Count);   // issuance is NOT audited (only the Create)
    }

    [Fact]
    public async Task Issue_UnknownDocumentType_Throws()
    {
        var (repo, _) = NewStores();
        var issue = new IssueNumberCommandHandler(repo);
        await Assert.ThrowsAsync<NumberingSchemeNotFoundException>(() =>
            issue.Handle(new IssueNumberCommand("Nope", NumberScope.Global, null, Admin), CancellationToken.None));
    }

    [Fact]
    public async Task Issue_InactiveScheme_Throws()
    {
        var (repo, audit) = NewStores();
        var create = new CreateNumberingSchemeCommandHandler(repo, audit);
        var created = await create.Handle(ValidCreate(active: false), CancellationToken.None);
        _ = created;

        var issue = new IssueNumberCommandHandler(repo);
        await Assert.ThrowsAsync<NumberingSchemeInactiveException>(() =>
            issue.Handle(new IssueNumberCommand("Invoice", NumberScope.Global, null, Admin), CancellationToken.None));
    }

    [Fact]
    public async Task Issue_Exhaustion_Throws()
    {
        var (repo, audit) = NewStores();
        var create = new CreateNumberingSchemeCommandHandler(repo, audit);
        // padding 1 -> capacity 9; start 9 -> first issue ok (9), second would be 10 -> exhausted.
        await create.Handle(ValidCreate(template: "{SEQ}", start: 9, padding: 1), CancellationToken.None);

        var issue = new IssueNumberCommandHandler(repo);
        var ok = await issue.Handle(new IssueNumberCommand("Invoice", NumberScope.Global, null, Admin), CancellationToken.None);
        Assert.Equal(9, ok.SequenceValue);

        await Assert.ThrowsAsync<NumberSequenceExhaustedException>(() =>
            issue.Handle(new IssueNumberCommand("Invoice", NumberScope.Global, null, Admin), CancellationToken.None));
    }

    // ---- Activate / Deactivate ----

    [Fact]
    public async Task Deactivate_TogglesAndAudits_ThenBlocksIssue()
    {
        var (repo, audit) = NewStores();
        var create = new CreateNumberingSchemeCommandHandler(repo, audit);
        var created = await create.Handle(ValidCreate(), CancellationToken.None);

        var setActive = new SetSchemeActiveCommandHandler(repo, audit);
        var deactivated = await setActive.Handle(new SetSchemeActiveCommand(created.Id, false, Admin), CancellationToken.None);
        Assert.False(deactivated.IsActive);
        Assert.Equal(2, audit.Count);   // Create + Update(toggle)

        var issue = new IssueNumberCommandHandler(repo);
        await Assert.ThrowsAsync<NumberingSchemeInactiveException>(() =>
            issue.Handle(new IssueNumberCommand("Invoice", NumberScope.Global, null, Admin), CancellationToken.None));
    }

    [Fact]
    public async Task SetActive_NoOp_WritesNoAudit()
    {
        var (repo, audit) = NewStores();
        var create = new CreateNumberingSchemeCommandHandler(repo, audit);
        var created = await create.Handle(ValidCreate(), CancellationToken.None);   // active by default

        var setActive = new SetSchemeActiveCommandHandler(repo, audit);
        await setActive.Handle(new SetSchemeActiveCommand(created.Id, true, Admin), CancellationToken.None); // already active
        Assert.Equal(1, audit.Count);   // only the Create
    }

    // ---- Queries ----

    [Fact]
    public async Task GetById_UnknownId_Throws()
    {
        var (repo, _) = NewStores();
        var query = new GetNumberingSchemeByIdQueryHandler(repo);
        await Assert.ThrowsAsync<NumberingSchemeNotFoundException>(() =>
            query.Handle(new GetNumberingSchemeByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task GetAuditTrail_UnknownId_ReturnsEmpty()
    {
        var (_, audit) = NewStores();
        var query = new GetSchemeAuditTrailQueryHandler(audit);
        var result = await query.Handle(new GetSchemeAuditTrailQuery(Guid.NewGuid()), CancellationToken.None);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Issue_ResetsOnPeriodRollover()
    {
        // Uses the repository directly to control the UTC instant across periods (Yearly reset).
        var repo = new InMemoryNumberingSchemeRepository();
        var audit = new InMemoryNumberingSchemeAuditRepository();
        var create = new CreateNumberingSchemeCommandHandler(repo, audit);
        await create.Handle(ValidCreate(template: "{SEQ:3}", reset: NumberResetRule.Yearly), CancellationToken.None);

        var y2026 = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var y2027 = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var a = await repo.IssueNextAsync("Invoice", NumberScope.Global, null, y2026);
        var b = await repo.IssueNextAsync("Invoice", NumberScope.Global, null, y2026);
        var c = await repo.IssueNextAsync("Invoice", NumberScope.Global, null, y2027);

        Assert.Equal(1, a!.Value.SequenceValue);
        Assert.Equal(2, b!.Value.SequenceValue);
        Assert.Equal(1, c!.Value.SequenceValue);   // reset for the new year
    }
}
