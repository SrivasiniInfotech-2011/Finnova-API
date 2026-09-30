using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Service.Court.Commands.CreateCourt;
using Finnova.Service.Court.Commands.SetCourtActive;
using Finnova.Service.Court.Commands.UpdateCourt;
using Finnova.Service.Court.Queries.GetCourtById;
using Finnova.Service.Court.Queries.GetCourtAuditTrail;
using Finnova.Tests.Infrastructure;
using Xunit;

namespace Finnova.Tests.Unit;

public class CourtHandlerTests
{
    private const string Admin = "admin-1";

    private static (InMemoryCourtRepository repo, InMemoryCourtAuditRepository audit) NewStores()
        => (new InMemoryCourtRepository(), new InMemoryCourtAuditRepository());

    private static CreateCourtCommand ValidCreate(
        string code = "DELHC", string name = "Delhi High Court", CourtType type = CourtType.High,
        string jur = "Delhi", string loc = "New Delhi", bool? active = null)
        => new(code, name, type, jur, loc, active, Admin);

    [Fact]
    public async Task Create_PersistsWithDefaultActiveAndWritesCreateAudit()
    {
        var (repo, audit) = NewStores();
        var handler = new CreateCourtCommandHandler(repo, audit);

        var result = await handler.Handle(ValidCreate(), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("DELHC", result.Code);
        Assert.True(result.IsActive);
        Assert.Equal(CourtType.High, result.CourtType);
        var entry = Assert.Single(audit.Snapshot());
        Assert.Equal("Create", entry.Action.ToString());
    }

    [Fact]
    public async Task Create_DuplicateCode_Throws_AndWritesNoAudit()
    {
        var (repo, audit) = NewStores();
        var handler = new CreateCourtCommandHandler(repo, audit);
        await handler.Handle(ValidCreate(code: "DELHC"), CancellationToken.None);

        await Assert.ThrowsAsync<CourtDuplicateCodeException>(() =>
            handler.Handle(ValidCreate(code: "delhc", name: "Dup"), CancellationToken.None));
        Assert.Single(audit.Snapshot());
    }

    [Fact]
    public async Task Update_NoOp_ReturnsUnchangedAndWritesNoAudit()
    {
        var (repo, audit) = NewStores();
        var created = await new CreateCourtCommandHandler(repo, audit).Handle(ValidCreate(), CancellationToken.None);
        var countAfterCreate = audit.Count;

        var update = new UpdateCourtCommandHandler(repo, audit);
        await update.Handle(new UpdateCourtCommand(
            created.Id, created.Name, created.CourtType, created.Jurisdiction, created.Location, created.IsActive, Admin),
            CancellationToken.None);

        Assert.Equal(countAfterCreate, audit.Count);
    }

    [Fact]
    public async Task Update_Changes_PersistsAndWritesUpdateAudit()
    {
        var (repo, audit) = NewStores();
        var created = await new CreateCourtCommandHandler(repo, audit).Handle(ValidCreate(), CancellationToken.None);

        var update = new UpdateCourtCommandHandler(repo, audit);
        var result = await update.Handle(new UpdateCourtCommand(
            created.Id, "Delhi HC", CourtType.High, "Delhi NCT", "New Delhi", true, Admin), CancellationToken.None);

        Assert.Equal("Delhi HC", result.Name);
        Assert.Equal("Delhi NCT", result.Jurisdiction);
        Assert.Equal(2, audit.Count);
        Assert.Contains(audit.Snapshot(), a => a.Action.ToString() == "Update");
    }

    [Fact]
    public async Task Update_UnknownId_Throws()
    {
        var (repo, audit) = NewStores();
        var update = new UpdateCourtCommandHandler(repo, audit);
        await Assert.ThrowsAsync<CourtNotFoundException>(() => update.Handle(
            new UpdateCourtCommand(Guid.NewGuid(), "X", CourtType.Other, "J", "L", true, Admin), CancellationToken.None));
    }

    [Fact]
    public async Task Deactivate_TogglesAndAudits()
    {
        var (repo, audit) = NewStores();
        var created = await new CreateCourtCommandHandler(repo, audit).Handle(ValidCreate(), CancellationToken.None);

        var setActive = new SetCourtActiveCommandHandler(repo, audit);
        var deactivated = await setActive.Handle(new SetCourtActiveCommand(created.Id, false, Admin), CancellationToken.None);

        Assert.False(deactivated.IsActive);
        Assert.Equal(2, audit.Count);
    }

    [Fact]
    public async Task SetActive_NoOp_WritesNoAudit()
    {
        var (repo, audit) = NewStores();
        var created = await new CreateCourtCommandHandler(repo, audit).Handle(ValidCreate(), CancellationToken.None);

        var setActive = new SetCourtActiveCommandHandler(repo, audit);
        await setActive.Handle(new SetCourtActiveCommand(created.Id, true, Admin), CancellationToken.None); // already active
        Assert.Equal(1, audit.Count);
    }

    [Fact]
    public async Task GetById_UnknownId_Throws()
    {
        var (repo, _) = NewStores();
        var query = new GetCourtByIdQueryHandler(repo);
        await Assert.ThrowsAsync<CourtNotFoundException>(() =>
            query.Handle(new GetCourtByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task GetAuditTrail_UnknownId_ReturnsEmpty()
    {
        var (_, audit) = NewStores();
        var query = new GetCourtAuditTrailQueryHandler(audit);
        var result = await query.Handle(new GetCourtAuditTrailQuery(Guid.NewGuid()), CancellationToken.None);
        Assert.Empty(result);
    }
}