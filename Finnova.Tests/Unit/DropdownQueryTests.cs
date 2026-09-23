using FluentValidation;
using Moq;
using Xunit;
using Finnova.Models.Domain.Entities;
using Finnova.Repository.Interfaces;
using Finnova.Service.Lookup.Queries.GetLookupDropdownItems;
using Finnova.Tests.Infrastructure;

namespace Finnova.Tests.Unit;

/// <summary>
/// Example/edge unit tests for the dropdown consumption query handler (task 4.13).
/// </summary>
public class DropdownQueryTests
{
    // R6.5 — unknown scope (not defined in the system) is rejected with a validation error.
    [Fact]
    public async Task Handler_Throws_When_ScopeUnknown()
    {
        var repo = new Mock<ILookupRepository>();
        repo.Setup(r => r.ScopeExistsAsync("Ghost", "NONE", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new GetLookupDropdownItemsQueryHandler(repo.Object);

        await Assert.ThrowsAsync<ValidationException>(
            () => handler.Handle(new GetLookupDropdownItemsQuery("Ghost", "NONE"), CancellationToken.None));
        repo.Verify(r => r.GetActiveByModuleAndTypeAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // R6.6 — a defined-but-empty scope returns an empty list, not an error.
    [Fact]
    public async Task Handler_Returns_Empty_For_DefinedButInactiveScope()
    {
        // Scope is defined (one row exists) but the only row is inactive, so the dropdown is empty.
        var inactive = new LookupValueBuilder()
            .WithModule("Origination").WithType("MARITAL_STATUS")
            .WithCode("OLD").WithValue("Old").Active(false).Build();

        var repo = new InMemoryLookupRepository(new[] { inactive });
        var handler = new GetLookupDropdownItemsQueryHandler(repo);

        var result = await handler.Handle(
            new GetLookupDropdownItemsQuery("Origination", "MARITAL_STATUS"), CancellationToken.None);

        Assert.Empty(result);
    }
}
