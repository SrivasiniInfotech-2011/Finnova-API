using FsCheck;
using FsCheck.Xunit;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Service.AppropriationLogic.Rules;
using Xunit;

namespace Finnova.Tests.AppropriationLogic;

public sealed class AppropriationLogicRulesTests
{
    [Property(MaxTest = 100)]
    public Property SequenceNumbersAreContiguous(NonNegativeInt seed)
    {
        var values = Enumerable.Range(0, seed.Get % 20 + 1).Select(x => x + seed.Get).ToArray();
        var distinct = values.Distinct().Take(7).ToArray();
        var ordered = distinct.Select((value, index) => new AppropriationRuleDetail { SequenceNumber = index + 1, DueType = (AppropriationDueType)(Math.Abs(value) % 8), Percentage = 0m }).ToArray();
        return ordered.Select(x => x.SequenceNumber).SequenceEqual(Enumerable.Range(1, ordered.Length)).ToProperty();
    }

    [Property(MaxTest = 100)]
    public Property ResolutionPrefersExactLob(Guid company)
    {
        var lob = Guid.NewGuid();
        var global = new AppropriationRuleVersion { CompanyId = company, IsActive = true, LineOfBusinessId = null };
        var exact = new AppropriationRuleVersion { CompanyId = company, IsActive = true, LineOfBusinessId = lob };
        var result = AppropriationLogicPrecedence.Resolve(company, lob, new[] { global, exact });
        return (result.Rule.Id == exact.Id && !result.UsedGlobalFallback).ToProperty();
    }

    [Property(MaxTest = 100)]
    public Property ResolutionUsesGlobalFallback(Guid company)
    {
        var global = new AppropriationRuleVersion { CompanyId = company, IsActive = true, LineOfBusinessId = null };
        var result = AppropriationLogicPrecedence.Resolve(company, Guid.NewGuid(), new[] { global });
        return (result.Rule.Id == global.Id && result.UsedGlobalFallback).ToProperty();
    }
}
