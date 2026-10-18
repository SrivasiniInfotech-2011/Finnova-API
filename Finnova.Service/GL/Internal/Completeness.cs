using Finnova.Models.Contracts.GL;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Interfaces;
using Finnova.Service.GL.Ports;

namespace Finnova.Service.GL.Internal;

/// <summary>
/// Computes System-GL configuration completeness per company (R7.1/7.4/7.8). Multi-currency
/// companies additionally require Realized + Unrealized FX GLs; single-currency companies do not.
/// </summary>
public static class Completeness
{
    public static async Task<SystemGLCompletenessResponse> ComputeAsync(
        IGLSystemAccountMappingRepository mappings, ICompanyQueryPort company,
        Guid companyId, CancellationToken ct)
    {
        var multi = await company.IsMultiCurrencyEnabledAsync(companyId, ct);

        var required = new List<SystemGLType>
        {
            SystemGLType.Suspense,
            SystemGLType.RoundOff,
            SystemGLType.InterBranchClearingHO,
            SystemGLType.InterBranchClearingBranch,
            SystemGLType.RetainedEarnings
        };
        if (multi)
        {
            required.Add(SystemGLType.RealizedFX);       // R7.8
            required.Add(SystemGLType.UnrealizedFX);
        }

        var active = (await mappings.GetActiveForCompanyAsync(companyId, ct))
            .Select(m => m.SystemGLType).ToHashSet();

        var missing = required.Where(t => !active.Contains(t)).ToList();
        return new SystemGLCompletenessResponse(companyId, missing.Count == 0, missing);
    }
}
