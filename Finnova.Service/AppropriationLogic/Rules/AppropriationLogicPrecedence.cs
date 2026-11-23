using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Exceptions;
using Finnova.Service.AppropriationLogic.Ports;

namespace Finnova.Service.AppropriationLogic.Rules;

public static class AppropriationLogicPrecedence
{
    public static AppropriationResolution Resolve(Guid companyId, Guid lineOfBusinessId, IReadOnlyList<AppropriationRuleVersion> candidates)
    {
        var scoped = candidates.Where(x => x.CompanyId == companyId && x.IsActive).ToArray();
        var exact = scoped.Where(x => x.LineOfBusinessId == lineOfBusinessId).ToArray();
        if (exact.Length > 1) throw new AppropriationLogicConflictException(AppropriationLogicErrorCodes.Ambiguous, "Multiple active appropriation rules match the Line of Business.");
        if (exact.Length == 1) return new AppropriationResolution(exact[0], false);
        var global = scoped.Where(x => x.LineOfBusinessId is null).ToArray();
        if (global.Length > 1) throw new AppropriationLogicConflictException(AppropriationLogicErrorCodes.Ambiguous, "Multiple active global appropriation rules match the Company.");
        if (global.Length == 1) return new AppropriationResolution(global[0], true);
        throw new AppropriationLogicNotFoundException("No active appropriation rule is configured for the Company and Line of Business.");
    }
}