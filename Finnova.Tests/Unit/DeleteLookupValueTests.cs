using Moq;
using Xunit;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Lookup.Commands.DeleteLookupValue;
using Finnova.Tests.Infrastructure;

namespace Finnova.Tests.Unit;

/// <summary>
/// Example/edge unit tests for the delete command handler (task 4.9).
/// </summary>
public class DeleteLookupValueTests
{
    // R3.1 (TC-LKP-02) — deleting a system-locked value (SYS_TXN_TYPE) is blocked with ERR-LKP-005
    // and the exact message, and the row is preserved.
    [Fact]
    public async Task Handler_Blocks_Delete_On_SystemLocked_TCLKP02()
    {
        var locked = new LookupValueBuilder()
            .WithModule("SystemAdmin").WithType("SYS_TXN_TYPE")
            .WithCode("DEBIT").WithValue("Debit").Locked().Build();

        var repo = new InMemoryLookupRepository(new[] { locked });
        var handler = new DeleteLookupValueCommandHandler(repo);

        var ex = await Assert.ThrowsAsync<LookupLockedException>(
            () => handler.Handle(new DeleteLookupValueCommand(locked.Id), CancellationToken.None));

        Assert.Equal("ERR-LKP-005", LookupLockedException.ErrorCode);
        Assert.Equal("System-defined lookup codes cannot be altered.", ex.Message);

        // Row preserved.
        var stored = await repo.GetByIdAsync(locked.Id, CancellationToken.None);
        Assert.NotNull(stored);
    }

    // R5.4 — deleting a non-existent value throws LookupNotFoundException.
    [Fact]
    public async Task Handler_Throws_NotFound_When_Missing()
    {
        var repo = new Mock<ILookupRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LookupValue?)null);

        var handler = new DeleteLookupValueCommandHandler(repo.Object);

        await Assert.ThrowsAsync<LookupNotFoundException>(
            () => handler.Handle(new DeleteLookupValueCommand(Guid.NewGuid()), CancellationToken.None));
    }

    // R5.6 (scope) — a non-locked, unreferenced value is deleted successfully and removed from the store.
    [Fact]
    public async Task Handler_Deletes_NonLocked_Value()
    {
        var value = new LookupValueBuilder().WithCode("SIN").WithValue("Single").Build();
        var repo = new InMemoryLookupRepository(new[] { value });
        var handler = new DeleteLookupValueCommandHandler(repo);

        var result = await handler.Handle(new DeleteLookupValueCommand(value.Id), CancellationToken.None);

        Assert.True(result);
        var stored = await repo.GetByIdAsync(value.Id, CancellationToken.None);
        Assert.Null(stored);
    }
}
