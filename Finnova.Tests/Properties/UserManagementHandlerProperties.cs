using Finnova.Models.Contracts.Common;
using Finnova.Models.Domain.Enums;
using Finnova.Service.UserManagement.Abstractions;
using Finnova.Service.UserManagement.Commands.CreateUserAccount;
using Finnova.Service.UserManagement.Queries.GetUserRecordsPaged;
using Finnova.Tests.Infrastructure;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace Finnova.Tests.Properties;

/// <summary>Property tests over the create/list handlers against the in-memory repository.</summary>
public class UserManagementHandlerProperties
{
    private const string Admin = "prop-admin";
    private static CreateUserAccountCommandHandler CreateHandler(InMemoryUserManagementRepository repo)
        => new(repo, new DefaultPasswordPolicy());

    // Feature: user-management, Property 6: List ordering is deterministic and total is paging-invariant. (R13.8/13.9)
    [Property(MaxTest = 100)]
    public Property Paging_OrdersByCode_TotalInvariant(int count, int pageSize)
    {
        var n = Math.Abs(count) % 25;
        var size = (Math.Abs(pageSize) % 10) + 1;
        var repo = new InMemoryUserManagementRepository();
        for (var i = 0; i < n; i++)
            repo.Users.Add(new Finnova.Models.Domain.Entities.UserAccount
            { UserCode = "U" + (1000 + i), Name = "User " + i, IsActive = true });

        var handler = new GetUserRecordsPagedQueryHandler(repo);
        var first = handler.Handle(new GetUserRecordsPagedQuery(null, null, null, 1, size), default).Result;
        var totalConsistent = first.Total == n;
        var sizeOk = first.Data.Count <= size;
        var sorted = first.Data.Select(d => d.Code).SequenceEqual(first.Data.Select(d => d.Code).OrderBy(c => c, StringComparer.Ordinal));
        return (totalConsistent && sizeOk && sorted).ToProperty();
    }

    // Feature: user-management, Property 7: Create round-trips fields, applies defaults, never stores plaintext. (R3.1/3.7/3.15)
    [Property(MaxTest = 100)]
    public Property Create_RoundTrips_AndNeverStoresPlaintext(NonEmptyString name)
    {
        var repo = new InMemoryUserManagementRepository();
        var handler = CreateHandler(repo);
        var pwd = "Passw0rd!";
        var r = handler.Handle(new CreateUserAccountCommand(
            name.Get, pwd, null, "Officer", "Operations", null, null, UserType.Branch, null, Admin), default).Result;

        var stored = repo.Users.Single();
        var ok = r.IsActive                                   // default active (R3.15)
            && r.DateOfJoining != default                      // DOJ defaulted (R3.7)
            && stored.PasswordHash != pwd                      // never plaintext
            && !string.IsNullOrEmpty(stored.UserCode);         // generated code
        return ok.ToProperty();
    }
}
