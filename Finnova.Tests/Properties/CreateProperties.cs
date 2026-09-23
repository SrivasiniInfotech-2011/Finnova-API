using FsCheck;
using FsCheck.Xunit;
using FluentValidation;
using Finnova.Models.Domain.Entities;
using Finnova.Service.Lookup.Commands.CreateLookupValue;
using Finnova.Service.Lookup.Queries.GetLookupDropdownItems;
using Finnova.Tests.Infrastructure;

namespace Finnova.Tests.Properties;

/// <summary>
/// Property-based tests for the create command path (Properties 2, 6).
/// </summary>
public class CreateProperties
{
    // Feature: lookup-master-management, Property 2: Lookup Code uniqueness within Module + Lookup
    // Type scope — a create whose (Module, LookupType, Code) already exists is rejected with a
    // validation error and the stored catalog is left unchanged.
    [Property(MaxTest = 200)]
    public Property Property2_CodeUniquenessOnCreate()
    {
        // Seed a single existing row; then attempt to create another row in the same scope with
        // the same Code (duplicate) — must be rejected and leave the store unchanged.
        var arb = Arb.From(
            from existing in LookupGenerators.ArbUnlockedValue().Generator
            from newValue in LookupGenerators.FieldsGen()
            select (existing, newValue));

        return Prop.ForAll(arb, tuple =>
        {
            var (existing, fields) = tuple;
            var repo = new InMemoryLookupRepository(new[] { existing });
            var handler = new CreateLookupValueCommandHandler(repo);

            // Force a duplicate: same scope + same Code as the existing row.
            var cmd = new CreateLookupValueCommand(
                existing.Module, existing.LookupType, existing.Code,
                fields.Value, fields.DisplayOrder, fields.IsActive);

            var before = repo.Snapshot();
            var rejected = false;
            try
            {
                handler.Handle(cmd, CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (ValidationException)
            {
                rejected = true;
            }

            var after = repo.Snapshot();
            var unchanged = after.Count == before.Count &&
                            after.Count == 1 &&
                            after[0].Value == existing.Value;

            return (rejected && unchanged).Label("duplicate create rejected, catalog unchanged");
        });
    }

    // Feature: lookup-master-management, Property 6: Create-then-dropdown round trip — after a valid
    // active create, a dropdown query for its Module and Lookup Type returns an item whose Code and
    // label (Value) equal the created value's.
    [Property(MaxTest = 200)]
    public Property Property6_CreateThenDropdownRoundTrip()
    {
        var arb = Arb.From(LookupGenerators.FieldsGen());

        return Prop.ForAll(arb, fields =>
        {
            // Seed one pre-existing row so the scope is defined (ScopeExists guard), but with a
            // different Code so the new create is not a duplicate.
            var seedRow = new LookupValue
            {
                Id = Guid.NewGuid(),
                Module = fields.Module,
                LookupType = fields.LookupType,
                Code = "__SEED__" + fields.Code,
                Value = "seed",
                DisplayOrder = 0,
                IsActive = true,
            };

            var repo = new InMemoryLookupRepository(new[] { seedRow });
            var createHandler = new CreateLookupValueCommandHandler(repo);
            var dropdownHandler = new GetLookupDropdownItemsQueryHandler(repo);

            // Create as active (IsActive = true) so it must appear in the dropdown.
            var cmd = new CreateLookupValueCommand(
                fields.Module, fields.LookupType, fields.Code,
                fields.Value, fields.DisplayOrder, true);

            createHandler.Handle(cmd, CancellationToken.None).GetAwaiter().GetResult();

            var items = dropdownHandler.Handle(
                new GetLookupDropdownItemsQuery(fields.Module, fields.LookupType),
                CancellationToken.None).GetAwaiter().GetResult();

            var found = items.Any(i => i.Code == fields.Code && i.Label == fields.Value);

            return found.Label($"created {fields.Code}/{fields.Value} present in dropdown");
        });
    }
}
