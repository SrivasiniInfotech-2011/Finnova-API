using FluentValidation;
using Moq;
using Xunit;
using Finnova.Models.Domain.Entities;
using Finnova.Repository.Interfaces;
using Finnova.Service.Lookup.Commands.CreateLookupValue;
using Finnova.Tests.Infrastructure;

namespace Finnova.Tests.Unit;

/// <summary>
/// Example/edge unit tests for the create command validator and handler (task 4.4, TC-LKP-01).
/// </summary>
public class CreateLookupValueTests
{
    private static readonly CreateLookupValueCommandValidator Validator = new();

    private static CreateLookupValueCommand ValidCommand() => new(
        Module: "Origination",
        LookupType: "MARITAL_STATUS",
        Code: "WID",
        Value: "Widowed",
        DisplayOrder: 4,
        IsActive: true);

    // R2.3 — missing required fields are rejected by the validator.
    [Theory]
    [InlineData("", "MARITAL_STATUS", "WID", "Widowed")]        // missing Module
    [InlineData("Origination", "", "WID", "Widowed")]            // missing LookupType
    [InlineData("Origination", "MARITAL_STATUS", "", "Widowed")] // missing Code
    [InlineData("Origination", "MARITAL_STATUS", "WID", "")]     // missing Value
    public void Validator_Rejects_MissingRequiredField(string module, string type, string code, string value)
    {
        var cmd = new CreateLookupValueCommand(module, type, code, value, 4, true);

        var result = Validator.Validate(cmd);

        Assert.False(result.IsValid);
    }

    // R2.7 — Value length overflow (> 200) is rejected.
    [Fact]
    public void Validator_Rejects_ValueOverMaxLength()
    {
        var cmd = ValidCommand() with { Value = new string('x', 201) };

        var result = Validator.Validate(cmd);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateLookupValueCommand.Value));
    }

    // R2.7 — Code length overflow (> 50) is rejected.
    [Fact]
    public void Validator_Rejects_CodeOverMaxLength()
    {
        var cmd = ValidCommand() with { Code = new string('C', 51) };

        var result = Validator.Validate(cmd);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateLookupValueCommand.Code));
    }

    // R2.5 — DisplayOrder out of range (< 0 or > 9999) is rejected.
    [Theory]
    [InlineData(-1)]
    [InlineData(10000)]
    public void Validator_Rejects_DisplayOrderOutOfRange(int order)
    {
        var cmd = ValidCommand() with { DisplayOrder = order };

        var result = Validator.Validate(cmd);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateLookupValueCommand.DisplayOrder));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9999)]
    public void Validator_Accepts_DisplayOrderBoundaries(int order)
    {
        var cmd = ValidCommand() with { DisplayOrder = order };

        var result = Validator.Validate(cmd);

        Assert.True(result.IsValid);
    }

    // R2.9 — unknown scope (Module/LookupType not defined) is rejected by the handler.
    [Fact]
    public async Task Handler_Throws_When_ScopeUnknown()
    {
        var repo = new Mock<ILookupRepository>();
        repo.Setup(r => r.ScopeExistsAsync("Ghost", "NONE", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new CreateLookupValueCommandHandler(repo.Object);
        var cmd = new CreateLookupValueCommand("Ghost", "NONE", "X", "Y", 1, true);

        await Assert.ThrowsAsync<ValidationException>(
            () => handler.Handle(cmd, CancellationToken.None));
        repo.Verify(r => r.AddAsync(It.IsAny<LookupValue>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // R2.8 — duplicate code within scope is rejected by the handler.
    [Fact]
    public async Task Handler_Throws_When_DuplicateCode()
    {
        var repo = new Mock<ILookupRepository>();
        repo.Setup(r => r.ScopeExistsAsync("Origination", "MARITAL_STATUS", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        repo.Setup(r => r.ExistsByCodeAsync("Origination", "MARITAL_STATUS", "WID", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new CreateLookupValueCommandHandler(repo.Object);

        await Assert.ThrowsAsync<ValidationException>(
            () => handler.Handle(ValidCommand(), CancellationToken.None));
        repo.Verify(r => r.AddAsync(It.IsAny<LookupValue>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // R2.4 — IsActive defaults to true when null on create.
    [Fact]
    public async Task Handler_Defaults_IsActive_True_When_Null()
    {
        var repo = new InMemoryLookupRepository(new[]
        {
            new LookupValueBuilder().WithCode("SIN").WithValue("Single").Build()
        });
        var handler = new CreateLookupValueCommandHandler(repo);

        var cmd = ValidCommand() with { IsActive = null };
        var response = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(response.IsActive);
    }

    // R2.2 (TC-LKP-01) — creating WID/Widowed/4/true under Origination/MARITAL_STATUS succeeds
    // and is retrievable via the dropdown consumption path.
    [Fact]
    public async Task Handler_Creates_WID_And_IsRetrievable_TCLKP01()
    {
        var repo = new InMemoryLookupRepository(new[]
        {
            new LookupValueBuilder().WithCode("SIN").WithValue("Single").WithDisplayOrder(1).Build(),
            new LookupValueBuilder().WithCode("MAR").WithValue("Married").WithDisplayOrder(2).Build(),
            new LookupValueBuilder().WithCode("DIV").WithValue("Divorced").WithDisplayOrder(3).Build(),
        });

        var createHandler = new CreateLookupValueCommandHandler(repo);
        var response = await createHandler.Handle(ValidCommand(), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, response.Id);
        Assert.Equal("WID", response.Code);
        Assert.Equal("Widowed", response.Value);

        var dropdownHandler = new Finnova.Service.Lookup.Queries.GetLookupDropdownItems
            .GetLookupDropdownItemsQueryHandler(repo);
        var items = await dropdownHandler.Handle(
            new Finnova.Service.Lookup.Queries.GetLookupDropdownItems
                .GetLookupDropdownItemsQuery("Origination", "MARITAL_STATUS"),
            CancellationToken.None);

        Assert.Contains(items, i => i.Code == "WID" && i.Label == "Widowed");
    }
}
