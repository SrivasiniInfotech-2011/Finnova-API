using Finnova.Models.Contracts.DraweeBanks;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Exceptions;

namespace Finnova.Service.DraweeBank;

/// <summary>
/// Shared aggregate helpers used by the create and update handlers: in-collection place-code
/// uniqueness (R3.3) and mapping branch/restriction requests to owned entities.
/// </summary>
public static class DraweeBankAssembler
{
    /// <summary>Rejects a submitted branch set that contains two place codes equal after trimming
    /// and case-insensitive comparison (R3.3).</summary>
    public static void EnsureUniquePlaceCodes(IReadOnlyList<DraweeBranchRequest> branches)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var b in branches)
        {
            if (!seen.Add(b.PlaceCode.Trim()))
                throw new DraweeBranchDuplicatePlaceCodeException();
        }
    }

    public static List<DraweeBranch> ToBranchEntities(Guid bankId, IReadOnlyList<DraweeBranchRequest> branches)
        => branches.Select(b => new DraweeBranch
        {
            DraweeBankId = bankId,
            PlaceCode = b.PlaceCode.Trim(),
            PlaceName = b.PlaceName,
            Address = b.Address,
            PostalCode = b.PostalCode,
            StartDate = b.StartDate,
            EndDate = b.EndDate
        }).ToList();

    public static RestrictionDetail? ToRestrictionEntity(Guid bankId, RestrictionDetailRequest? r)
        => r == null ? null : new RestrictionDetail
        {
            DraweeBankId = bankId,
            ClearingDays = r.ClearingDays,
            StartDate = r.StartDate,
            EndDate = r.EndDate
        };
}
