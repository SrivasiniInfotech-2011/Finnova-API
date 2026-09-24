using FsCheck;
using FsCheck.Xunit;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Service.Nationality.Commands.CreateNationality;
using Finnova.Service.Nationality.Queries.GetNationalitiesPaged;
using Finnova.Tests.Infrastructure;

namespace Finnova.Tests.Properties;

/// <summary>
/// Property-based tests for the create-nationality command path (Properties 1, 2, 3, 6).
/// Each test drives the real <see cref="CreateNationalityCommandHandler"/> (and, for the
/// round trip, the real <see cref="GetNationalitiesPagedQueryHandler"/>) over the hand-written
/// in-memory nationality + audit repositories, using the custom <see cref="NationalityGenerators"/>
/// arbitraries registered inline via <c>Arb.From</c> exactly as the Lookup property tests do.
/// FsCheck.Xunit v2, MaxTest = 200 (>= the required 100 iterations).
/// </summary>
public class CreateNationalityProperties
{
    private const string Actor = "admin-1";

    private static CreateNationalityCommandHandler CreateHandler(
        InMemoryNationalityRepository repo, InMemoryNationalityAuditRepository audit)
        => new(repo, audit);

    // Feature: nationality-master-management, Property 1: Create round trip — for any valid create
    // input applied to a store with no matching code, the create persists the record and a
    // subsequent paged query returns it, echoing the submitted Code and Name with a non-empty
    // generated Id. Validates Requirements 1.1, 1.6.
    [Property(MaxTest = 200)]
    public Property Property1_CreateRoundTrip()
    {
        var arb = Arb.From(NationalityGenerators.FieldsGen());

        return Prop.ForAll(arb, fields =>
        {
            var repo = new InMemoryNationalityRepository();
            var audit = new InMemoryNationalityAuditRepository();
            var createHandler = CreateHandler(repo, audit);
            var queryHandler = new GetNationalitiesPagedQueryHandler(repo);

            var created = createHandler.Handle(
                new CreateNationalityCommand(fields.Code, fields.Name, fields.IsActive, Actor),
                CancellationToken.None).GetAwaiter().GetResult();

            var expectedCode = fields.Code.Trim();

            // Returned record echoes the submitted (trimmed) Code and Name with a real Id.
            var responseOk =
                created.Id != Guid.Empty &&
                created.Code == expectedCode &&
                created.Name == fields.Name;

            // The created record is retrievable via a paged query (searching by its code).
            var page = queryHandler.Handle(
                new GetNationalitiesPagedQuery(expectedCode, 1, 200),
                CancellationToken.None).GetAwaiter().GetResult();

            var found = page.Data.Any(r =>
                r.Id == created.Id &&
                r.Code == expectedCode &&
                r.Name == fields.Name);

            return (responseOk && found)
                .Label($"created {expectedCode}/{fields.Name} persisted and queryable");
        });
    }

