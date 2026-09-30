using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Service.Entity.Commands.CreateEntity;
using Finnova.Service.Entity.Commands.SetEntityActive;
using Finnova.Service.Entity.Commands.UpdateEntity;
using Finnova.Service.Entity.Queries.GetEntityById;
using Finnova.Service.Entity.Queries.GetEntityAuditTrail;
using Finnova.Tests.Infrastructure;
using Xunit;

namespace Finnova.Tests.Unit;

public class EntityHandlerTests
{
    private const string Admin = "admin-1";

    private static (InMemoryEntityRepository repo, InMemoryEntityAuditRepository audit) NewStores()
        => (new InMemoryEntityRepository(), new InMemoryEntityAuditRepository());

    private static CreateEntityCommand ValidCreate(
        string code = "DLR001", string name = "Acme Motors", EntityType type = EntityType.Dealer,
        IReadOnlyDictionary<string, string>? attributes = null, bool? active = null)
        => new(code, name, type, "22AAAAA0000A1Z5", "Priya Sharma", "priya@acme.example",
            "9876543210", "12 MG Road, Bengaluru", attributes, active, Admin);

    [Fact]
    public async Task Create_PersistsWithDefaultActiveAndWritesCreateAudit()
    {
        var (repo, audit) = NewStores();
        var handler = new CreateEntityCommandHandler(repo, audit);

        var result = await handler.Handle(ValidCreate(), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("DLR001", result.Code);
        Assert.True(result.IsActive);
        Assert.Equal(EntityType.Dealer, result.EntityType);
        var entry = Assert.Single(audit.Snapshot());
        Assert.Equal("Create", entry.Action.ToString());
    }

    [Fact]
    public async Task Create_InapplicableAttribute_Throws_AndWritesNoRecord()
    {
        var (repo, audit) = NewStores();
        var handler = new CreateEntityCommandHandler(repo, audit);

        // gstin is a Supplier key, not applicable to a Dealer.
        var attrs = new Dictionary<string, string> { ["gstin"] = "22AAAAA0000A1Z5" };
        await Assert.ThrowsAsync<EntityInvalidAttributesException>(() =>
            handler.Handle(ValidCreate(type: EntityType.Dealer, attributes: attrs), CancellationToken.None));

        Assert.Empty(repo.Snapshot());
        Assert.Empty(audit.Snapshot());
    }

    [Fact]
    public async Task Create_ApplicableAttribute_Persisted()
    {
        var (repo, audit) = NewStores();
        var handler = new CreateEntityCommandHandler(repo, audit);

        var attrs = new Dictionary<string, string> { ["gstin"] = "27AAAAA0000A1Z5" };
        var result = await handler.Handle(
            ValidCreate(code: "SUP001", name: "Global Supply", type: EntityType.Supplier, attributes: attrs),
            CancellationToken.None);

        Assert.Equal("27AAAAA0000A1Z5", result.Attributes["gstin"]);
    }

    [Fact]
    public async Task Create_DuplicateCode_SameType_Throws_AndWritesNoAudit()
    {
        var (repo, audit) = NewStores();
        var handler = new CreateEntityCommandHandler(repo, audit);
        await handler.Handle(ValidCreate(code: "DLR001"), CancellationToken.None);

        await Assert.ThrowsAsync<EntityDuplicateCodeException>(() =>
            handler.Handle(ValidCreate(code: "dlr001", name: "Dup"), CancellationToken.None));
        Assert.Single(audit.Snapshot());
    }

    [Fact]
    public async Task Create_SameCode_DifferentTypes_BothPersist()
    {
        var (repo, audit) = NewStores();
        var handler = new CreateEntityCommandHandler(repo, audit);

        await handler.Handle(ValidCreate(code: "X01", type: EntityType.Dealer), CancellationToken.None);
        await handler.Handle(ValidCreate(code: "X01", name: "Supplier X", type: EntityType.Supplier), CancellationToken.None);

        Assert.Equal(2, repo.Count);
    }

    [Fact]
    public async Task Update_NoOp_ReturnsUnchangedAndWritesNoAudit()
    {
        var (repo, audit) = NewStores();
        var created = await new CreateEntityCommandHandler(repo, audit).Handle(ValidCreate(), CancellationToken.None);
        var countAfterCreate = audit.Count;

        var update = new UpdateEntityCommandHandler(repo, audit);
        await update.Handle(new UpdateEntityCommand(
            created.Id, created.Name, created.RegistrationIdentifier, created.ContactPerson, created.Email,
            created.Phone, created.AddressLine, created.Attributes, created.IsActive, Admin),
            CancellationToken.None);

        Assert.Equal(countAfterCreate, audit.Count);
    }

    [Fact]
    public async Task Update_Changes_PersistsAndWritesUpdateAudit()
    {
        var (repo, audit) = NewStores();
        var created = await new CreateEntityCommandHandler(repo, audit).Handle(ValidCreate(), CancellationToken.None);

        var update = new UpdateEntityCommandHandler(repo, audit);
        var result = await update.Handle(new UpdateEntityCommand(
            created.Id, "Acme Motors Pvt Ltd", created.RegistrationIdentifier, "Ravi Kumar", created.Email,
            created.Phone, created.AddressLine, null, true, Admin), CancellationToken.None);

        Assert.Equal("Acme Motors Pvt Ltd", result.Name);
        Assert.Equal(2, audit.Count);
        Assert.Contains(audit.Snapshot(), a => a.Action.ToString() == "Update");
    }

    [Fact]
    public async Task Update_UnknownId_Throws()
    {
        var (repo, audit) = NewStores();
        var update = new UpdateEntityCommandHandler(repo, audit);
        await Assert.ThrowsAsync<EntityNotFoundException>(() => update.Handle(
            new UpdateEntityCommand(Guid.NewGuid(), "X", null, null, null, null, null, null, true, Admin),
            CancellationToken.None));
    }

    [Fact]
    public async Task Deactivate_TogglesAndAudits()
    {
        var (repo, audit) = NewStores();
        var created = await new CreateEntityCommandHandler(repo, audit).Handle(ValidCreate(), CancellationToken.None);

        var setActive = new SetEntityActiveCommandHandler(repo, audit);
        var deactivated = await setActive.Handle(new SetEntityActiveCommand(created.Id, false, Admin), CancellationToken.None);

        Assert.False(deactivated.IsActive);
        Assert.Equal(2, audit.Count);
    }

    [Fact]
    public async Task SetActive_NoOp_WritesNoAudit()
    {
        var (repo, audit) = NewStores();
        var created = await new CreateEntityCommandHandler(repo, audit).Handle(ValidCreate(), CancellationToken.None);

        var setActive = new SetEntityActiveCommandHandler(repo, audit);
        await setActive.Handle(new SetEntityActiveCommand(created.Id, true, Admin), CancellationToken.None); // already active
        Assert.Equal(1, audit.Count);
    }

    [Fact]
    public async Task GetById_UnknownId_Throws()
    {
        var (repo, _) = NewStores();
        var query = new GetEntityByIdQueryHandler(repo);
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            query.Handle(new GetEntityByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task GetAuditTrail_UnknownId_ReturnsEmpty()
    {
        var (_, audit) = NewStores();
        var query = new GetEntityAuditTrailQueryHandler(audit);
        var result = await query.Handle(new GetEntityAuditTrailQuery(Guid.NewGuid()), CancellationToken.None);
        Assert.Empty(result);
    }
}