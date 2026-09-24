using Moq;
using Xunit;
using Finnova.Models.Contracts.Nationalities;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Nationality.Commands.UpdateNationalityName;
using Finnova.Service.Nationality.Queries.GetNationalityAuditTrail;
using Finnova.Tests.Infrastructure;

namespace Finnova.Tests.Unit;

/// <summary>
/// Not-found, audit-missing-id, and immutability edge tests (task 10.3).
///
/// Covers:
///   - R3.4: updating an unknown id throws <see cref="NationalityNotFoundException"/> and never
///     touches the record store (UpdateAsync is not invoked, no audit entry written).
///   - R4.5: reading the audit trail for a nationality id with no entries returns an empty list
///     without throwing.
///   - R4.6: the audit repository exposes only add + immutable reads; UpdateAsync/DeleteAsync
///     throw <see cref="NotSupportedException"/>.
///   - Code immutability post-create: <see cref="UpdateNationalityNameRequest"/> has no Code
///     member (only Name is editable).
/// </summary>
public class NationalityEdgeTests
{
    // ---- R3.4: update unknown id -> not found, nothing mutated ----

    [Fact]
    public async Task Update_UnknownId_Throws_NotFound_And_Never_Calls_UpdateAsync()
    {
        var repo = new Mock<INationalityRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Nationality?)null);
        var audit = new Mock<INationalityAuditRepository>();

        var handler = new UpdateNationalityNameCommandHandler(repo.Object, audit.Object);
        var cmd = new UpdateNationalityNameCommand(Guid.NewGuid(), "Indian", "admin");

        await Assert.ThrowsAsync<NationalityNotFoundException>(
            () => handler.Handle(cmd, CancellationToken.None));

        // R3.4: nothing changed - no record update, no audit entry.
        repo.Verify(r => r.UpdateAsync(It.IsAny<Nationality>(), It.IsAny<CancellationToken>()), Times.Never);
        audit.Verify(a => a.AddAsync(It.IsAny<NationalityAuditEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- R4.5: audit read for a missing id returns [] with no exception ----

    [Fact]
    public async Task AuditTrail_MissingId_Returns_Empty_List_No_Exception()
    {
        // Store seeded with an unrelated nationality's audit entry.
        var otherId = Guid.NewGuid();
        var auditRepo = new InMemoryNationalityAuditRepository(new[]
        {
            new NationalityAuditEntryBuilder().ForNationality(otherId).Build()
        });

        var handler = new GetNationalityAuditTrailQueryHandler(auditRepo);
        var result = await handler.Handle(
            new GetNationalityAuditTrailQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    // ---- R4.6: audit entries are immutable - add + reads only ----

    [Fact]
    public async Task AuditRepository_UpdateAsync_Throws_NotSupported()
    {
        var repo = new InMemoryNationalityAuditRepository();
        var entry = new NationalityAuditEntryBuilder().Build();

        await Assert.ThrowsAsync<NotSupportedException>(
            () => repo.UpdateAsync(entry, CancellationToken.None));
    }

    [Fact]
    public async Task AuditRepository_DeleteAsync_Throws_NotSupported()
    {
        var repo = new InMemoryNationalityAuditRepository();
        var entry = new NationalityAuditEntryBuilder().Build();

        await Assert.ThrowsAsync<NotSupportedException>(
            () => repo.DeleteAsync(entry, CancellationToken.None));
    }

    [Fact]
    public async Task AuditRepository_Supports_Add_And_Immutable_Read()
    {
        var repo = new InMemoryNationalityAuditRepository();
        var nationalityId = Guid.NewGuid();
        var entry = new NationalityAuditEntryBuilder().ForNationality(nationalityId).Build();

        await repo.AddAsync(entry, CancellationToken.None);

        var read = await repo.GetByNationalityIdAsync(nationalityId, CancellationToken.None);
        Assert.Single(read);
        Assert.Equal(entry.Id, read[0].Id);
    }

    // ---- Code immutability post-create: request record has no Code field ----

    [Fact]
    public void UpdateNationalityNameRequest_Has_No_Code_Field()
    {
        var propertyNames = typeof(UpdateNationalityNameRequest)
            .GetProperties()
            .Select(p => p.Name)
            .ToArray();

        Assert.DoesNotContain("Code", propertyNames);
        // Sanity: Name is the only editable field carried by the update request.
        Assert.Contains("Name", propertyNames);
    }
}
