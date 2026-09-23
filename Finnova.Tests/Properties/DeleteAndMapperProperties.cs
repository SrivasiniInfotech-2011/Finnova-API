using FsCheck;
using FsCheck.Xunit;
using Finnova.Models.Domain.Entities;
using Finnova.Service.Lookup.Commands.DeleteLookupValue;
using Finnova.Service.Lookup.Queries.GetLookupDropdownItems;
using Finnova.Service.Lookup.Queries.GetLookupValuesPaged;
using Finnova.Service.Mappers;
using Finnova.Tests.Infrastructure;

namespace Finnova.Tests.Properties;

/// <summary>
/// Property-based tests for delete (Property 10) and the mapper (Property 12).
/// </summary>
public class DeleteAndMapperProperties
{
    // Feature: lookup-master-management, Property 10: Deleting a non-locked value removes it from
    // all queries — after deletion the value no longer appears in the paged list or the dropdown
    // result for its scope.
    [Property(MaxTest = 200)]
    public Property Property10_DeleteRemovesFromAllQueries()
    {
        var arb = Arb.From(
            from target in LookupGenerators.ArbUnlockedValue().Generator
            from others in LookupGenerators.DatasetGen()
            select (target, others));

        return Prop.ForAll(arb, tuple =>
        {
            var (target, others) = tuple;

            // Ensure the target is present and its (Module,Type,Code) is unique in the store.
            var rows = others
                .Where(o => !(o.Module == target.Module &&
                              o.LookupType == target.LookupType &&
                              o.Code == target.Code) && o.Id != target.Id)
                .Append(target)
                .ToList();

            var repo = new InMemoryLookupRepository(rows);

            var deleteHandler = new DeleteLookupValueCommandHandler(repo);
            deleteHandler.Handle(new DeleteLookupValueCommand(target.Id), CancellationToken.None)
                .GetAwaiter().GetResult();

            // Paged list for the scope must not contain the target Id.
            var pagedHandler = new GetLookupValuesPagedQueryHandler(repo);
            var paged = pagedHandler.Handle(
                new GetLookupValuesPagedQuery(target.Module, target.LookupType, null, 1, 500),
                CancellationToken.None).GetAwaiter().GetResult();
            var notInPaged = paged.Data.All(x => x.Id != target.Id);

            // Dropdown for the scope must not contain the target Code+Value pair. If the target was
            // the only row in its scope, the scope no longer exists after deletion and the handler
            // correctly rejects with an unknown-scope error; in that case the value is trivially
            // absent from any dropdown, so treat that as satisfied.
            var dropdownHandler = new GetLookupDropdownItemsQueryHandler(repo);
            bool notInDropdown;
            try
            {
                var dropdown = dropdownHandler.Handle(
                    new GetLookupDropdownItemsQuery(target.Module, target.LookupType),
                    CancellationToken.None).GetAwaiter().GetResult();
                notInDropdown = dropdown.All(x => !(x.Code == target.Code && x.Label == target.Value));
            }
            catch (FluentValidation.ValidationException)
            {
                notInDropdown = true; // scope gone => target cannot appear
            }

            var gone = repo.GetByIdAsync(target.Id, CancellationToken.None).GetAwaiter().GetResult() is null;

            return (notInPaged && notInDropdown && gone)
                .Label("deleted value absent from paged, dropdown, and by-id");
        });
    }

    // Feature: lookup-master-management, Property 12: Mapper preserves fields — ToResponse preserves
    // IsSystemLocked and all scalar fields; ToDropdownItem preserves Code and the label (Value).
    [Property(MaxTest = 200)]
    public Property Property12_MapperPreservesFields()
    {
        var arb = Arb.From(
            Gen.OneOf(
                LookupGenerators.ArbLockedValue().Generator,
                LookupGenerators.ArbUnlockedValue().Generator));

        return Prop.ForAll(arb, (LookupValue x) =>
        {
            var response = x.ToResponse();
            var dropdown = x.ToDropdownItem();

            var responsePreserves =
                response.Id == x.Id &&
                response.Module == x.Module &&
                response.LookupType == x.LookupType &&
                response.Code == x.Code &&
                response.Value == x.Value &&
                response.DisplayOrder == x.DisplayOrder &&
                response.IsActive == x.IsActive &&
                response.IsSystemLocked == x.IsSystemLocked &&
                response.CreatedAt == x.CreatedAt &&
                response.UpdatedAt == x.UpdatedAt;

            var dropdownPreserves =
                dropdown.Code == x.Code &&
                dropdown.Label == x.Value;

            return (responsePreserves && dropdownPreserves)
                .Label("ToResponse + ToDropdownItem preserve all fields");
        });
    }
}
