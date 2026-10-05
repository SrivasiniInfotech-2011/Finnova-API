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
    public static (List<AccessRightRow> Rows, List<BranchSelection> Branches) Merge(
        (IEnumerable<AccessRightRow> Rows, IEnumerable<BranchSelection> Branches) current,
        (IEnumerable<AccessRightRow> Rows, IEnumerable<BranchSelection> Branches) source)
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

        // De-dup branches by a stable key: ALL collapses to one; others by their LocationId Guid.
        var branches = new List<BranchSelection>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var b in current.Branches.Concat(source.Branches))
        {
            var key = b.IsAll ? "ALL" : b.LocationId?.ToString();
            if (key is null || !seen.Add(key))
                continue;
            branches.Add(b);
        }

        return (byRole.Values.ToList(), branches);
    }
}
