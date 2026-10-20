using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Finnova.Models.Contracts.Documents;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Finnova.Tests.Integration.Document;

public class DocumentAuthorizationTests : IClassFixture<DocumentAppFactory>
{
    private readonly DocumentAppFactory _factory;
    public DocumentAuthorizationTests(DocumentAppFactory factory) => _factory = factory;

    // Reuse the existing dev-signed JWT helper used by the Nationality/Court integration tests.
    // DevJwt.ForRoles(params string[] roles) issues a token signed with the test Jwt key
    // configured for the host (Issuer/Audience/SigningKey from DevJwt, matching the host's
    // appsettings "Jwt" section), carrying one role claim per supplied role.
    private HttpClient ClientWithRoles(params string[] roles)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", DevJwt.ForRoles(roles));
        return client;
    }

    [Fact] // R12.1 / R12.5 — missing token => 401 (not 403)
    public async Task Create_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/document", NewCreate());
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact] // R12.2 — maker route without DOC_MASTER_MAKER => 403
    public async Task Create_WithoutMakerRole_Returns403()
    {
        var client = ClientWithRoles("DOC_MASTER_CHECKER");
        var resp = await client.PostAsJsonAsync("/api/document", NewCreate());
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact] // R12.3 — checker route without DOC_MASTER_CHECKER => 403
    public async Task Authorize_WithoutCheckerRole_Returns403()
    {
        var client = ClientWithRoles("DOC_MASTER_MAKER");
        var resp = await client.PostAsync($"/api/document/{Guid.NewGuid()}/authorize", null);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact] // R12.6 — dual-role user still blocked from authorizing own change (ERR-DOC-001)
    public async Task DualRoleUser_CannotAuthorizeOwnChange_Returns409()
    {
        var client = ClientWithRoles("DOC_MASTER_MAKER", "DOC_MASTER_CHECKER");
        var created = await client.PostAsJsonAsync("/api/document", NewCreate());
        created.EnsureSuccessStatusCode();
        var doc = await created.Content.ReadFromJsonAsync<DocumentResponse>();

        var authorize = await client.PostAsync($"/api/document/{doc!.Id}/authorize", null);
        Assert.Equal(HttpStatusCode.Conflict, authorize.StatusCode);
        var body = await authorize.Content.ReadAsStringAsync();
        Assert.Contains("ERR-DOC-001", body);
    }

    // End-to-end to add: maker (user A) creates -> checker (user B) authorizes -> consume returns
    // the document for its (Constitution, EntityRole) with resolved mandatory (R5.4, R11.1).
    // Requires the lookup seed to contain the DOC_CATEGORY/APPLICABLE_ENTITY/CONSTITUTION codes
    // used in NewCreate(); run against SQL Server (test container or local) like the Lookup E2E.

    private static CreateDocumentRequest NewCreate() => new(
        "DOC-PAN", "PAN Card", "PAN", "KYC PAN document", "KYC", "CUSTOMER",
        IsMandatory: true, HasExpiry: false, RequiresMasking: false, IsAadhaarDocument: false,
        MaxFileSizeMb: 5);
}