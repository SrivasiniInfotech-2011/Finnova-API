using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace Finnova.Tests.Integration;

/// <summary>
/// Task 8.2 — Host smoke test and end-to-end create-then-list (Requirements 2.2, 4.6, 7.6).
///
/// The host boots in-process against the EF Core InMemory provider (see
/// <see cref="SystemAdminAppFactory"/>). The design's 8.2 mentions a "real SQL Server"; because
/// SQL Server may be unavailable in CI, the E2E here runs against the InMemory-backed factory so it
/// is deterministic. Note: EF InMemory does NOT enforce unique indexes, so the
/// (Module, LookupType, Code) uniqueness constraint (R2.8/R5.3) is covered by the service-layer
/// property/unit tests instead; a SQL-Server-backed variant of this E2E can be added where a
/// database is available to also validate the migration and the unique index at the storage layer.
/// </summary>
public class SmokeAndE2ETests : IClassFixture<SystemAdminAppFactory>
{
    private readonly SystemAdminAppFactory _factory;

    public SmokeAndE2ETests(SystemAdminAppFactory factory) => _factory = factory;

    private HttpClient AdminClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", DevJwt.SystemAdmin());
        return client;
    }

    // R7.6 — the host boots (JWT scheme + SystemAdmin policy registered) and /health is healthy.
    [Fact]
    public async Task Health_ReturnsHealthy()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal("Healthy", body);
    }

    // R7.6 — the host booted successfully and the auth pipeline is active: an unauthenticated
    // admin call is challenged (401), proving the JWT scheme + SystemAdmin policy are wired.
    [Fact]
    public async Task Host_BootsWithAuthPipeline_ChallengesUnauthenticatedAdminCall()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/lookup");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // R2.2 / R4.6 — create a value then list it back; the new WID value is present and ordered.
    [Fact]
    public async Task CreateThenList_MaritalStatus_ContainsCreatedValueOrdered()
    {
        var client = AdminClient();

        var createBody = new
        {
            Module = "Origination",
            LookupType = "MARITAL_STATUS",
            Code = "WID",
            Value = "Widowed",
            DisplayOrder = 4,
            IsActive = true
        };

        var createResponse = await client.PostAsJsonAsync("/api/lookup", createBody);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var listResponse = await client.GetAsync(
            "/api/lookup?module=Origination&type=MARITAL_STATUS");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var page = await listResponse.Content.ReadFromJsonAsync<PaginatedResponseDto>();
        Assert.NotNull(page);

        var created = page!.Data.SingleOrDefault(x => x.Code == "WID");
        Assert.NotNull(created);
        Assert.Equal("Widowed", created!.Value);

        // R4.6 — items are ordered by DisplayOrder ascending (Id tie-break). WID has the highest
        // DisplayOrder among the seeded MARITAL_STATUS rows, so it must appear last and the sequence
        // must be non-decreasing by DisplayOrder.
        var orders = page.Data.Select(x => x.DisplayOrder).ToList();
        Assert.Equal(orders.OrderBy(o => o).ToList(), orders);
        Assert.Equal("WID", page.Data[^1].Code);
    }

    // Deserialization DTOs (casing handled by the default web JSON options — case-insensitive).
    private sealed record PaginatedResponseDto(
        List<LookupValueDto> Data, int Total, int Page, int PageSize, int TotalPages);

    private sealed record LookupValueDto(
        Guid Id, string Module, string LookupType, string Code, string Value,
        int DisplayOrder, bool IsActive, bool IsSystemLocked);
}
