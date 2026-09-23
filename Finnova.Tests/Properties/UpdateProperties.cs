using FsCheck;
using FsCheck.Xunit;
using FluentValidation;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Exceptions;
using Finnova.Service.Lookup.Commands.UpdateLookupValue;
using Finnova.Tests.Infrastructure;

namespace Finnova.Tests.Properties;

/// <summary>
/// Property-based tests for the update command path (Properties 7, 8, 9).
/// </summary>
public class UpdateProperties
{
    // Feature: lookup-master-management, Property 7: Update round trip for editable fields — for a
    // non-system-locked stored value, a valid update to Code/Value/DisplayOrder/IsActive yields
    // exactly the submitted field values when the row is retrieved afterward.
    [Property(MaxTest = 200)]
    public Property Property7_UpdateRoundTrip()
    {
        var arb = Arb.From(
            from existing in LookupGenerators.ArbUnlockedValue().Generator
            from fields in LookupGenerators.FieldsGen()
            select (existing, fields));

        return Prop.ForAll(arb, tuple =>
        {
            var (existing, fields) = tuple;
            var repo = new InMemoryLookupRepository(new[] { existing });
            var handler = new UpdateLookupValueCommandHandler(repo);

            // Editable fields only; Module/LookupType are not editable. Use fields.Code as the new
            // code — since only one row exists in scope, uniqueness cannot be violated.
            var cmd = new UpdateLookupValueCommand(
                existing.Id, fields.Code, fields.Value, fields.DisplayOrder, fields.IsActive);

            handler.Handle(cmd, CancellationToken.None).GetAwaiter().GetResult();

            var stored = repo.GetByIdAsync(existing.Id, CancellationToken.None)
                .GetAwaiter().GetResult()!;

            var roundTrips =
                stored.Code == fields.Code &&
                stored.Value == fields.Value &&
                stored.DisplayOrder == fields.DisplayOrder &&
                stored.IsActive == fields.IsActive;

            return roundTrips.Label("editable fields persisted exactly");
        });
    }

    // Feature: lookup-master-management, Property 8: System-locked values cannot be deleted or
    // renamed (atomic protection) — any update that changes the Code (alone or with other fields)
    // is blocked with ERR-LKP-005 and every attribute remains exactly as it was.
    [Property(MaxTest = 200)]
    public Property Property8_LockedCannotBeRenamed()
    {
        var arb = Arb.From(
            from locked in LookupGenerators.ArbLockedValue().Generator
            from fields in LookupGenerators.FieldsGen()
            // Ensure the requested Code differs from the existing Code (a genuine rename attempt).
            where fields.Code != locked.Code
            select (locked, fields));

        return Prop.ForAll(arb, tuple =>
        {
            var (locked, fields) = tuple;
            var repo = new InMemoryLookupRepository(new[] { locked });
            var handler = new UpdateLookupValueCommandHandler(repo);

            var cmd = new UpdateLookupValueCommand(
                locked.Id, fields.Code, fields.Value, fields.DisplayOrder, fields.IsActive);

            var blocked = false;
            string? code = null;
            try
            {
                handler.Handle(cmd, CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (LookupLockedException ex)
            {
                blocked = true;
                code = LookupLockedException.ErrorCode;
                _ = ex.Message;
            }

            var stored = repo.GetByIdAsync(locked.Id, CancellationToken.None)
                .GetAwaiter().GetResult()!;

            var unchanged =
                stored.Code == locked.Code &&
                stored.Value == locked.Value &&
                stored.DisplayOrder == locked.DisplayOrder &&
                stored.IsActive == locked.IsActive;

            return (blocked && code == "ERR-LKP-005" && unchanged)
                .Label("locked rename blocked with ERR-LKP-005, row unchanged");
        });
    }

    // Feature: lookup-master-management, Property 9: System-locked values still permit non-identity
    // edits — an update that leaves the Code unchanged but modifies Value/DisplayOrder/IsActive
    // succeeds and persists those changes.
    [Property(MaxTest = 200)]
    public Property Property9_LockedPermitsNonIdentityEdits()
    {
        var arb = Arb.From(
            from locked in LookupGenerators.ArbLockedValue().Generator
            from fields in LookupGenerators.FieldsGen()
            select (locked, fields));

        return Prop.ForAll(arb, tuple =>
        {
            var (locked, fields) = tuple;
            var repo = new InMemoryLookupRepository(new[] { locked });
            var handler = new UpdateLookupValueCommandHandler(repo);

            // Same Code (no rename), but different Value/DisplayOrder/IsActive.
            var cmd = new UpdateLookupValueCommand(
                locked.Id, locked.Code, fields.Value, fields.DisplayOrder, fields.IsActive);

            handler.Handle(cmd, CancellationToken.None).GetAwaiter().GetResult();

            var stored = repo.GetByIdAsync(locked.Id, CancellationToken.None)
                .GetAwaiter().GetResult()!;

            var applied =
                stored.Code == locked.Code &&
                stored.Value == fields.Value &&
                stored.DisplayOrder == fields.DisplayOrder &&
                stored.IsActive == fields.IsActive &&
                stored.IsSystemLocked; // still locked

            return applied.Label("locked non-identity edit persisted");
        });
    }
}
