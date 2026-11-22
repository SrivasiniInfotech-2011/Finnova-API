using Finnova.Models.Contracts.AppropriationLogic;
using Finnova.Service.AppropriationLogic.Ports;
using Finnova.Service.AppropriationLogic.Rules;

namespace Finnova.Service.AppropriationLogic.Services;

public sealed class AppropriationRuleResolver(IAppropriationLogicRepository repository) : IAppropriationRuleResolver
{
    public async Task<AppropriationResolutionResult> ResolveAsync(Guid companyId, Guid lineOfBusinessId, CancellationToken ct)
    {
        var candidates = await repository.GetActiveCandidatesAsync(companyId, lineOfBusinessId, ct);
        try
        {
            var resolution = AppropriationLogicPrecedence.Resolve(companyId, lineOfBusinessId, candidates);
            var rule = resolution.Rule;
            return new(AppropriationResolutionStatus.Resolved, new AppropriationResolution(rule, resolution.UsedGlobalFallback), "1.0", null);
        }
        catch (Models.Domain.Exceptions.AppropriationLogicNotFoundException)
        {
            return new(AppropriationResolutionStatus.NotFound, null, "1.0", "ERR-APPROP-RULE-NOT-FOUND");
        }
        catch (Models.Domain.Exceptions.AppropriationLogicConflictException)
        {
            return new(AppropriationResolutionStatus.Ambiguous, null, "1.0", "ERR-APPROP-RULE-AMBIGUOUS");
        }
    }
}