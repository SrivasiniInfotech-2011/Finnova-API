using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Service.DocumentNumberControl.Internal;
using FsCheck.Xunit;

namespace Finnova.Tests.LoanAccounts;

public sealed class SchemeResolverProperties
{
    private static readonly Guid Lob = Guid.NewGuid();
    private static readonly Guid Branch = Guid.NewGuid();

    // level 0 LOB+Branch, 1 LOB only, 2 Branch only, 3 Global, 4 unrelated scope (other LOB and branch)
    private static NumberingScheme Make(int level, bool active, string documentType) => new()
    {
        Code = Guid.NewGuid().ToString("N")[..12],
        Name = "n",
        DocumentType = documentType,
        FormatTemplate = "{SEQ}",
        IsActive = active,
        Scope = level is 0 or 2 or 4 ? NumberScope.Branch : NumberScope.Global,
        ScopeId = level switch { 0 or 2 => Branch, 4 => Guid.NewGuid(), _ => null },
        LineOfBusinessId = level switch { 0 or 1 => Lob, 4 => Guid.NewGuid(), _ => null }
    };

    [Property(MaxTest = 200)] // Feature: application-account-creation, Property 6: Scheme resolution fallback
    public bool ReturnsFirstActiveSchemeInFallbackOrder(bool[] actives, byte[] levels, byte[] docs)
    {
        var n = new[] { actives.Length, levels.Length, docs.Length }.Min();
        var schemes = Enumerable.Range(0, n)
            .Select(i => Make(levels[i] % 5, actives[i], docs[i] % 2 == 0 ? "LoanAccount" : "Other")).ToList();

        var result = SchemeResolver.Resolve(schemes, "LoanAccount", Lob, Branch);

        NumberingScheme? expected = null;
        for (var level = 0; level < 4 && expected is null; level++)
        {
            var wanted = level;
            expected = schemes.FirstOrDefault(s => s.IsActive && s.DocumentType == "LoanAccount" && LevelOf(s) == wanted);
        }
        return result?.Scheme.Id == expected?.Id
            && (result is null || (result.Scheme.IsActive && result.Scheme.DocumentType == "LoanAccount"));
    }

    private static int LevelOf(NumberingScheme s) =>
        s.Scope == NumberScope.Branch && s.ScopeId == Branch && s.LineOfBusinessId == Lob ? 0
        : s.Scope == NumberScope.Global && s.ScopeId == null && s.LineOfBusinessId == Lob ? 1
        : s.Scope == NumberScope.Branch && s.ScopeId == Branch && s.LineOfBusinessId == null ? 2
        : s.Scope == NumberScope.Global && s.ScopeId == null && s.LineOfBusinessId == null ? 3 : 4;
}
