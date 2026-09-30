using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace Finnova.Tests.Integration;

/// <summary>Authorization integration tests for the Entity endpoints (FINNOVA-11 R7).</summary>
public class EntityAuthorizationTests : IClassFixture<SystemAdminAppFactory>
{
    private readonly SystemAdminAppFactory _factory;
    public EntityAuthorizationTests(SystemAdminAppFactory factory) => _factory = factory;

    private HttpClient Client() => _factory.CreateClient();
    private static void SetBearer(HttpClient c, string t) =>
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", t);

    private static readonly Guid SampleId = Guid.NewGuid();

    public static IEnumerable<object[]> Endpoints()
    {
        yield return new object[] { "GET list", (Func<HttpRequestMessage>)(() =>
            new HttpRequestMessage(HttpMethod.Get, "/api/entity")) };
        yield return new object[] { "GET by id", (Func<HttpRequestMessage>)(() =>
            new HttpRequestMessage(HttpMethod.Get, $"/api/entity/{SampleId}")) };
        yield return new object[] { "POST create", (Func<HttpRequestMessage>)(() =>
            new HttpRequestMessage(HttpMethod.Post, "/api/entity")
            { Content = JsonContent.Create(new { Code = "DLR001", Name = "Acme Motors", EntityType = "Dealer" }) }) };
        yield return new object[] { "PUT update", (Func<HttpRequestMessage>)(() =>
            new HttpRequestMessage(HttpMethod.Put, $"/api/entity/{SampleId}")
            { Content = JsonContent.Create(new { Name = "X", IsActive = true }) }) };
        yield return new object[] { "POST activate", (Func<HttpRequestMessage>)(() =>
            new HttpRequestMessage(HttpMethod.Post, $"/api/entity/{SampleId}/activate")) };
        yield return new object[] { "POST deactivate", (Func<HttpRequestMessage>)(() =>
            new HttpRequestMessage(HttpMethod.Post, $"/api/entity/{SampleId}/deactivate")) };
        yield return new object[] { "GET audit", (Func<HttpRequestMessage>)(() =>
            new HttpRequestMessage(HttpMethod.Get, $"/api/entity/{SampleId}/audit")) };
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Endpoint_WithoutToken_Returns401(string name, Func<HttpRequestMessage> request)
    { _ = name; Assert.Equal(HttpStatusCode.Unauthorized, (await Client().SendAsync(request())).StatusCode); }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Endpoint_WithExpiredToken_Returns401(string name, Func<HttpRequestMessage> request)
    { _ = name; var c = Client(); SetBearer(c, DevJwt.Expired()); Assert.Equal(HttpStatusCode.Unauthorized, (await c.SendAsync(request())).StatusCode); }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Endpoint_WithTamperedToken_Returns401(string name, Func<HttpRequestMessage> request)
    { _ = name; var c = Client(); SetBearer(c, DevJwt.Tampered()); Assert.Equal(HttpStatusCode.Unauthorized, (await c.SendAsync(request())).StatusCode); }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Endpoint_WithNonAdminToken_Returns403(string name, Func<HttpRequestMessage> request)
    { _ = name; var c = Client(); SetBearer(c, DevJwt.NoRole()); Assert.Equal(HttpStatusCode.Forbidden, (await c.SendAsync(request())).StatusCode); }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Endpoint_WithInvalidAndRolelessToken_Returns401Not403(string name, Func<HttpRequestMessage> request)
    {
        _ = name; var c = Client(); SetBearer(c, DevJwt.Tampered());
        var r = await c.SendAsync(request());
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, r.StatusCode);
    }
}