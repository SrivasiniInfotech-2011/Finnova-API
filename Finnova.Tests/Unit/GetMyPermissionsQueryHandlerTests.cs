using Finnova.Models.Domain.Entities;
using Finnova.Repository.Interfaces;
using Finnova.Service.Auth.Queries.GetMyPermissions;
using Moq;
using Xunit;

namespace Finnova.Tests.Unit;

public class GetMyPermissionsQueryHandlerTests
{
    private static ScreenProgram Program(string name) =>
        new() { Id = Guid.NewGuid(), ProgramName = name, DisplayName = name, IsActive = true };

    private static UserAccessAssignment Row(
        Guid userId, Guid programId, bool add = false, bool modify = false, bool query = false, bool delete = false) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserAccountId = userId,
            ProgramId = programId,
            CanAdd = add,
            CanModify = modify,
            CanQuery = query,
            CanDelete = delete
        };

    private static Mock<IUserManagementRepository> Repo(
        IEnumerable<ScreenProgram> active, IEnumerable<UserAccessAssignment>? rows = null)
    {
        var mock = new Mock<IUserManagementRepository>();
        mock.Setup(r => r.GetActiveProgramsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(active.ToList());
        mock.Setup(r => r.GetAccessAssignmentsByUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((rows ?? Enumerable.Empty<UserAccessAssignment>()).ToList());
        return mock;
    }

    [Fact]
    public async Task Admin_ReturnsEveryActiveProgram_AllFlagsTrue()
    {
        var active = new[] { Program("LookupMaster"), Program("UserManagement") };
        var repo = Repo(active);
        var handler = new GetMyPermissionsQueryHandler(repo.Object);

        var result = await handler.Handle(
            new GetMyPermissionsQuery(Guid.NewGuid(), IsAdmin: true), CancellationToken.None);

        Assert.True(result.IsAdmin);
        Assert.Equal(2, result.Programs.Count);
        Assert.All(result.Programs, p =>
        {
            Assert.True(p.CanAdd);
            Assert.True(p.CanModify);
            Assert.True(p.CanQuery);
            Assert.True(p.CanDelete);
        });
        // Admin never reads the per-user rows.
        repo.Verify(r => r.GetAccessAssignmentsByUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NonAdmin_TwoRowsSameProgram_CollapsedByOr()
    {
        var userId = Guid.NewGuid();
        var lookup = Program("LookupMaster");
        var active = new[] { lookup };
        var rows = new[]
        {
            Row(userId, lookup.Id, add: true, query: false),
            Row(userId, lookup.Id, add: false, query: true, delete: true)
        };
        var repo = Repo(active, rows);
        var handler = new GetMyPermissionsQueryHandler(repo.Object);

        var result = await handler.Handle(
            new GetMyPermissionsQuery(userId, IsAdmin: false), CancellationToken.None);

        Assert.False(result.IsAdmin);
        var entry = Assert.Single(result.Programs);
        Assert.Equal("LookupMaster", entry.ProgramName);
        Assert.True(entry.CanAdd);     // from row 1
        Assert.False(entry.CanModify); // neither row set it
        Assert.True(entry.CanQuery);   // from row 2
        Assert.True(entry.CanDelete);  // from row 2
    }

    [Fact]
    public async Task NonAdmin_ProgramNotInActiveRegistry_Omitted()
    {
        var userId = Guid.NewGuid();
        var lookup = Program("LookupMaster");
        var active = new[] { lookup };
        var rows = new[]
        {
            Row(userId, lookup.Id, query: true),
            Row(userId, Guid.NewGuid(), add: true, query: true)   // program id not in active registry
        };
        var repo = Repo(active, rows);
        var handler = new GetMyPermissionsQueryHandler(repo.Object);

        var result = await handler.Handle(
            new GetMyPermissionsQuery(userId, IsAdmin: false), CancellationToken.None);

        var entry = Assert.Single(result.Programs);
        Assert.Equal("LookupMaster", entry.ProgramName);
    }

    [Fact]
    public async Task NonAdmin_ActiveProgramWithNoRows_Omitted()
    {
        var userId = Guid.NewGuid();
        var lookup = Program("LookupMaster");
        var active = new[] { lookup, Program("UserManagement") };
        var rows = new[] { Row(userId, lookup.Id, query: true) };
        var repo = Repo(active, rows);
        var handler = new GetMyPermissionsQueryHandler(repo.Object);

        var result = await handler.Handle(
            new GetMyPermissionsQuery(userId, IsAdmin: false), CancellationToken.None);

        var entry = Assert.Single(result.Programs);
        Assert.Equal("LookupMaster", entry.ProgramName);
    }
}
