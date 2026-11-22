using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Finnova.Tests.AppropriationLogic;

public sealed class AppropriationLogicIntegrationTests
{
    [Fact]
    public async Task MissingTokenReturns401()
    {
        await using var factory = new WebApplicationFactory<Finnova.SystemAdminService.Controllers.AppropriationLogicController>();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/appropriation-logic");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}