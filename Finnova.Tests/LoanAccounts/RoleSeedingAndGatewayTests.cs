using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Roles;
using Finnova.Service.Auth;
using Finnova.Service.LoanAccounts.Kyc;
using Microsoft.Extensions.Options;
using Xunit;

namespace Finnova.Tests.LoanAccounts;

public sealed class RoleSeedingAndGatewayTests
{
    [Fact]
    public void Token_service_emits_assigned_roles_as_claims()
    {
        var service = new TokenService(Options.Create(new JwtSettings { Secret = "unit-test-secret-key-that-is-long-enough-32", Issuer = "Finnova", Audience = "FinnovaClients" }));
        var (token, _) = service.GenerateToken(new UserAccount { Name = "U", Role = UserRole.User }, new[] { "KycApprover", "AccountsViewer" });
        var values = new JwtSecurityTokenHandler().ReadJwtToken(token).Claims.Select(c => c.Value).ToList();
        Assert.Contains("KycApprover", values);
        Assert.Contains("AccountsViewer", values);
        Assert.DoesNotContain("SystemAdmin", values);
    }

    [Fact]
    public void Original_token_overload_still_works_without_extra_roles()
    {
        var service = new TokenService(Options.Create(new JwtSettings { Secret = "unit-test-secret-key-that-is-long-enough-32" }));
        var (token, _) = service.GenerateToken(new UserAccount { Name = "U", Role = UserRole.User });
        Assert.False(string.IsNullOrEmpty(token));
    }

    [Fact]
    public void Seeding_creates_the_four_roles_exactly_once_even_when_applied_twice()
    {
        using var db = LoanAccountTestData.NewDb();
        db.Database.EnsureCreated();
        db.Database.EnsureCreated();
        Assert.Equal(AppRoleCatalog.Roles.Count, db.AppRoles.Count());
        foreach (var role in AppRoleCatalog.Roles) Assert.Single(db.AppRoles.Where(r => r.Name == role.Name));
    }

    [Theory]
    [InlineData(ValidationStatus.Success, KycCheckResult.Passed)]
    [InlineData(ValidationStatus.Failure, KycCheckResult.Failed)]
    [InlineData(ValidationStatus.Unavailable, KycCheckResult.Unavailable)]
    public void Validation_status_maps_to_check_result(ValidationStatus input, KycCheckResult expected)
        => Assert.Equal(expected, KycResultMapper.FromValidation(input));

    [Theory]
    [InlineData(ComparisonOutcome.Match, KycCheckResult.Passed)]
    [InlineData(ComparisonOutcome.NonMatch, KycCheckResult.Failed)]
    [InlineData(ComparisonOutcome.NoUsableMatch, KycCheckResult.NotChecked)]
    public void Comparison_outcome_maps_to_check_result(ComparisonOutcome input, KycCheckResult expected)
        => Assert.Equal(expected, KycResultMapper.FromComparison(input));

    private static JsonElement Gateway()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var path = Path.Combine(dir.FullName, "Finnova.ApiGateway", "appsettings.json");
            if (File.Exists(path)) return JsonDocument.Parse(File.ReadAllText(path)).RootElement.GetProperty("ReverseProxy").GetProperty("Routes");
            dir = dir.Parent;
        }
        throw new FileNotFoundException("Finnova.ApiGateway/appsettings.json was not found above the test output folder.");
    }

    [Fact]
    public void Gateway_forwards_loan_account_routes_to_the_accounts_cluster_with_the_alias_rewrite()
    {
        var routes = Gateway();
        foreach (var name in new[] { "loan-accounts-route", "loan-accounts-root", "ua-loan-accounts-alias-route", "ua-loan-accounts-alias-root" })
            Assert.Equal("accounts-cluster", routes.GetProperty(name).GetProperty("ClusterId").GetString());
        Assert.Equal("/api/ua/api/loan-accounts/{**catch-all}", routes.GetProperty("ua-loan-accounts-alias-route").GetProperty("Match").GetProperty("Path").GetString());
        Assert.Contains("PathRemovePrefix", routes.GetProperty("ua-loan-accounts-alias-route").GetProperty("Transforms").ToString());
    }

    [Fact]
    public void Gateway_has_no_route_to_internal_endpoints_and_never_sends_accounts_to_systemadmin()
    {
        foreach (var route in Gateway().EnumerateObject())
        {
            var path = route.Value.GetProperty("Match").GetProperty("Path").GetString() ?? string.Empty;
            Assert.DoesNotContain("/internal", path, StringComparison.OrdinalIgnoreCase);
            if (path.Contains("loan-accounts", StringComparison.OrdinalIgnoreCase))
                Assert.NotEqual("systemadmin-cluster", route.Value.GetProperty("ClusterId").GetString());
        }
    }
}