    // Feature: nationality-master-management, Property 2: Code uniqueness on create — for any stored
    // nationality and any casing / surrounding-whitespace variant of its Code, a create with that
    // variant is rejected with NationalityDuplicateCodeException (message "Nationality code must be
    // unique"), creates no new record, leaves the existing record unchanged, and writes no audit.
    // Validates Requirements 1.5, 2.1, 2.2.
    [Property(MaxTest = 200)]
    public Property Property2_CodeUniquenessOnCreate()
    {
        var arb = Arb.From(
            from existing in NationalityGenerators.ArbNationality().Generator
            from newName in NationalityGenerators.NameGen()
            from variant in NationalityGenerators.CasingWhitespaceVariantsOf(existing.Code)
            from isActive in Gen.Elements<bool?>(null, true, false)
            select (existing, newName, variant, isActive));

        return Prop.ForAll(arb, tuple =>
        {
            var (existing, newName, variant, isActive) = tuple;

            var repo = new InMemoryNationalityRepository(new[] { existing });
            var audit = new InMemoryNationalityAuditRepository();
            var handler = CreateHandler(repo, audit);

            var before = repo.Snapshot();
            var auditBefore = audit.Count;

            var rejected = false;
            string? message = null;
            try
            {
                handler.Handle(
                    new CreateNationalityCommand(variant, newName, isActive, Actor),
                    CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (NationalityDuplicateCodeException ex)
            {
                rejected = true;
                message = ex.Message;
            }

            var after = repo.Snapshot();
            var unchanged =
                after.Count == before.Count &&
                after.Count == 1 &&
                after[0].Id == existing.Id &&
                after[0].Code == existing.Code &&
                after[0].Name == existing.Name;

            var noAudit = audit.Count == auditBefore && audit.Count == 0;
            var exactMessage = message == "Nationality code must be unique";

            return (rejected && exactMessage && unchanged && noAudit)
                .Label($"duplicate variant '{variant}' of '{existing.Code}' rejected, store + audit unchanged");
        });
    }

    // Feature: nationality-master-management, Property 3: Create defaults IsActive to true — for any
    // valid create input where IsActive is not provided (null) the persisted record has
    // IsActive == true; when IsActive is provided the persisted value is honored.
    // Validates Requirements 1.2.
    [Property(MaxTest = 200)]
    public Property Property3_CreateDefaultsIsActiveToTrue()
    {
        var arb = Arb.From(
            from code in NationalityGenerators.CodeGen()
            from name in NationalityGenerators.NameGen()
            from isActive in Gen.Elements<bool?>(null, true, false)
            select (code, name, isActive));

        return Prop.ForAll(arb, tuple =>
        {
            var (code, name, isActive) = tuple;

            var repo = new InMemoryNationalityRepository();
            var audit = new InMemoryNationalityAuditRepository();
            var handler = CreateHandler(repo, audit);

            var created = handler.Handle(
                new CreateNationalityCommand(code, name, isActive, Actor),
                CancellationToken.None).GetAwaiter().GetResult();

            var stored = repo.Snapshot().Single(x => x.Id == created.Id);

            // When null -> defaulted to true; when provided -> honored.
            var expected = isActive ?? true;
            var resolvedOk = created.IsActive == expected && stored.IsActive == expected;

            return resolvedOk.Label(
                $"IsActive input={(isActive.HasValue ? isActive.Value.ToString() : "null")} " +
                $"-> persisted={stored.IsActive} (expected {expected})");
        });
    }

    // Feature: nationality-master-management, Property 6: Audit entry written on create — for any
    // successful create, exactly one audit entry is recorded with Action = Create, OldName = null,
    // NewName equal to the created Name, NationalityId equal to the created id, ChangedBy equal to
    // the acting administrator, and a UTC timestamp. Validates Requirements 4.2.
    [Property(MaxTest = 200)]
    public Property Property6_AuditEntryWrittenOnCreate()
    {
        var arb = Arb.From(NationalityGenerators.FieldsGen());

        return Prop.ForAll(arb, fields =>
        {
            var repo = new InMemoryNationalityRepository();
            var audit = new InMemoryNationalityAuditRepository();
            var handler = CreateHandler(repo, audit);

            var before = DateTime.UtcNow;
            var created = handler.Handle(
                new CreateNationalityCommand(fields.Code, fields.Name, fields.IsActive, Actor),
                CancellationToken.None).GetAwaiter().GetResult();
            var after = DateTime.UtcNow;

            var entries = audit.Snapshot();

            // Exactly one audit entry, with the expected Create shape.
            var exactlyOne = entries.Count == 1;
            if (!exactlyOne)
                return false.Label($"expected exactly one audit entry, got {entries.Count}");

            var e = entries[0];
            var shapeOk =
                e.Action == NationalityAuditAction.Create &&
                e.OldName == null &&
                e.NewName == created.Name &&
                e.NationalityId == created.Id &&
                e.ChangedBy == Actor &&
                e.ChangedAtUtc.Kind == DateTimeKind.Utc &&
                e.ChangedAtUtc >= before && e.ChangedAtUtc <= after;

            return shapeOk.Label(
                $"one Create audit entry: action={e.Action} old={e.OldName ?? "null"} " +
                $"new={e.NewName} by={e.ChangedBy} at={e.ChangedAtUtc:o}");
        });
    }
}
