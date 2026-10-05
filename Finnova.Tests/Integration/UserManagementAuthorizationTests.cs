using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace Finnova.Tests.Integration;

/// <summary>Authorization integration tests for the User Management endpoints (FS §9 R15).</summary>
public class UserManagementAuthorizationTests : IClassFixture<SystemAdminAppFactory>
{
    private readonly SystemAdminAppFactory _factory;
    public UserManagementAuthorizationTests(SystemAdminAppFactory factory) => _factory = factory;

    private HttpClient Client() => _factory.CreateClient();
    private static void SetBearer(HttpClient c, string t) =>
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", t);

    private static readonly Guid SampleId = Guid.NewGuid();

    public static IEnumerable<object[]> Endpoints()
    {
        yield return new object[] { "GET list", (Func<HttpRequestMessage>)(() =>
            new HttpRequestMessage(HttpMethod.Get, "/api/user")) };
        yield return new object[] { "GET by-code", (Func<HttpRequestMessage>)(() =>
            new HttpRequestMessage(HttpMethod.Get, "/api/user/by-code/U1001")) };
        yield return new object[] { "POST create", (Func<HttpRequestMessage>)(() =>
            new HttpRequestMessage(HttpMethod.Post, "/api/user")
            { Content = JsonContent.Create(new { Name = "Asha", Password = "Passw0rd!", Designation = "Officer", Department = "Operations", UserType = "Branch" }) }) };
        yield return new object[] { "POST group", (Func<HttpRequestMessage>)(() =>
            new HttpRequestMessage(HttpMethod.Post, "/api/user/group")
            { Content = JsonContent.Create(new { Name = "Ops", MemberUserCodes = new[] { "U1001" } }) }) };
        yield return new object[] { "PUT access", (Func<HttpRequestMessage>)(() =>
            new HttpRequestMessage(HttpMethod.Put, $"/api/user/{SampleId}/access")
            { Content = JsonContent.Create(new { LineOfBusinessId = Guid.Empty, Rows = Array.Empty<object>(), BranchCodes = new[] { "ALL" } }) }) };
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Endpoint_WithoutToken_Returns401(string name, Func<HttpRequestMessage> request)
    { _ = name; Assert.Equal(HttpStatusCode.Unauthorized, (await Client().SendAsync(request())).StatusCode); }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Endpoint_WithNonAdminToken_Returns403(string name, Func<HttpRequestMessage> request)
    { _ = name; var c = Client(); SetBearer(c, DevJwt.NoRole()); Assert.Equal(HttpStatusCode.Forbidden, (await c.SendAsync(request())).StatusCode); }

    [Theory] // R15.4 — 401 before 403 for an invalid + roleless token
    [MemberData(nameof(Endpoints))]
    public async Task Endpoint_WithTamperedToken_Returns401Not403(string name, Func<HttpRequestMessage> request)
    {
        _ = name; var c = Client(); SetBearer(c, DevJwt.Tampered());
        var r = await c.SendAsync(request());
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, r.StatusCode);
    }
}
