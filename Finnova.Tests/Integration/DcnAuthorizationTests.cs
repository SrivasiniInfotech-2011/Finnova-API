using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace Finnova.Tests.Integration;

/// <summary>
/// Authorization integration tests for the DCN endpoints (FINNOVA-7 R8). Every endpoint (including
/// issuance and activate/deactivate) is SystemAdmin-only:
///   - R8.1 missing/expired/tampered token -> 401 on every endpoint.
///   - R8.2 valid non-admin token -> 403 on every endpoint.
///   - R8.4 invalid + role-less -> 401 (authn before authz), not 403.
/// </summary>
public class DcnAuthorizationTests : IClassFixture<SystemAdminAppFactory>
{
    private readonly SystemAdminAppFactory _factory;

    public DcnAuthorizationTests(SystemAdminAppFactory factory) => _factory = factory;

    private HttpClient Client() => _factory.CreateClient();

    private static void SetBearer(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static readonly Guid SampleId = Guid.NewGuid();

    public static IEnumerable<object[]> Endpoints()
    {
        yield return new object[] { "GET list", (Func<HttpRequestMessage>)(() =>
            new HttpRequestMessage(HttpMethod.Get, "/api/dcn")) };

        yield return new object[] { "GET by id", (Func<HttpRequestMessage>)(() =>
            new HttpRequestMessage(HttpMethod.Get, $"/api/dcn/{SampleId}")) };

        yield return new object[] { "POST create", (Func<HttpRequestMessage>)(() =>
            new HttpRequestMessage(HttpMethod.Post, "/api/dcn")
            {
                Content = JsonContent.Create(new { Code = "INV", Name = "Invoice", DocumentType = "Invoice", FormatTemplate = "{SEQ:5}", ResetRule = "Never" })
            }) };

        yield return new object[] { "PUT update", (Func<HttpRequestMessage>)(() =>
            new HttpRequestMessage(HttpMethod.Put, $"/api/dcn/{SampleId}")
            {
                Content = JsonContent.Create(new { Name = "X", FormatTemplate = "{SEQ}", SeqIncrement = 1, SeqPadding = 1, ResetRule = "Never", IsActive = true })
            }) };

        yield return new object[] { "POST activate", (Func<HttpRequestMessage>)(() =>
            new HttpRequestMessage(HttpMethod.Post, $"/api/dcn/{SampleId}/activate")) };

        yield return new object[] { "POST deactivate", (Func<HttpRequestMessage>)(() =>
            new HttpRequestMessage(HttpMethod.Post, $"/api/dcn/{SampleId}/deactivate")) };

        yield return new object[] { "POST issue", (Func<HttpRequestMessage>)(() =>
            new HttpRequestMessage(HttpMethod.Post, "/api/dcn/issue")
            {
                Content = JsonContent.Create(new { DocumentType = "Invoice", Scope = "Global", ScopeId = (Guid?)null })
            }) };

        yield return new object[] { "GET audit", (Func<HttpRequestMessage>)(() =>
            new HttpRequestMessage(HttpMethod.Get, $"/api/dcn/{SampleId}/audit")) };
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

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Endpoint_WithInvalidAndRolelessToken_Returns401Not403(string name, Func<HttpRequestMessage> request)
    {
        _ = name;
        var client = Client();
        SetBearer(client, DevJwt.Tampered());
        var response = await client.SendAsync(request());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
