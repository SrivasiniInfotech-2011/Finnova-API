using Finnova.Data.Seeder.DemandSample;
using Xunit;

namespace Finnova.Tests.DemandProcessing;

public sealed class SampleDataGuardTests
{
    [Theory]
    [InlineData("Development", "Development")]
    [InlineData("development", "Development")]
    [InlineData("Testing", "Testing")]
    [InlineData("TESTING", "Testing")]
    public void AllowedEnvironmentsAreNormalized(string input, string expected) => Assert.Equal(expected, SampleDataEnvironmentGuard.Require(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("UAT")]
    public void EveryOtherEnvironmentIsRefused(string? input) => Assert.Throws<SampleDataEnvironmentException>(() => SampleDataEnvironmentGuard.Require(input));

    [Fact]
    public void RefusalMessageDoesNotEchoTheEnvironmentValue()
    {
        var ex = Assert.Throws<SampleDataEnvironmentException>(() => SampleDataEnvironmentGuard.Require("prod-secret-name"));
        Assert.DoesNotContain("prod-secret-name", ex.Message);
    }

    [Fact]
    public void CatalogReferenceLocationsMatchTheMigrationList()
    {
        var codes = SampleDataCatalog.Locations.Where(x => x.Level <= SampleDataCatalog.ReferenceLocationMaxLevel).Select(x => x.Code).OrderBy(x => x).ToArray();
        Assert.Equal(new[] { "CBE", "CHN", "MDU", "SLM", "TN", "TNJ", "TRI" }, codes);
    }

    [Fact]
    public void OptionsDefaultToTheFixedFixtureValues()
    {
        Assert.Equal("SAMPLE-DP-V1", new SampleDataOptions().SampleDataKey);
        Assert.Equal(new DateOnly(2026, 3, 31), new SampleDataOptions().SampleAsOfDate);
        Assert.Equal("Asia/Kolkata", new SampleDataOptions().BusinessTimezone);
    }
}