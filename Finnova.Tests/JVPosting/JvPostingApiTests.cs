using Finnova.Tests.Integration;
using System.Net;
using Xunit;

namespace Finnova.Tests.JVPosting;

public sealed class JvPostingApiTests
{
    [Fact]
    public async Task Submission_requires_authentication()
    {
        using var factory = TestApplicationFactory.Create();
        using var client = factory.CreateClient();
        var response = await client.PostAsync("/api/jv-posting/requests", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
