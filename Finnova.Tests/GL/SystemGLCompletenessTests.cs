using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Interfaces;
using Finnova.Service.GL.Internal;
using Finnova.Service.GL.Ports;
using Moq;
using Xunit;

namespace Finnova.Tests.GL;

public class SystemGLCompletenessTests
{
    private static Mock<IGLSystemAccountMappingRepository> MappingsWith(Guid company, params SystemGLType[] types)
    {
        var repo = new Mock<IGLSystemAccountMappingRepository>();
        repo.Setup(r => r.GetActiveForCompanyAsync(company, default))
            .ReturnsAsync(types.Select(t => new GLSystemAccountMapping
            { CompanyId = company, SystemGLType = t, IsActive = true }).ToList());
        return repo;
    }

    // R7.8 — single-currency: five mappings complete the set.
    [Fact]
    public async Task SingleCurrency_complete_without_fx()
    {
        var company = Guid.NewGuid();
        var mappings = MappingsWith(company,
            SystemGLType.Suspense, SystemGLType.RoundOff, SystemGLType.InterBranchClearingHO,
            SystemGLType.InterBranchClearingBranch, SystemGLType.RetainedEarnings);
        var companyPort = new Mock<ICompanyQueryPort>();
        companyPort.Setup(c => c.IsMultiCurrencyEnabledAsync(company, default)).ReturnsAsync(false);

        var result = await Completeness.ComputeAsync(mappings.Object, companyPort.Object, company, default);
        Assert.True(result.IsComplete);
    }

    // R7.8 — multi-currency: FX GLs are additionally required.
    [Fact]
    public async Task MultiCurrency_requires_fx()
    {
        var company = Guid.NewGuid();
        var mappings = MappingsWith(company,
            SystemGLType.Suspense, SystemGLType.RoundOff, SystemGLType.InterBranchClearingHO,
            SystemGLType.InterBranchClearingBranch, SystemGLType.RetainedEarnings);
        var companyPort = new Mock<ICompanyQueryPort>();
        companyPort.Setup(c => c.IsMultiCurrencyEnabledAsync(company, default)).ReturnsAsync(true);

        var result = await Completeness.ComputeAsync(mappings.Object, companyPort.Object, company, default);
        Assert.False(result.IsComplete);
        Assert.Contains(SystemGLType.RealizedFX, result.MissingOrInvalid);
        Assert.Contains(SystemGLType.UnrealizedFX, result.MissingOrInvalid);
    }
}
