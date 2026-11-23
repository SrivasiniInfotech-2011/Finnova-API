using Xunit;

namespace Finnova.Tests.DemandProcessing;

/// <summary>Fails if sample-data code or a reference to the data tool reaches a production project.</summary>
public sealed class SampleDataIsolationTests
{
    private static readonly string[] ForbiddenTokens = ["Finnova.Data", "DemandSample", "UseSampleAdapters", "SeedSampleDataOnStartup"];

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Finnova.Backend.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Solution root was not found.");
    }

    [Theory]
    [InlineData("Finnova.AccountsService")]
    [InlineData("Finnova.UAService")]
    [InlineData("Finnova.SystemAdminService")]
    [InlineData("Finnova.ApiGateway")]
    [InlineData("Finnova.Service")]
    [InlineData("Finnova.Repository")]
    [InlineData("Finnova.Models")]
    public void ProductionProjectsContainNoSampleDataCodeOrReference(string project)
    {
        var root = Path.Combine(RepoRoot(), project);
        var sep = Path.DirectorySeparatorChar;
        var files = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(f => (f.EndsWith(".cs") || f.EndsWith(".csproj") || f.EndsWith(".json")) && !f.Contains($"{sep}bin{sep}") && !f.Contains($"{sep}obj{sep}"))
            .ToArray();
        Assert.NotEmpty(files);
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            foreach (var token in ForbiddenTokens) Assert.False(text.Contains(token, StringComparison.Ordinal), $"{Path.GetRelativePath(RepoRoot(), file)} mentions '{token}'.");
        }
    }
}