using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace Finnova.Tests.Integration;

/// <summary>
/// End-to-end integration tests for the DCN host against the in-memory provider
/// (<see cref="SystemAdminAppFactory"/>): create scheme -> issue repeatedly (formatted + gap-free)
/// -> deactivate -> issue rejected -> reactivate -> issue resumes -> update -> audit trail; plus
/// rejection paths (duplicate code 409, scope conflict 409, invalid template 400, unknown id 404).
/// </summary>
public class DcnEndToEndTests : IClassFixture<SystemAdminAppFactory>
{
    private readonly SystemAdminAppFactory _factory;

    public DcnEndToEndTests(SystemAdminAppFactory factory) => _factory = factory;

    private HttpClient AdminClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", DevJwt.SystemAdmin());
        return client;
    }

    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6];

    private async Task<SchemeDto> CreateAsync(
        HttpClient client, string code, string docType, string template = "{SEQ:5}", string reset = "Never")
    {
        var response = await client.PostAsJsonAsync("/api/dcn", new
        {
            Code = code,
            Name = "Display " + code,
            DocumentType = docType,
            FormatTemplate = template,
            ResetRule = reset,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<SchemeDto>();
        Assert.NotNull(dto);
        return dto!;
    }

    private static async Task<IssuedDto> IssueAsync(HttpClient client, string docType)
    {
        var response = await client.PostAsJsonAsync("/api/dcn/issue",
            new { DocumentType = docType, Scope = "Global", ScopeId = (Guid?)null });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<IssuedDto>();
        Assert.NotNull(dto);
        return dto!;
    }

    [Fact]
    public async Task FullLifecycle_CreateIssueDeactivateReactivateUpdateAudit()
    {
        var client = AdminClient();
        var code = Unique("INV");
        var docType = Unique("Invoice");

        var created = await CreateAsync(client, code, docType, "INV-{SEQ:5}");
        Assert.Equal(0, created.CurrentValue);

        var n1 = await IssueAsync(client, docType);
        var n2 = await IssueAsync(client, docType);
        var n3 = await IssueAsync(client, docType);
        Assert.Equal("INV-00001", n1.Number);
        Assert.Equal("INV-00002", n2.Number);
        Assert.Equal("INV-00003", n3.Number);
        Assert.Equal(new[] { 1L, 2L, 3L }, new[] { n1.SequenceValue, n2.SequenceValue, n3.SequenceValue });

        // Deactivate -> issuance rejected 409.
        var deact = await client.PostAsync($"/api/dcn/{created.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.OK, deact.StatusCode);
        var blocked = await client.PostAsJsonAsync("/api/dcn/issue",
            new { DocumentType = docType, Scope = "Global", ScopeId = (Guid?)null });
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal("ERR-DCN-409", (await blocked.Content.ReadFromJsonAsync<ProblemDto>())!.Code);

        // Reactivate -> issuance resumes from 4 (no reset rule).
        var react = await client.PostAsync($"/api/dcn/{created.Id}/activate", null);
        Assert.Equal(HttpStatusCode.OK, react.StatusCode);
        var n4 = await IssueAsync(client, docType);
        Assert.Equal(4, n4.SequenceValue);

        // Update the template.
        var upd = await client.PutAsJsonAsync($"/api/dcn/{created.Id}", new
        {
            Name = "Renamed",
            FormatTemplate = "INV-{SEQ:6}",
            SeqIncrement = 1,
            SeqPadding = 6,
            ResetRule = "Never",
            IsActive = true,
        });
        Assert.Equal(HttpStatusCode.OK, upd.StatusCode);

        // Audit trail: Create, deactivate, reactivate, update = 4 entries, newest first.
        var audit = await client.GetFromJsonAsync<List<AuditDto>>($"/api/dcn/{created.Id}/audit");
        Assert.NotNull(audit);
        Assert.Equal(4, audit!.Count);
        Assert.Equal("Update", audit[0].Action);
        Assert.Equal("Create", audit[^1].Action);
    }

    [Fact]
    public async Task DuplicateCode_Rejected_With409()
    {
        var client = AdminClient();
        var code = Unique("DUP");
        await CreateAsync(client, code, Unique("Doc"));

        var dup = await client.PostAsJsonAsync("/api/dcn", new
        {
            Code = code,
            Name = "Second",
            DocumentType = Unique("Other"),
            FormatTemplate = "{SEQ}",
            ResetRule = "Never",
        });
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
        Assert.Equal("ERR-DCN-409", (await dup.Content.ReadFromJsonAsync<ProblemDto>())!.Code);
    }

    [Fact]
    public async Task ScopeConflict_Rejected_With409()
    {
        var client = AdminClient();
        var docType = Unique("Shared");
        await CreateAsync(client, Unique("A"), docType);

        var conflict = await client.PostAsJsonAsync("/api/dcn", new
        {
            Code = Unique("B"),
            Name = "Second",
            DocumentType = docType,   // same doc type + Global scope
            FormatTemplate = "{SEQ}",
            ResetRule = "Never",
        });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal("ERR-DCN-409", (await conflict.Content.ReadFromJsonAsync<ProblemDto>())!.Code);
    }

    [Fact]
    public async Task InvalidTemplate_Rejected_With400()
    {
        var client = AdminClient();
        var res = await client.PostAsJsonAsync("/api/dcn", new
        {
            Code = Unique("BAD"),
            Name = "Bad",
            DocumentType = Unique("Doc"),
            FormatTemplate = "INV-0001",   // no SEQ token
            ResetRule = "Never",
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("ERR-DCN-400", (await res.Content.ReadFromJsonAsync<ProblemDto>())!.Code);
    }

    [Fact]
    public async Task IssueUnknownDocumentType_Returns404()
    {
        var client = AdminClient();
        var res = await client.PostAsJsonAsync("/api/dcn/issue",
            new { DocumentType = Unique("Missing"), Scope = "Global", ScopeId = (Guid?)null });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal("ERR-DCN-404", (await res.Content.ReadFromJsonAsync<ProblemDto>())!.Code);
    }

    [Fact]
    public async Task GetById_UnknownId_Returns404()
    {
        var client = AdminClient();
        var res = await client.GetAsync($"/api/dcn/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    private sealed record SchemeDto(
        Guid Id, string Code, string Name, string DocumentType, string FormatTemplate,
        long SeqStart, int SeqIncrement, int SeqPadding, long CurrentValue, bool IsActive);

    private sealed record IssuedDto(Guid SchemeId, string DocumentType, string Number, long SequenceValue, DateTime IssuedAtUtc);

    private sealed record AuditDto(Guid Id, Guid SchemeId, string Action, string? OldValues, string NewValues, string Summary, string ChangedBy, DateTime ChangedAtUtc);

    private sealed record ProblemDto(int? Status, string? Title, string? Detail, string? Code);
}
