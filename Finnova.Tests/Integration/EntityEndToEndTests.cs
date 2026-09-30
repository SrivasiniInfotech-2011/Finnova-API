using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace Finnova.Tests.Integration;

/// <summary>E2E integration tests for the Entity host against the in-memory provider.</summary>
public class EntityEndToEndTests : IClassFixture<SystemAdminAppFactory>
{
    private readonly SystemAdminAppFactory _factory;
    public EntityEndToEndTests(SystemAdminAppFactory factory) => _factory = factory;

    private HttpClient AdminClient()
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", DevJwt.SystemAdmin());
        return c;
    }

    private static string Unique(string p) => p + Guid.NewGuid().ToString("N")[..6];

    private async Task<EntityDto> CreateAsync(HttpClient c, string code, string name, string type = "Dealer")
    {
        var res = await c.PostAsJsonAsync("/api/entity", new
        {
            Code = code,
            Name = name,
            EntityType = type,
            RegistrationIdentifier = "22AAAAA0000A1Z5",
            ContactPerson = "Priya Sharma",
        });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var dto = await res.Content.ReadFromJsonAsync<EntityDto>();
        Assert.NotNull(dto);
        return dto!;
    }

    [Fact]
    public async Task FullLifecycle_CreateUpdateDeactivateReactivateAudit()
    {
        var client = AdminClient();
        var created = await CreateAsync(client, Unique("DLR"), "Acme Motors");
        Assert.True(created.IsActive);

        var upd = await client.PutAsJsonAsync($"/api/entity/{created.Id}", new
        {
            Name = "Acme Motors Pvt Ltd",
            RegistrationIdentifier = "22AAAAA0000A1Z5",
            ContactPerson = "Ravi Kumar",
            IsActive = true,
        });
        Assert.Equal(HttpStatusCode.OK, upd.StatusCode);

        var deact = await client.PostAsync($"/api/entity/{created.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.OK, deact.StatusCode);
        var react = await client.PostAsync($"/api/entity/{created.Id}/activate", null);
        Assert.Equal(HttpStatusCode.OK, react.StatusCode);

        var audit = await client.GetFromJsonAsync<List<AuditDto>>($"/api/entity/{created.Id}/audit");
        Assert.NotNull(audit);
        Assert.Equal(4, audit!.Count);   // Create, Update, deactivate, reactivate
        Assert.Equal("Update", audit[0].Action);
        Assert.Equal("Create", audit[^1].Action);
    }

    [Fact]
    public async Task DuplicateCode_SameType_Rejected_With409()
    {
        var client = AdminClient();
        var code = Unique("DUP");
        await CreateAsync(client, code, "First", "Dealer");
        var dup = await client.PostAsJsonAsync("/api/entity", new
        {
            Code = code,
            Name = "Second",
            EntityType = "Dealer",
        });
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
        Assert.Equal("ERR-ENT-409", (await dup.Content.ReadFromJsonAsync<ProblemDto>())!.Code);
    }

    [Fact]
    public async Task SameCode_DifferentType_Allowed()
    {
        var client = AdminClient();
        var code = Unique("SHARED");
        await CreateAsync(client, code, "Dealer X", "Dealer");
        var other = await client.PostAsJsonAsync("/api/entity", new
        {
            Code = code,
            Name = "Supplier X",
            EntityType = "Supplier",
        });
        Assert.Equal(HttpStatusCode.Created, other.StatusCode);
    }

    [Fact]
    public async Task MissingRequiredField_Rejected_With400()
    {
        var client = AdminClient();
        var res = await client.PostAsJsonAsync("/api/entity", new
        {
            Code = Unique("BAD"),
            Name = "",
            EntityType = "Dealer",
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("ERR-ENT-400", (await res.Content.ReadFromJsonAsync<ProblemDto>())!.Code);
    }

    [Fact]
    public async Task InapplicableAttribute_Rejected_With400()
    {
        var client = AdminClient();
        var res = await client.PostAsJsonAsync("/api/entity", new
        {
            Code = Unique("DLR"),
            Name = "Acme Motors",
            EntityType = "Dealer",
            Attributes = new Dictionary<string, string> { ["gstin"] = "22AAAAA0000A1Z5" }, // Supplier key
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("ERR-ENT-400", (await res.Content.ReadFromJsonAsync<ProblemDto>())!.Code);
    }

    [Fact]
    public async Task GetById_UnknownId_Returns404()
    {
        var client = AdminClient();
        var res = await client.GetAsync($"/api/entity/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    private sealed record EntityDto(Guid Id, string Code, string Name, string EntityType, string? RegistrationIdentifier, bool IsActive);
    private sealed record AuditDto(Guid Id, Guid EntityId, string Action, string? OldValues, string NewValues, string Summary, string ChangedBy, DateTime ChangedAtUtc);
    private sealed record ProblemDto(int? Status, string? Title, string? Detail, string? Code);
}