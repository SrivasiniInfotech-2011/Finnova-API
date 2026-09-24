using FsCheck;
using FsCheck.Xunit;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Service.Nationality.Commands.UpdateNationalityName;
using Finnova.Tests.Infrastructure;

namespace Finnova.Tests.Properties;

/// <summary>
/// Property-based tests for the audit behavior of the update-name command path
/// (Properties 5, 7, 8). Each test drives the real
/// <see cref="UpdateNationalityNameCommandHandler"/> over the hand-written in-memory nationality +
/// audit repositories, using the custom <see cref="NationalityGenerators"/> arbitraries. Kept in
/// its own class file (separate from <c>UpdateNationalityRoundTripProperties</c>, task 8.2) to
/// avoid conflicts with concurrently-authored nationality property suites.
/// FsCheck.Xunit v2, MaxTest = 200 (>= the required 100 iterations).
/// </summary>
public class UpdateNationalityAuditProperties
{
    private const string Actor = "admin-1";

    // Feature: nationality-master-management, Property 5: Same-name update is a no-op with no audit —
    // for any stored nationality, submitting an update whose Name equals the record's current Name
    // returns the existing record unchanged (Id, Code, Name, IsActive, CreatedAt, UpdatedAt) and
    // writes no audit entry. Validates Requirements 3.5, 4.3.
    [Property(MaxTest = 200)]
    public Property Property5_SameNameUpdateIsNoOpWithNoAudit()
    {
        var arb = Arb.From(NationalityGenerators.ArbNationality().Generator);

        return Prop.ForAll(arb, existing =>
        {
            var repo = new InMemoryNationalityRepository(new[] { existing });
            var audit = new InMemoryNationalityAuditRepository();
            var handler = new UpdateNationalityNameCommandHandler(repo, audit);

            var before = repo.Snapshot().Single(x => x.Id == existing.Id);
            var auditBefore = audit.Count;

            // Submit the record's current Name verbatim -> handled as a no-op (R3.5).
            var response = handler.Handle(
                new UpdateNationalityNameCommand(existing.Id, existing.Name, Actor),
                CancellationToken.None).GetAwaiter().GetResult();

            var after = repo.Snapshot().Single(x => x.Id == existing.Id);

            var returnedExisting =
                response.Id == existing.Id &&
                response.Code == existing.Code &&
                response.Name == existing.Name &&
                response.IsActive == existing.IsActive &&
                response.CreatedAt == existing.CreatedAt &&
                response.UpdatedAt == existing.UpdatedAt;

            var storeUnchanged =
                after.Id == before.Id &&
                after.Code == before.Code &&
                after.Name == before.Name &&
                after.IsActive == before.IsActive &&
                after.CreatedAt == before.CreatedAt &&
                after.UpdatedAt == before.UpdatedAt;

            var noAudit = audit.Count == auditBefore && audit.Count == 0;

            return (returnedExisting && storeUnchanged && noAudit)
                .Label($"same-name update of '{existing.Name}' returned existing record, store + audit unchanged");
        });
    }

    // Feature: nationality-master-management, Property 7: Audit entry written on name update — for
    // any successful name change, exactly one audit entry is recorded with Action = Update,
    // OldName equal to the prior Name, NewName equal to the submitted Name, NationalityId equal to
    // the updated id, ChangedBy equal to the acting administrator, and a UTC timestamp.
    // Validates Requirements 4.1.
    [Property(MaxTest = 200)]
    public Property Property7_AuditEntryWrittenOnNameUpdate()
    {
        var arb = Arb.From(
            from existing in NationalityGenerators.ArbNationality().Generator
            from fields in NationalityGenerators.FieldsGen()
            select (existing, fields));

        return Prop.ForAll(arb, tuple =>
        {
            var (existing, fields) = tuple;

            // This property covers a real name change; skip the no-op case where the generated
            // new name is ordinally equal to the current name (that path writes no audit, R3.5).
            if (string.Equals(existing.Name, fields.Name, StringComparison.Ordinal))
                return true.Label("skipped: generated name equals current name (no-op case)");

            var repo = new InMemoryNationalityRepository(new[] { existing });
            var audit = new InMemoryNationalityAuditRepository();
            var handler = new UpdateNationalityNameCommandHandler(repo, audit);

            var oldName = existing.Name;

            var before = DateTime.UtcNow;
            handler.Handle(
                new UpdateNationalityNameCommand(existing.Id, fields.Name, Actor),
                CancellationToken.None).GetAwaiter().GetResult();
            var after = DateTime.UtcNow;

            var entries = audit.Snapshot();

            // Exactly one audit entry, with the expected Update shape.
            var exactlyOne = entries.Count == 1;
            if (!exactlyOne)
                return false.Label($"expected exactly one audit entry, got {entries.Count}");

            var e = entries[0];
            var shapeOk =
                e.Action == NationalityAuditAction.Update &&
                e.OldName == oldName &&
                e.NewName == fields.Name &&
                e.NationalityId == existing.Id &&
                e.ChangedBy == Actor &&
                e.ChangedAtUtc.Kind == DateTimeKind.Utc &&
                e.ChangedAtUtc >= before && e.ChangedAtUtc <= after;

            return shapeOk.Label(
                $"one Update audit entry: action={e.Action} old={e.OldName ?? "null"} " +
                $"new={e.NewName} by={e.ChangedBy} at={e.ChangedAtUtc:o}");
        });
    }

    // Feature: nationality-master-management, Property 8: No audit entry on rejection — for any
    // update targeting a non-existent id, the handler throws NationalityNotFoundException, the
    // total count of persisted audit entries is unchanged (stays 0), and the store is not mutated.
    // Validates Requirements 4.3.
    [Property(MaxTest = 200)]
    public Property Property8_NoAuditEntryOnRejection()
    {
        var arb = Arb.From(
            // A store seeded with an existing record, plus a fresh valid payload and a missing id
            // that is guaranteed distinct from the stored record's id.
            from existing in NationalityGenerators.ArbNationality().Generator
            from fields in NationalityGenerators.FieldsGen()
            select (existing, fields));

        return Prop.ForAll(arb, tuple =>
        {
            var (existing, fields) = tuple;

            var repo = new InMemoryNationalityRepository(new[] { existing });
            var audit = new InMemoryNationalityAuditRepository();
            var handler = new UpdateNationalityNameCommandHandler(repo, audit);

            var before = repo.Snapshot();
            var auditBefore = audit.Count;

            // A missing id distinct from the stored record.
            var missingId = Guid.NewGuid();
            while (missingId == existing.Id)
                missingId = Guid.NewGuid();

            var threw = false;
            try
            {
                handler.Handle(
                    new UpdateNationalityNameCommand(missingId, fields.Name, Actor),
                    CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (NationalityNotFoundException)
            {
                threw = true;
            }

            var after = repo.Snapshot();
            var storeUnchanged =
                after.Count == before.Count &&
                after.Count == 1 &&
                after[0].Id == existing.Id &&
                after[0].Code == existing.Code &&
                after[0].Name == existing.Name &&
                after[0].IsActive == existing.IsActive &&
                after[0].UpdatedAt == existing.UpdatedAt;

            var noAudit = audit.Count == auditBefore && audit.Count == 0;

            return (threw && storeUnchanged && noAudit)
                .Label($"update of missing id {missingId} rejected, store + audit unchanged");
        });
    }
}
