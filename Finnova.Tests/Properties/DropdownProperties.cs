using FsCheck;
using FsCheck.Xunit;
using FluentValidation;
using Finnova.Models.Contracts.Lookups;
using Finnova.Models.Domain.Entities;
using Finnova.Service.Lookup.Queries.GetLookupDropdownItems;
using Finnova.Tests.Infrastructure;

namespace Finnova.Tests.Properties;

/// <summary>
/// Property-based tests for the dropdown consumption query handler (Properties 4, 5).
/// </summary>
public class DropdownProperties
{
    private static GetLookupDropdownItemsQueryHandler Handler(IEnumerable<LookupValue> data)
        => new(new InMemoryLookupRepository(data));

    // Gen of a dataset plus a scope (Module, Type) that is guaranteed to be defined in the data
    // (drawn from an existing row) so the handler does not short-circuit with an unknown-scope error.
    private static Gen<(List<LookupValue> Data, string Module, string Type)> DatasetWithDefinedScope()
    {
        return
            from data in LookupGenerators.DatasetGen().Where(d => d.Count > 0)
            from idx in Gen.Choose(0, int.MaxValue)
            let pick = data[idx % data.Count]
            select (data, pick.Module, pick.LookupType);
    }

    // Feature: lookup-master-management, Property 4: Dropdown ordering is DisplayOrder ascending with
    // Value tie-break and is deterministic across repeated queries over identical data.
    [Property(MaxTest = 200)]
    public Property Property4_DropdownOrderingDeterministic()
    {
        var arb = Arb.From(DatasetWithDefinedScope());

        return Prop.ForAll(arb, tuple =>
        {
            var (data, module, type) = tuple;
            var handler = Handler(data);
            var q = new GetLookupDropdownItemsQuery(module, type);

            var first = handler.Handle(q, CancellationToken.None).GetAwaiter().GetResult();
            var second = handler.Handle(q, CancellationToken.None).GetAwaiter().GetResult();

            // Reconstruct the expected order from active rows in scope: DisplayOrder then Value.
            var expected = data
                .Where(x => x.Module == module && x.LookupType == type && x.IsActive)
                .OrderBy(x => x.DisplayOrder)
                .ThenBy(x => x.Value, StringComparer.Ordinal)
                .Select(x => x.Code)
                .ToList();

            var ordered = first.Select(x => x.Code).SequenceEqual(expected);
            var deterministic = first.Select(x => x.Code).SequenceEqual(second.Select(x => x.Code));

            return (ordered && deterministic).Label($"scope={module}/{type} count={first.Count}");
        });
    }

    // Feature: lookup-master-management, Property 5: Dropdown returns only active values, and no
    // active value in the requested scope is omitted.
    [Property(MaxTest = 200)]
    public Property Property5_DropdownActiveOnly()
    {
        var arb = Arb.From(DatasetWithDefinedScope());

        return Prop.ForAll(arb, tuple =>
        {
            var (data, module, type) = tuple;
            var handler = Handler(data);

            List<LookupDropdownItemResponse> result;
            try
            {
                result = handler.Handle(
                    new GetLookupDropdownItemsQuery(module, type),
                    CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (ValidationException)
            {
                // Scope guaranteed to exist by construction; a validation error would be a bug.
                return false.Label("unexpected unknown-scope error for a defined scope");
            }

            var activeCodesInScope = data
                .Where(x => x.Module == module && x.LookupType == type && x.IsActive)
                .Select(x => x.Code)
                .OrderBy(c => c, StringComparer.Ordinal)
                .ToList();

            var returnedCodes = result.Select(x => x.Code)
                .OrderBy(c => c, StringComparer.Ordinal)
                .ToList();

            // Every returned code corresponds to an active in-scope row, and none is omitted.
            var sameSet = returnedCodes.SequenceEqual(activeCodesInScope);

            return sameSet.Label($"scope={module}/{type} returned={result.Count} " +
                                  $"activeInScope={activeCodesInScope.Count}");
        });
    }
}
