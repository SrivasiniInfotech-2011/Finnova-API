using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace Finnova.Tests.Integration;

/// <summary>
/// Authorization integration tests for the Organization Hierarchy endpoints (R7).
///
/// Every endpoint is <c>[Authorize(Policy = "SystemAdmin")]</c>, so:
/// <list type="bullet">
///   <item>R7.1 — a missing, expired, or tampered token yields 401 on every endpoint.</item>
///   <item>R7.2 — a valid token lacking the SystemAdmin role yields 403 on every endpoint.</item>
///   <item>R7.4 — authentication is enforced before authorization: an invalid token that also
///     lacks the role is rejected with 401 (not 403).</item>
/// </list>
///
/// Uses dev-signed JWTs (<see cref="DevJwt"/>) against the in-memory host
/// (<see cref="SystemAdminAppFactory"/>).
/// </summary>
public class OrgHierarchyAuthorizationTests : IClassFixture<SystemAdminAppFactory>
{
    private readonly SystemAdminAppFactory _factory;

    public OrgHierarchyAuthorizationTests(SystemAdminAppFactory factory) => _factory = factory;

    private HttpClient Client() => _factory.CreateClient();

    private static void SetBearer(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static readonly Guid SampleId = Guid.NewGuid();

    // Every org-hierarchy endpoint as a request factory so each auth scenario can be replayed
    // across all of them (GET list, GET tree, GET children, POST create, PUT update, DELETE, GET audit).
    public static IEnumerable<object[]> Endpoints()
    {
        yield return new object[] { "GET list", (Func<HttpRequestMessage>)(() =>
            new HttpRequestMessage(HttpMethod.Get, "/api/orghierarchy")) };

        yield return new object[] { "GET tree", (Func<HttpRequestMessage>)(() =>
            new HttpRequestMessage(HttpMethod.Get, "/api/orghierarchy/tree")) };

        yield return new object[] { "GET children", (Func<HttpRequestMessage>)(() =>
            new HttpRequestMessage(HttpMethod.Get, $"/api/orghierarchy/{SampleId}/children")) };

        yield return new object[] { "POST create", (Func<HttpRequestMessage>)(() =>
            new HttpRequestMessage(HttpMethod.Post, "/api/orghierarchy")
            {
                Content = JsonContent.Create(new { Code = "HO", Name = "Head Office", ParentId = (Guid?)null, IsActive = true })
            }) };

        yield return new object[] { "PUT update", (Func<HttpRequestMessage>)(() =>
            new HttpRequestMessage(HttpMethod.Put, $"/api/orghierarchy/{SampleId}")
            {
                Content = JsonContent.Create(new { Name = "Renamed", ParentId = (Guid?)null })
            }) };

        yield return new object[] { "DELETE", (Func<HttpRequestMessage>)(() =>
            new HttpRequestMessage(HttpMethod.Delete, $"/api/orghierarchy/{SampleId}")) };

        yield return new object[] { "GET audit", (Func<HttpRequestMessage>)(() =>
            new HttpRequestMessage(HttpMethod.Get, $"/api/orghierarchy/{SampleId}/audit")) };
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Endpoint_WithoutToken_Returns401(string name, Func<HttpRequestMessage> request)
    {
        _ = name;
        var response = await Client().SendAsync(request());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

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

    // R7.4 — invalid signature + role-less: rejected with 401, not 403 (authn before authz).
    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Endpoint_WithInvalidAndRolelessToken_Returns401Not403(
        string name, Func<HttpRequestMessage> request)
    {
        _ = name;
        var client = Client();
        SetBearer(client, DevJwt.Tampered());
        var response = await client.SendAsync(request());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
