using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Finnova.Tests.DemandProcessing;

public sealed class DemandProcessingIntegrationTests : IClassFixture<WebApplicationFactory<Finnova.AccountsService.Controllers.DemandProcessingController>>
{
    private readonly HttpClient _client;
    public DemandProcessingIntegrationTests(WebApplicationFactory<Finnova.AccountsService.Controllers.DemandProcessingController> factory) => _client = factory.CreateClient();
    [Fact] public async Task MissingTokenReturns401WithoutDemandData() { var response = await _client.GetAsync("/api/demand-processing/runs"); Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode); Assert.DoesNotContain("TotalDemand", await response.Content.ReadAsStringAsync()); }
    [Fact] public async Task InvalidTokenReturns401() { _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid"); var response = await _client.GetAsync("/api/demand-processing/runs"); Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode); }
    [Fact] public async Task NoDemandGenerationBypassRouteExists() { var response = await _client.GetAsync("/api/demand-processing/generate"); Assert.NotEqual(HttpStatusCode.OK, response.StatusCode); }
}
