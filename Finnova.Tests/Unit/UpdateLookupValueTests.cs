using FluentValidation;
using Moq;
using Xunit;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Lookup.Commands.UpdateLookupValue;
using Finnova.Tests.Infrastructure;

namespace Finnova.Tests.Unit;

/// <summary>
/// Example/edge unit tests for the update command validator and handler (task 4.7).
/// </summary>
public class UpdateLookupValueTests
{
    private static readonly UpdateLookupValueCommandValidator Validator = new();

    // R5.4 — updating a non-existent value throws LookupNotFoundException.
    [Fact]
    public async Task Handler_Throws_NotFound_When_Missing()
    {
        var repo = new Mock<ILookupRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LookupValue?)null);

        var handler = new UpdateLookupValueCommandHandler(repo.Object);
        var cmd = new UpdateLookupValueCommand(Guid.NewGuid(), "X", "Y", 1, true);

        await Assert.ThrowsAsync<LookupNotFoundException>(
            () => handler.Handle(cmd, CancellationToken.None));
    }

    // R5.3 — renaming to a code that already exists in scope is rejected with a validation error.
    [Fact]
    public async Task Handler_Throws_When_RenameDuplicatesExistingCode()
    {
        var existing = new LookupValueBuilder().WithCode("SIN").WithValue("Single").Build();
        var repo = new Mock<ILookupRepository>();
        repo.Setup(r => r.GetByIdAsync(existing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        repo.Setup(r => r.ExistsByCodeAsync(existing.Module, existing.LookupType, "MAR", existing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new UpdateLookupValueCommandHandler(repo.Object);
        var cmd = new UpdateLookupValueCommand(existing.Id, "MAR", "Single", 1, true);

        await Assert.ThrowsAsync<ValidationException>(
            () => handler.Handle(cmd, CancellationToken.None));
        repo.Verify(r => r.UpdateAsync(It.IsAny<LookupValue>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // R3.2 / R3.4 — renaming a system-locked value is blocked with ERR-LKP-005 and the exact message,
    // and nothing is persisted.
    [Fact]
    public async Task Handler_Blocks_Rename_On_SystemLocked_With_ErrorCode()
    {
        var locked = new LookupValueBuilder()
            .WithModule("SystemAdmin").WithType("SYS_TXN_TYPE")
            .WithCode("DEBIT").WithValue("Debit").Locked().Build();

        var repo = new InMemoryLookupRepository(new[] { locked });
        var handler = new UpdateLookupValueCommandHandler(repo);

        // Rename attempt (Code changes from DEBIT -> DR).
        var cmd = new UpdateLookupValueCommand(locked.Id, "DR", "Debit", 1, true);

        var ex = await Assert.ThrowsAsync<LookupLockedException>(
            () => handler.Handle(cmd, CancellationToken.None));

        Assert.Equal("ERR-LKP-005", LookupLockedException.ErrorCode);
        Assert.Equal("System-defined lookup codes cannot be altered.", ex.Message);

        var stored = await repo.GetByIdAsync(locked.Id, CancellationToken.None);
        Assert.Equal("DEBIT", stored!.Code); // unchanged
    }

    // R3.3 — a system-locked value permits a non-identity edit (same Code, different Value/order/active).
    [Fact]
    public async Task Handler_Allows_NonIdentity_Edit_On_SystemLocked()
    {
        var locked = new LookupValueBuilder()
            .WithModule("SystemAdmin").WithType("SYS_TXN_TYPE")
            .WithCode("DEBIT").WithValue("Debit").WithDisplayOrder(1).Active().Locked().Build();

        var repo = new InMemoryLookupRepository(new[] { locked });
        var handler = new UpdateLookupValueCommandHandler(repo);

        // Same Code, different Value / DisplayOrder / IsActive.
        var cmd = new UpdateLookupValueCommand(locked.Id, "DEBIT", "Debit (Dr)", 5, false);
        var response = await handler.Handle(cmd, CancellationToken.None);

        Assert.Equal("DEBIT", response.Code);
        Assert.Equal("Debit (Dr)", response.Value);
        Assert.Equal(5, response.DisplayOrder);
        Assert.False(response.IsActive);
        Assert.True(response.IsSystemLocked);
    }

    // R5.7 — validator rejects empty/overlong Value and out-of-range DisplayOrder.
    [Theory]
    [InlineData("", 1)]                 // empty Value
    [InlineData("ok", -1)]              // DisplayOrder below range
    [InlineData("ok", 10000)]          // DisplayOrder above range
    public void Validator_Rejects_InvalidFields(string value, int order)
    {
        var actualValue = value == "overlong" ? new string('v', 201) : value;
        var cmd = new UpdateLookupValueCommand(Guid.NewGuid(), "CODE", actualValue, order, true);

        var result = Validator.Validate(cmd);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validator_Rejects_ValueOverMaxLength()
    {
        var cmd = new UpdateLookupValueCommand(Guid.NewGuid(), "CODE", new string('v', 201), 1, true);

        var result = Validator.Validate(cmd);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateLookupValueCommand.Value));
    }
}
