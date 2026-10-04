using Finnova.Models.Contracts.UserManagement;

namespace Finnova.Service.UserManagement.Helpers;

/// <summary>
/// Copy-Profile merge (R10.2): append source rows/branches to current and de-dup. Duplicate access
/// rows (same RoleCode) collapse to one row whose Add/Modify/Query/Delete flags are the logical OR
/// of the duplicates; duplicate branches collapse to one. Pure, commutative on flags, idempotent —
/// property-tested (P3, P4).
/// </summary>
public static class AccessAssignmentMerger
{
    public static (List<AccessRightRow> Rows, List<string> Branches) Merge(
        (IEnumerable<AccessRightRow> Rows, IEnumerable<string> Branches) current,
        (IEnumerable<AccessRightRow> Rows, IEnumerable<string> Branches) source)
    {
        var byRole = new Dictionary<string, AccessRightRow>(StringComparer.OrdinalIgnoreCase);

        foreach (var r in current.Rows.Concat(source.Rows))
        {
            if (byRole.TryGetValue(r.RoleCode, out var existing))
            {
                byRole[r.RoleCode] = existing with
                {
                    CanAdd = existing.CanAdd || r.CanAdd,
                    CanModify = existing.CanModify || r.CanModify,
                    CanQuery = existing.CanQuery || r.CanQuery,
                    CanDelete = existing.CanDelete || r.CanDelete,
                };
            }
            else
            {
                byRole[r.RoleCode] = r;
            }
        }

        var branches = current.Branches
            .Concat(source.Branches)
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return (byRole.Values.ToList(), branches);
    }
}
