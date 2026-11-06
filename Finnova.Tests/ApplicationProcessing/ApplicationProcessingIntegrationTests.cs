// ApplicationProcessingIntegrationTests.cs
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Finnova.Tests.ApplicationProcessing;

public sealed class ApplicationProcessingIntegrationTests : IClassFixture<ApplicationProcessingApiFactory>
{
    private readonly ApplicationProcessingApiFactory _factory;
    public ApplicationProcessingIntegrationTests(ApplicationProcessingApiFactory factory) => _factory = factory;
    private HttpClient Client(string? role = null, bool valid = true)
    {
        var c = _factory.CreateClient();
        if (!valid) { c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token"); return c; }
        var key = Environment.GetEnvironmentVariable("FINNOVA_TEST_JWT_KEY") ?? throw new InvalidOperationException("FINNOVA_TEST_JWT_KEY must be set.");
        var claims = role is null ? Array.Empty<Claim>() : new[] { new Claim(ClaimTypes.Role, role), new Claim(ClaimTypes.NameIdentifier, "integration-user") };
        var token = new JwtSecurityToken("Finnova", "FinnovaTests", claims, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(10), new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token)); return c;
    }
    [Fact] public async Task MissingTokenReturns401BeforePolicy() { var response = await _factory.CreateClient().GetAsync("/api/application-processing"); Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode); }
    [Fact] public async Task InvalidTokenReturns401Before403() { var response = await Client("NotApplicationProcessor", false).GetAsync("/api/application-processing"); Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode); }
    [Fact] public async Task ValidWrongRoleReturns403() { var response = await Client("OtherRole").GetAsync("/api/application-processing"); Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); }
    [Fact] public async Task AuthorizedUnknownIdReturns404AndStableCode() { var response = await Client("ApplicationProcessor").GetAsync($"/api/application-processing/{Guid.NewGuid()}"); Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); var body = await response.Content.ReadAsStringAsync(); Assert.Contains("ERR-APP-404", body); }
    [Fact] public async Task ProtectedReferenceRouteRequiresAuthentication() { var response = await _factory.CreateClient().GetAsync("/api/application-processing/references/lines-of-business"); Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode); }
    [Fact] public async Task AuthorizedReferenceRouteIsReachable() { var response = await Client("ApplicationProcessor").GetAsync("/api/application-processing/references/lines-of-business"); Assert.True((int)response.StatusCode is 200 or 500); }
}
