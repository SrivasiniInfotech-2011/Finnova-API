using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace Finnova.Tests.Integration;

/// <summary>E2E integration tests for the Court host against the in-memory provider.</summary>
public class CourtEndToEndTests : IClassFixture<SystemAdminAppFactory>
{
    private readonly SystemAdminAppFactory _factory;
    public CourtEndToEndTests(SystemAdminAppFactory factory) => _factory = factory;

    private HttpClient AdminClient()
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", DevJwt.SystemAdmin());
        return c;
    }

    private static string Unique(string p) => p + Guid.NewGuid().ToString("N")[..6];

    private async Task<CourtDto> CreateAsync(HttpClient c, string code, string name)
    {
        var res = await c.PostAsJsonAsync("/api/court", new
        {
            Code = code,
            Name = name,
            CourtType = "High",
            Jurisdiction = "Delhi",
            Location = "New Delhi",
        });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var dto = await res.Content.ReadFromJsonAsync<CourtDto>();
        Assert.NotNull(dto);
        return dto!;
    }

    [Fact]
    public async Task FullLifecycle_CreateUpdateDeactivateReactivateAudit()
    {
        var client = AdminClient();
        var created = await CreateAsync(client, Unique("HC"), "Delhi High Court");
        Assert.True(created.IsActive);

        var upd = await client.PutAsJsonAsync($"/api/court/{created.Id}", new
        {
            Name = "Delhi HC",
            CourtType = "High",
            Jurisdiction = "Delhi NCT",
            Location = "New Delhi",
            IsActive = true,
        });
        Assert.Equal(HttpStatusCode.OK, upd.StatusCode);

        var deact = await client.PostAsync($"/api/court/{created.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.OK, deact.StatusCode);
        var react = await client.PostAsync($"/api/court/{created.Id}/activate", null);
        Assert.Equal(HttpStatusCode.OK, react.StatusCode);

        var audit = await client.GetFromJsonAsync<List<AuditDto>>($"/api/court/{created.Id}/audit");
        Assert.NotNull(audit);
        Assert.Equal(4, audit!.Count);   // Create, Update, deactivate, reactivate
        Assert.Equal("Update", audit[0].Action);
        Assert.Equal("Create", audit[^1].Action);
    }

    [Fact]
    public async Task DuplicateCode_Rejected_With409()
    {
        var client = AdminClient();
        var code = Unique("DUP");
        await CreateAsync(client, code, "First");
        var dup = await client.PostAsJsonAsync("/api/court", new
        {
            Code = code,
            Name = "Second",
            CourtType = "District",
            Jurisdiction = "X",
            Location = "Y",
        });
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
        Assert.Equal("ERR-CRT-409", (await dup.Content.ReadFromJsonAsync<ProblemDto>())!.Code);
    }

    [Fact]
    public async Task MissingRequiredField_Rejected_With400()
    {
        var client = AdminClient();
        var res = await client.PostAsJsonAsync("/api/court", new
        {
            Code = Unique("BAD"),
            Name = "",
            CourtType = "High",
            Jurisdiction = "J",
            Location = "L",
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("ERR-CRT-400", (await res.Content.ReadFromJsonAsync<ProblemDto>())!.Code);
    }

    [Fact]
    public async Task GetById_UnknownId_Returns404()
    {
        var client = AdminClient();
        var res = await client.GetAsync($"/api/court/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    private sealed record CourtDto(Guid Id, string Code, string Name, string CourtType, string Jurisdiction, string Location, bool IsActive);
    private sealed record AuditDto(Guid Id, Guid CourtId, string Action, string? OldValues, string NewValues, string Summary, string ChangedBy, DateTime ChangedAtUtc);
    private sealed record ProblemDto(int? Status, string? Title, string? Detail, string? Code);
}