using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace Finnova.Tests.Integration;

/// <summary>
/// Task 8.1 — Authorization integration tests for the SystemAdmin host (Requirement 7).
///
/// Uses dev-signed JWTs (<see cref="DevJwt"/>) because UA token issuance does not exist yet
/// (design A2). The host runs against an EF Core InMemory database (see
/// <see cref="SystemAdminAppFactory"/>) so authorization can be exercised without SQL Server.
/// </summary>
public class AuthorizationTests : IClassFixture<SystemAdminAppFactory>
{
    private readonly SystemAdminAppFactory _factory;

    public AuthorizationTests(SystemAdminAppFactory factory) => _factory = factory;

    private HttpClient Client() => _factory.CreateClient();

    private static void SetBearer(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    // R7.1 — missing token -> 401
    [Fact]
    public async Task GetLookup_WithoutToken_Returns401()
    {
        var client = Client();

        var response = await client.GetAsync("/api/lookup");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // R7.1 / R7.5 — expired token -> 401
    [Fact]
    public async Task GetLookup_WithExpiredToken_Returns401()
    {
        var client = Client();
        SetBearer(client, DevJwt.Expired());

        var response = await client.GetAsync("/api/lookup");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // R7.1 — tampered / garbage signature -> 401
    [Fact]
    public async Task GetLookup_WithTamperedToken_Returns401()
    {
        var client = Client();
        SetBearer(client, DevJwt.Tampered());

        var response = await client.GetAsync("/api/lookup");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // R7.2 — authenticated but lacking SystemAdmin role -> 403 on a write endpoint
    [Fact]
    public async Task PostLookup_WithNonAdminToken_Returns403()
    {
        var client = Client();
        SetBearer(client, DevJwt.NoRole());

        var body = new
        {
            Module = "Origination",
            LookupType = "MARITAL_STATUS",
            Code = "NEW",
            Value = "New Value",
            DisplayOrder = 5,
            IsActive = true
        };

        var response = await client.PostAsJsonAsync("/api/lookup", body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // R7.3 — valid SystemAdmin token -> 200 on the admin list
    [Fact]
    public async Task GetLookup_WithSystemAdminToken_Returns200()
    {
        var client = Client();
        SetBearer(client, DevJwt.SystemAdmin());

        var response = await client.GetAsync("/api/lookup");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // R7.4 — dropdown allows any authenticated caller (non-admin) -> 200
    [Fact]
    public async Task GetDropdown_WithNonAdminAuthenticatedToken_Returns200()
    {
        var client = Client();
        SetBearer(client, DevJwt.NoRole());

        var response = await client.GetAsync(
            "/api/lookup/dropdown?module=Origination&type=MARITAL_STATUS");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // R7.5 — dropdown without a token -> 401
    [Fact]
    public async Task GetDropdown_WithoutToken_Returns401()
    {
        var client = Client();

        var response = await client.GetAsync(
            "/api/lookup/dropdown?module=Origination&type=MARITAL_STATUS");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
