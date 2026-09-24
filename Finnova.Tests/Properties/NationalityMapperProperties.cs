using FsCheck;
using FsCheck.Xunit;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Service.Mappers;

namespace Finnova.Tests.Properties;

/// <summary>
/// Property-based test for the nationality mappers (Property 13). Mirrors the lookup
/// mapper property test conventions (FsCheck.Xunit v2, MaxTest >= 100, bounded-string
/// generators over a safe alphabet within the model max lengths of Code 10 / Name 100).
/// </summary>
public class NationalityMapperProperties
{
    private const string Alphabet =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_";

    private static Gen<char> AlphaNumChar() => Gen.Elements(Alphabet.ToCharArray());

    /// <summary>Non-empty string of length 1..max drawn from a safe alphabet.</summary>
    private static Gen<string> BoundedString(int max) =>
        Gen.Choose(1, max).SelectMany(len =>
            Gen.ArrayOf(len, AlphaNumChar()).Select(chars => new string(chars)));

    private static Gen<Nationality> NationalityGen() =>
        from code in BoundedString(10)
        from name in BoundedString(100)
        from active in Arb.Generate<bool>()
        select new Nationality
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            IsActive = active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

    private static Gen<NationalityAuditEntry> AuditEntryGen() =>
        from action in Gen.Elements(NationalityAuditAction.Create, NationalityAuditAction.Update)
        // OldName is null on Create and non-null on Update, but the mapper must preserve
        // either shape, so exercise both null and populated values across both actions.
        from oldName in Gen.OneOf(Gen.Constant<string?>(null), BoundedString(100).Select(s => (string?)s))
        from newName in BoundedString(100)
        from changedBy in BoundedString(200)
        select new NationalityAuditEntry
        {
            Id = Guid.NewGuid(),
            NationalityId = Guid.NewGuid(),
            Action = action,
            OldName = oldName,
            NewName = newName,
            ChangedBy = changedBy,
            ChangedAtUtc = DateTime.UtcNow,
        };

    // Feature: nationality-master-management, Property 13: Mapper preserves fields — for any
    // Nationality, ToResponse (and ToResponseList) preserves Id, Code, Name, IsActive,
    // CreatedAt, UpdatedAt; for any NationalityAuditEntry, ToResponse preserves Id,
    // NationalityId, the Action name string, OldName, NewName, ChangedBy, and ChangedAtUtc.
    [Property(MaxTest = 200)]
    public Property Property13_MapperPreservesFields()
    {
        var arb = Arb.From(
            from list in Gen.NonEmptyListOf(NationalityGen())
            from audit in AuditEntryGen()
            select (list.ToList(), audit));

        return Prop.ForAll(arb, tuple =>
        {
            var (nationalities, audit) = tuple;

            // Single-entity mapper preserves every scalar field.
            var single = nationalities[0];
            var response = single.ToResponse();
            var singlePreserves =
                response.Id == single.Id &&
                response.Code == single.Code &&
                response.Name == single.Name &&
                response.IsActive == single.IsActive &&
                response.CreatedAt == single.CreatedAt &&
                response.UpdatedAt == single.UpdatedAt;

            // List mapper preserves order and every field for each element.
            var listResponses = nationalities.ToResponseList();
            var listPreserves =
                listResponses.Count == nationalities.Count &&
                listResponses.Zip(nationalities, (r, x) =>
                    r.Id == x.Id &&
                    r.Code == x.Code &&
                    r.Name == x.Name &&
                    r.IsActive == x.IsActive &&
                    r.CreatedAt == x.CreatedAt &&
                    r.UpdatedAt == x.UpdatedAt).All(ok => ok);

            // Audit-entry mapper preserves every field; Action maps to its enum name string.
            var auditResponse = audit.ToResponse();
            var auditPreserves =
                auditResponse.Id == audit.Id &&
                auditResponse.NationalityId == audit.NationalityId &&
                auditResponse.Action == audit.Action.ToString() &&
                auditResponse.OldName == audit.OldName &&
                auditResponse.NewName == audit.NewName &&
                auditResponse.ChangedBy == audit.ChangedBy &&
                auditResponse.ChangedAtUtc == audit.ChangedAtUtc;

            return (singlePreserves && listPreserves && auditPreserves)
                .Label("ToResponse + ToResponseList + audit ToResponse preserve all fields");
        });
    }
}
