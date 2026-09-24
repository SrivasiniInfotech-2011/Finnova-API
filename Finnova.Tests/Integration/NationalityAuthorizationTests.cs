using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace Finnova.Tests.Integration;

/// <summary>
/// Task 13.1 — Authorization integration tests for the Nationality Master endpoints
/// (Requirements 6.1, 6.2, 6.4).
///
/// Every nationality endpoint is <c>[Authorize(Policy = "SystemAdmin")]</c>, so:
/// <list type="bullet">
///   <item>R6.1 — a missing, expired, or invalid/tampered token yields 401 on every endpoint.</item>
///   <item>R6.2 — a valid token lacking the SystemAdmin role yields 403 on every endpoint.</item>
///   <item>R6.4 — authentication is enforced before authorization: an invalid token that also
///     lacks the role is rejected with 401 (not 403).</item>
/// </list>
///
/// Uses dev-signed JWTs (<see cref="DevJwt"/>) because UA token issuance does not exist yet.
/// The host runs against an EF Core InMemory database (see <see cref="SystemAdminAppFactory"/>)
/// so authorization can be exercised without SQL Server.
/// </summary>
public class NationalityAuthorizationTests : IClassFixture<SystemAdminAppFactory>
{
    private readonly SystemAdminAppFactory _factory;

    public NationalityAuthorizationTests(SystemAdminAppFactory factory) => _factory = factory;

    private HttpClient Client() => _factory.CreateClient();

    private static void SetBearer(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static readonly Guid SampleId = Guid.NewGuid();

    // Each nationality endpoint under test, as an HTTP request factory so every auth scenario
    // can be replayed against all four endpoints (GET list, POST create, PUT rename, GET audit).
    public static IEnumerable<object[]> Endpoints()
    {
        yield return new object[] { "GET list", (Func<HttpRequestMessage>)(() =>
            new HttpRequestMessage(HttpMethod.Get, "/api/nationality")) };

        yield return new object[] { "POST create", (Func<HttpRequestMessage>)(() =>
            new HttpRequestMessage(HttpMethod.Post, "/api/nationality")
            {
                Content = JsonContent.Create(new { Code = "IN", Name = "Indian", IsActive = true })
            }) };

        yield return new object[] { "PUT rename", (Func<HttpRequestMessage>)(() =>
            new HttpRequestMessage(HttpMethod.Put, $"/api/nationality/{SampleId}")
            {
                Content = JsonContent.Create(new { Name = "Renamed" })
            }) };

        yield return new object[] { "GET audit", (Func<HttpRequestMessage>)(() =>
            new HttpRequestMessage(HttpMethod.Get, $"/api/nationality/{SampleId}/audit")) };
    }

    // R6.1 — missing token -> 401 on every endpoint.
    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Endpoint_WithoutToken_Returns401(string name, Func<HttpRequestMessage> request)
    {
        _ = name;
        var client = Client();

        var response = await client.SendAsync(request());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // R6.1 — expired token -> 401 on every endpoint.
    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Endpoint_WithExpiredToken_Returns401(string name, Func<HttpRequestMessage> request)
    {
        _ = name;
        var client = Client();
        SetBearer(client, DevJwt.Expired());

        var response = await client.SendAsync(request());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // R6.1 — invalid / tampered signature -> 401 on every endpoint.
    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Endpoint_WithTamperedToken_Returns401(string name, Func<HttpRequestMessage> request)
    {
        _ = name;
        var client = Client();
        SetBearer(client, DevJwt.Tampered());

        var response = await client.SendAsync(request());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // R6.2 — valid, authenticated, but lacking the SystemAdmin role -> 403 on every endpoint.
    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Endpoint_WithNonAdminToken_Returns403(string name, Func<HttpRequestMessage> request)
    {
        _ = name;
        var client = Client();
        SetBearer(client, DevJwt.NoRole());

        var response = await client.SendAsync(request());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // R6.4 — authentication is enforced before authorization: a token that is BOTH invalid
    // (bad signature) AND would lack the SystemAdmin role is rejected with 401, not 403.
    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Endpoint_WithInvalidAndRolelessToken_Returns401Not403(
        string name, Func<HttpRequestMessage> request)
    {
        _ = name;
        var client = Client();
        // Tampered() corrupts a token's signature; the principal never authenticates, so the role
        // is never evaluated. The challenge must be 401 (authentication) rather than 403.
        SetBearer(client, DevJwt.Tampered());

        var response = await client.SendAsync(request());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
