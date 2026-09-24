using FsCheck;
using FsCheck.Xunit;
using Finnova.Models.Domain.Entities;
using Finnova.Service.Nationality.Commands.UpdateNationalityName;
using Finnova.Service.Nationality.Queries.GetNationalitiesPaged;
using Finnova.Tests.Infrastructure;

namespace Finnova.Tests.Properties;

/// <summary>
/// Property-based test for the update-name command path (Property 4). Drives the real
/// <see cref="UpdateNationalityNameCommandHandler"/> over the in-memory nationality repositories
/// (mirroring the lookup property-test conventions: FsCheck.Xunit v2, MaxTest >= 100, custom
/// generators). Kept in its own class file to avoid conflicts with other concurrently-authored
/// nationality property suites.
/// </summary>
[Properties(Arbitrary = new[] { typeof(NationalityArbitraries) })]
public class UpdateNationalityRoundTripProperties
{
    // Feature: nationality-master-management, Property 4: Update name round trip — for any stored
    // nationality and any valid new Name that differs from the current one, applying the update
    // persists the change so retrieving the record afterward yields the submitted Name, the
    // record's UpdatedAt is no earlier than its previous value, and Code/Id are unchanged.
    // Validates: Requirement 3.1.
    [Property(MaxTest = 200)]
    public Property Property4_UpdateNameRoundTrip()
    {
        // A stored nationality plus a fresh valid field payload supplying the candidate new Name.
        var arb = Arb.From(
            from existing in NationalityGenerators.ArbNationality().Generator
            from fields in NationalityGenerators.FieldsGen()
            select (existing, fields));

        return Prop.ForAll(arb, tuple =>
        {
            var (existing, fields) = tuple;

            // The handler treats an ordinally-equal Name as a no-op (R3.5); this property only
            // covers the "differs" case, so skip trivial collisions where the generated new name
            // happens to equal the current one.
            if (string.Equals(existing.Name, fields.Name, StringComparison.Ordinal))
                return true.Label("skipped: generated name equals current name (no-op case)");

            var repo = new InMemoryNationalityRepository(new[] { existing });
            var auditRepo = new InMemoryNationalityAuditRepository();
            var handler = new UpdateNationalityNameCommandHandler(repo, auditRepo);

            var previousUpdatedAt = existing.UpdatedAt;

            var cmd = new UpdateNationalityNameCommand(existing.Id, fields.Name, "admin");
            var response = handler.Handle(cmd, CancellationToken.None).GetAwaiter().GetResult();

            // Read back via the paged query handler to confirm the change is persisted and
            // retrievable (not just reflected in the returned response).
            var queryHandler = new GetNationalitiesPagedQueryHandler(repo);
            var page = queryHandler.Handle(
                new GetNationalitiesPagedQuery(fields.Name, 1, 100),
                CancellationToken.None).GetAwaiter().GetResult();

            var stored = page.Data.FirstOrDefault(x => x.Id == existing.Id);

            var responseReflectsUpdate =
                response.Name == fields.Name &&
                response.Id == existing.Id &&
                response.Code == existing.Code &&
                response.UpdatedAt >= previousUpdatedAt;

            var retrievableWithNewName =
                stored is not null &&
                stored.Name == fields.Name &&
                stored.Code == existing.Code &&
                stored.Id == existing.Id &&
                stored.UpdatedAt >= previousUpdatedAt;

            return (responseReflectsUpdate && retrievableWithNewName)
                .Label($"updated name persisted and retrievable; Code/Id unchanged, UpdatedAt refreshed");
        });
    }
}
