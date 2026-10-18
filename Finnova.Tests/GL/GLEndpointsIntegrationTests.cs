using System.Net;
using Finnova.Tests.Integration;
using Xunit;

namespace Finnova.Tests.GL;

/// <summary>
/// Host-wiring / authorization checks against the AccountsService governed host (via
/// <see cref="GLAppFactory"/>, which binds to the AccountsService host and swaps in the EF Core
/// InMemory provider). The GL routes require auth: no token -> 401 (R11.1); wrong/no GL role ->
/// 403 (R11.2/11.6). A full create->submit->authorize happy path requires seeded GLMaker/GLChecker
/// tokens — wire those via your test token helper (mirroring the SystemAdmin integration tests)
/// and extend here.
/// </summary>
public class GLEndpointsIntegrationTests : IClassFixture<GLAppFactory>
{
    private readonly GLAppFactory _factory;
    public GLEndpointsIntegrationTests(GLAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Get_paged_without_token_returns_401()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/gl?page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);   // R11.1
    }

    [Fact]
    public async Task Create_without_token_returns_401()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsync("/api/gl",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Health_is_anonymous()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }
}
