using Finnova.Models.Contracts.UserManagement;
using Finnova.Service.UserManagement.Helpers;
using Finnova.Service.UserManagement.Internal;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace Finnova.Tests.Properties;

/// <summary>
/// Property-based tests over the pure User Management helpers (FsCheck.Xunit, >=100 iterations).
/// English-only inputs; no bilingual/Arabic/RTL generators.
/// </summary>
public class UserManagementHelperProperties
{
    // Feature: user-management, Property 1: Generated codes satisfy the format invariant and are unique. (R2.1-2.5)
    [Property(MaxTest = 100)]
    public Property GeneratedCode_MatchesFormat_AndIsNotTaken(NonEmptyString source, int takenSeed)
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < Math.Abs(takenSeed) % 5; i++) taken.Add("U" + i);

        var code = UserCodeGenerator.Generate(source.Get, taken.Contains);

        var formatOk = code.Length is >= 4 and <= 6
            && char.IsLetter(code[0])
            && code.All(char.IsLetterOrDigit)
            && code == code.ToUpperInvariant();
        return (formatOk && !taken.Contains(code)).ToProperty();
    }

    // Feature: user-management, Property 2: Role Code is a deterministic pure function. (R8.4)
    [Property(MaxTest = 100)]
    public Property RoleCode_IsUppercaseConcatenation_AndDeterministic(NonNull<string> rc, NonNull<string> prog)
    {
        var a = RoleCodeBuilder.Build(rc.Get, prog.Get);
        var b = RoleCodeBuilder.Build(rc.Get, prog.Get);
        var expected = (rc.Get + prog.Get).ToUpperInvariant();
        return (a == expected && a == b).ToProperty();
    }

    // Feature: user-management, Property 3: Copy-Profile merge is OR-merge + de-dup union. (R10.2)
    [Property(MaxTest = 100)]
    public Property Merge_OrsFlags_AndDeDupsRolesAndBranches(int seed)
    {
        var r = new System.Random(seed);
        AccessRightRow Row(string code, bool a, bool m, bool q, bool d)
            => new(code, "RC", Guid.Empty, "P", "P", a, m, q, d);

        var loc1 = new BranchSelection(Guid.NewGuid(), false, "B1");
        var loc2 = new BranchSelection(Guid.NewGuid(), false, "B2");

        var current = (new[] { Row("X", true, false, false, false) }.AsEnumerable(),
                       new[] { loc1 }.AsEnumerable());
        var source = (new[] { Row("X", false, true, false, false), Row("Y", true, false, false, false) }.AsEnumerable(),
                      new[] { loc1, loc2 }.AsEnumerable());

        var (rows, branches) = AccessAssignmentMerger.Merge(current, source);

        var x = rows.Single(r2 => r2.RoleCode == "X");
        var merged = x.CanAdd && x.CanModify && rows.Count == 2 && branches.Count == 2;
        return merged.ToProperty();
    }

    // Feature: user-management, Property 4: Copy-Profile merge is idempotent. (R10.2)
    [Property(MaxTest = 100)]
    public Property Merge_WithItself_IsIdempotent(bool a, bool m, bool q, bool d)
    {
        var rows = new[] { new AccessRightRow("X", "RC", Guid.Empty, "P", "P", a, m, q, d) }.AsEnumerable();
        var branches = new[] { new BranchSelection(Guid.NewGuid(), false, "B1") }.AsEnumerable();

        var (r1, b1) = AccessAssignmentMerger.Merge((rows, branches), (rows, branches));
        return (r1.Count == 1 && b1.Count == 1
            && r1[0].CanAdd == a && r1[0].CanModify == m && r1[0].CanQuery == q && r1[0].CanDelete == d).ToProperty();
    }

    // Feature: user-management, Property 5: Mobile/email validation accepts exactly the format rules. (R4.1-4.5)
    [Property(MaxTest = 100)]
    public Property Email_AcceptedIffFormatRulesHold(NonNull<string> s)
    {
        var email = s.Get;
        var accepted = EmailFormat.IsValid(email);
        var expected = !string.IsNullOrEmpty(email)
            && email.Length <= 60
            && email.Count(c => c == '@') == 1
            && email.Contains('.')
            && char.IsLetterOrDigit(email[0])
            && char.IsLetterOrDigit(email[^1]);
        return (accepted == expected).ToProperty();
    }
}
