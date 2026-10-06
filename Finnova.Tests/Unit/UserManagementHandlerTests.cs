using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Service.UserManagement.Abstractions;
using Finnova.Service.UserManagement.Commands.CreateUserAccount;
using Finnova.Service.UserManagement.Commands.CreateUserGroup;
using Finnova.Service.UserManagement.Commands.ResetPassword;
using Finnova.Service.UserManagement.Commands.UpdateUserAccount;
using Finnova.Tests.Infrastructure;
using FluentValidation;
using Xunit;

namespace Finnova.Tests.Unit;

/// <summary>Example / edge tests for the User Management handlers + validators over the in-memory repo.</summary>
public class UserManagementHandlerTests
{
    private const string Admin = "admin-1";
    private static readonly IPasswordPolicy Policy = new DefaultPasswordPolicy();

    private static UserAccount ActiveUser(string code, string name = "User")
        => new()
        {
            UserCode = code,
            Name = name,
            IsActive = true,
            PasswordHash = "x.y",
            Designation = "Officer",
            Department = "Operations",
            UserType = UserType.Branch,
            DateOfJoining = DateTime.UtcNow.Date
        };

    [Fact] // R3.5 — non-compliant password rejected on create
    public async Task Create_WithWeakPassword_Throws()
    {
        var repo = new InMemoryUserManagementRepository();
        var handler = new CreateUserAccountCommandHandler(repo, Policy);
        await Assert.ThrowsAsync<UserValidationException>(() => handler.Handle(
            new CreateUserAccountCommand("Asha", "weak", null, "Officer", "Operations",
                null, null, UserType.Branch, null, Admin), default));
        Assert.Empty(repo.Users);     // no persistence on rejection
        Assert.Empty(repo.Audit);     // no audit on rejection (R14.3)
    }

    [Fact] // R5.6 — group with no members rejected by validator
    public void CreateGroup_WithNoMembers_FailsValidation()
    {
        var validator = new CreateUserGroupCommandValidator();
        var result = validator.Validate(new CreateUserGroupCommand("Ops Team", new List<string>(), null, Admin));
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Please enter the User Code & Name");
    }

    [Fact] // R5.8 — inactive member rejected
    public async Task CreateGroup_WithInactiveMember_Throws()
    {
        var repo = new InMemoryUserManagementRepository();
        repo.Users.Add(new UserAccount { UserCode = "U1001", Name = "X", IsActive = false, PasswordHash = "x.y" });
        var handler = new CreateUserGroupCommandHandler(repo);
        await Assert.ThrowsAsync<UserInactiveMemberException>(() => handler.Handle(
            new CreateUserGroupCommand("Ops Team", new[] { "U1001" }, null, Admin), default));
    }

    [Fact] // R11.8 — modify leaves password hash unchanged (blank-password = no change)
    public async Task Update_DoesNotChangePasswordHash()
    {
        var repo = new InMemoryUserManagementRepository();
        var user = ActiveUser("U1001", "Asha");
        user.PasswordHash = "ORIGINAL.HASH";
        repo.Users.Add(user);

        var handler = new UpdateUserAccountCommandHandler(repo);
        await handler.Handle(new UpdateUserAccountCommand(user.Id, "Asha Rao", null, "Officer",
            "Operations", null, null, UserType.Branch, true, Admin), default);

        Assert.Equal("ORIGINAL.HASH", repo.Users.Single().PasswordHash);
    }

    [Fact] // R11.6 — reset-password with weak password preserves the existing hash
    public async Task ResetPassword_WithWeakPassword_PreservesHash()
    {
        var repo = new InMemoryUserManagementRepository();
        var user = ActiveUser("U1001");
        user.PasswordHash = "ORIGINAL.HASH";
        repo.Users.Add(user);

        var handler = new ResetPasswordCommandHandler(repo, Policy);
        await Assert.ThrowsAsync<UserValidationException>(() => handler.Handle(
            new ResetPasswordCommand(user.Id, "weak", Admin), default));
        Assert.Equal("ORIGINAL.HASH", repo.Users.Single().PasswordHash);
    }

    [Fact] // R11.7 — modify a missing user yields not-found
    public async Task Update_MissingUser_Throws()
    {
        var repo = new InMemoryUserManagementRepository();
        var handler = new UpdateUserAccountCommandHandler(repo);
        await Assert.ThrowsAsync<UserNotFoundException>(() => handler.Handle(
            new UpdateUserAccountCommand(Guid.NewGuid(), "X", null, "Officer", "Operations",
                null, null, UserType.Branch, true, Admin), default));
    }

    [Fact] // R14.1 — successful create writes exactly one audit entry
    public async Task Create_WritesSingleAuditEntry()
    {
        var repo = new InMemoryUserManagementRepository();
        var handler = new CreateUserAccountCommandHandler(repo, Policy);
        await handler.Handle(new CreateUserAccountCommand("Asha", "Passw0rd!", null, "Officer",
            "Operations", null, null, UserType.Branch, null, Admin), default);
        Assert.Single(repo.Audit);
        Assert.Equal(UserManagementAuditAction.Create, repo.Audit[0].Action);
    }

    [Theory] // R3.2/3.4/3.9/3.10 — required-field messages
    [InlineData("", "Passw0rd!", "Officer", "Operations", "Please enter the User Name")]
    [InlineData("Asha", "", "Officer", "Operations", "Please enter the User Password")]
    [InlineData("Asha", "Passw0rd!", "", "Operations", "Please select the Designation")]
    [InlineData("Asha", "Passw0rd!", "Officer", "", "Please select the Department")]
    public void CreateValidator_RequiredFieldMessages(string name, string pwd, string desig, string dept, string message)
    {
        var validator = new CreateUserAccountCommandValidator();
        var result = validator.Validate(new CreateUserAccountCommand(
            name, pwd, null, desig, dept, null, null, UserType.Branch, null, Admin));
        Assert.Contains(result.Errors, e => e.ErrorMessage == message);
    }

    [Fact] // R4.2 — mobile special-character message
    public void CreateValidator_MobileSpecialChars_Message()
    {
        var validator = new CreateUserAccountCommandValidator();
        var result = validator.Validate(new CreateUserAccountCommand(
            "Asha", "Passw0rd!", null, "Officer", "Operations", "98-76", null, UserType.Branch, null, Admin));
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Special characters are not allowed in this field");
    }
}
