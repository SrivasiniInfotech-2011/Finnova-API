using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;

namespace Finnova.Service.DocumentNumberControl.Internal;

public enum SchemeScopeLevel { LobAndBranch, Lob, Branch, Global }

public sealed record SchemeResolution(NumberingScheme Scheme, SchemeScopeLevel Level);

public static class SchemeResolver
{
    public static SchemeResolution? Resolve(IEnumerable<NumberingScheme> schemes, string documentType, Guid? lobId, Guid? branchId)
    {
        var dt = documentType?.Trim() ?? string.Empty;
        var active = schemes
            .Where(s => s.IsActive && string.Equals(s.DocumentType?.Trim(), dt, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (lobId is not null && branchId is not null)
        {
            var s = active.FirstOrDefault(x => x.Scope == NumberScope.Branch && x.ScopeId == branchId && x.LineOfBusinessId == lobId);
            if (s is not null) return new SchemeResolution(s, SchemeScopeLevel.LobAndBranch);
        }
        if (lobId is not null)
        {
            var s = active.FirstOrDefault(x => x.Scope == NumberScope.Global && x.ScopeId == null && x.LineOfBusinessId == lobId);
            if (s is not null) return new SchemeResolution(s, SchemeScopeLevel.Lob);
        }
        if (branchId is not null)
        {
            var s = active.FirstOrDefault(x => x.Scope == NumberScope.Branch && x.ScopeId == branchId && x.LineOfBusinessId == null);
            if (s is not null) return new SchemeResolution(s, SchemeScopeLevel.Branch);
        }
        var g = active.FirstOrDefault(x => x.Scope == NumberScope.Global && x.ScopeId == null && x.LineOfBusinessId == null);
        return g is null ? null : new SchemeResolution(g, SchemeScopeLevel.Global);
    }
}