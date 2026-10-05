using Finnova.Models.Contracts.UserManagement;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Service.UserManagement.Commands.SaveUserAccess;
using Finnova.Tests.Infrastructure;
using Xunit;

namespace Finnova.Tests.Unit;

/// <summary>
/// Branch-association existence validation on SaveUserAccess (LocationId FK normalization).
/// A non-ALL selection must carry a LocationId that EXISTS in locations (existence only — no
/// IsActive/Level check); ALL skips location lookup. Also covers the LocationId/IsAll/BranchCode
/// contract round-trip persisted on UserBranchAssociation and echoed on UserAccessResponse.
/// </summary>
public class SaveUserAccessBranchTests
{
    private const string Admin = "admin-1";
    private const string Lob = "Retail Lending";    // LineOfBusinessCatalog.HasRoleCodes == true

    private static (InMemoryUserManagementRepository Repo, UserAccount User, LineOfBusiness Lob) Seed()
    {
        var repo = new InMemoryUserManagementRepository();
        var user = new UserAccount
        {
            UserCode = "U1001",
            Name = "Asha",
            IsActive = true,
            PasswordHash = "x.y",
            Designation = "Officer",
            Department = "Operations",
            UserType = UserType.Branch,
            DateOfJoining = DateTime.UtcNow.Date,
        };
        repo.Users.Add(user);

        var lob = new LineOfBusiness { LOB_Name = Lob, LOB_Description = "Retail", IsActive = true };
        repo.Lobs.Add(lob);
        return (repo, user, lob);
    }

    private static SaveUserAccessCommand Command(UserAccount user, LineOfBusiness lob, params BranchSelection[] branches)
        => new(user.Id, lob.Id, Array.Empty<AccessRightRow>(), branches, null, Admin);

    [Fact] // non-existent LocationId on a non-ALL selection is rejected (ERR-USR-400 path)
    public async Task Save_WithNonExistentLocationId_Throws()
    {
        var (repo, user, lob) = Seed();
        var handler = new SaveUserAccessCommandHandler(repo);

        await Assert.ThrowsAsync<UserValidationException>(() => handler.Handle(
            Command(user, lob, new BranchSelection(Guid.NewGuid(), false, "")), default));

        Assert.Empty(repo.Branches);   // nothing persisted on rejection
    }

    [Fact] // non-ALL selection with a null LocationId is rejected
    public async Task Save_WithNullLocationId_Throws()
    {
        var (repo, user, lob) = Seed();
        var handler = new SaveUserAccessCommandHandler(repo);

        await Assert.ThrowsAsync<UserValidationException>(() => handler.Handle(
            Command(user, lob, new BranchSelection(null, false, "")), default));
    }

    [Fact] // ALL selection persists IsAll/null/"ALL" without any location lookup
    public async Task Save_WithAll_PersistsSentinel()
    {
        var (repo, user, lob) = Seed();
        var handler = new SaveUserAccessCommandHandler(repo);

        var result = await handler.Handle(
            Command(user, lob, new BranchSelection(null, true, "ALL")), default);

        var persisted = Assert.Single(repo.Branches);
        Assert.True(persisted.IsAll);
        Assert.Null(persisted.LocationId);
        Assert.Equal("ALL", persisted.BranchCode);

        var selection = Assert.Single(result.Branches);
        Assert.True(selection.IsAll);
        Assert.Null(selection.LocationId);
        Assert.Equal("ALL", selection.BranchCode);
    }

    [Fact] // a valid, existing LocationId persists the FK and fills BranchCode from the location's Code
    public async Task Save_WithExistingLocationId_PersistsFkAndCode()
    {
        var (repo, user, lob) = Seed();
        var location = new Location { Code = "FORT", Name = "Fort Branch", Level = 5, IsActive = false };
        repo.Locations.Add(location);   // IsActive false proves existence-only (no active check)
        var handler = new SaveUserAccessCommandHandler(repo);

        var result = await handler.Handle(
            Command(user, lob, new BranchSelection(location.Id, false, "")), default);

        var persisted = Assert.Single(repo.Branches);
        Assert.False(persisted.IsAll);
        Assert.Equal(location.Id, persisted.LocationId);
        Assert.Equal("FORT", persisted.BranchCode);   // reference code resolved server-side

        var selection = Assert.Single(result.Branches);
        Assert.Equal(location.Id, selection.LocationId);
        Assert.False(selection.IsAll);
        Assert.Equal("FORT", selection.BranchCode);
    }
}
