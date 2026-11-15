using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Context;
using Finnova.Service.Auth;
using Finnova.Tests.Integration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Finnova.Tests.LoanAccounts;

public sealed class InternalEndpointsTests : IClassFixture<SystemAdminAppFactory>
{
    private static readonly Guid ClientId = new("00000000-0000-0000-0004-000000000001");
    private const string Secret = "unit-test-service-secret";
    private static readonly Guid LobId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private readonly SystemAdminAppFactory _factory;

    public InternalEndpointsTests(SystemAdminAppFactory factory)
    {
        _factory = factory;
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FinnovaDbContext>();
        if (db.ServiceCredentials.Any(x => x.ClientId == ClientId)) return;
        db.ServiceCredentials.Add(new ServiceCredential { ClientId = ClientId, SecretHash = PasswordHasher.Hash(Secret), IsActive = true });
        db.LinesOfBusiness.Add(new LineOfBusiness { Id = LobId, LOB_Name = "Retail Lending", LOB_Description = "d", IsActive = true });
        db.Locations.Add(new Location { Id = BranchId, Code = "MUM01", Name = "Mumbai", Level = 5, IsActive = true });
        db.NumberingSchemes.Add(new NumberingScheme
        {
            Code = "LOAN-ACCT-TEST",
            Name = "Loan Account",
            DocumentType = "LoanAccount",
            FormatTemplate = "{LOB}-{BRANCH}-{SEQ:4}",
            SeqPadding = 4,
            Scope = NumberScope.Branch,
            ScopeId = BranchId,
            LineOfBusinessId = LobId,
            IsActive = true
        });
        db.SaveChanges();
    }

    private async Task<HttpClient> ServiceClientAsync()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/internal/service-auth/token", new { clientId = ClientId, clientSecret = Secret });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static object IssueBody(string documentType = "LoanAccount", Guid? lob = null, Guid? branch = null) =>
        new { documentType, lineOfBusinessId = lob ?? LobId, branchId = branch ?? BranchId, correlationId = "c1" };

    [Fact]
    public async Task Issue_without_a_token_returns_401()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().PostAsJsonAsync("/internal/dcn/issue", IssueBody())).StatusCode);

    [Fact]
    public async Task Issue_rejects_a_user_token_without_the_service_permission()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", DevJwt.SystemAdmin());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/internal/dcn/issue", IssueBody())).StatusCode);
    }

    [Fact]
    public async Task Token_endpoint_rejects_a_wrong_secret()
        => Assert.Equal(HttpStatusCode.Unauthorized,
            (await _factory.CreateClient().PostAsJsonAsync("/internal/service-auth/token", new { clientId = ClientId, clientSecret = "wrong" })).StatusCode);

    [Fact]
    public async Task Service_token_issues_a_formatted_number_from_the_resolved_scheme()
    {
        var client = await ServiceClientAsync();
        var first = await client.PostAsJsonAsync("/internal/dcn/issue", IssueBody());
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var body = await first.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("RETAILLEND-MUM01-0001", body.GetProperty("issuedNumber").GetString());
        Assert.Equal("LOAN-ACCT-TEST", body.GetProperty("resolvedSchemeCode").GetString());
        Assert.Equal("LobAndBranch", body.GetProperty("resolvedScopeLevel").GetString());

        var second = await (await client.PostAsJsonAsync("/internal/dcn/issue", IssueBody())).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("RETAILLEND-MUM01-0002", second.GetProperty("issuedNumber").GetString());
    }

    [Fact]
    public async Task Unknown_document_type_returns_404_and_unknown_branch_returns_400()
    {
        var client = await ServiceClientAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/internal/dcn/issue", IssueBody("NoSuchType"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/internal/dcn/issue", IssueBody(branch: Guid.NewGuid()))).StatusCode);
    }

    [Fact]
    public async Task Internal_endpoints_are_hidden_when_forwarded_headers_are_present()
    {
        var client = await ServiceClientAsync();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.0.0.1");
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/internal/dcn/issue", IssueBody())).StatusCode);
    }

    [Fact]
    public async Task Kyc_verify_rejects_unauthenticated_callers()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().PostAsJsonAsync("/internal/kyc/verify", new { })).StatusCode);
}
