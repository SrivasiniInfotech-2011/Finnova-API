using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Finnova.Tests.LoanAccounts;

public sealed class LoanAccountsApiTests : IClassFixture<LoanAccountsApiFactory>
{
    private readonly LoanAccountsApiFactory _factory;
    public LoanAccountsApiTests(LoanAccountsApiFactory factory) => _factory = factory;

    private Guid SeedFlaggedAccount()
    {
        Guid id = default;
        _factory.Seed(db => id = LoanAccountTestData.SeedLoan(db, "APP-" + Guid.NewGuid().ToString("N")[..6], "LN-" + Guid.NewGuid().ToString("N")[..6], "C-" + Guid.NewGuid().ToString("N")[..4], "Asha Rao").Id);
        return id;
    }

    [Fact]
    public async Task Missing_token_returns_401()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("/api/loan-accounts")).StatusCode);

    [Fact]
    public async Task Invalid_token_returns_401_before_403()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/loan-accounts")).StatusCode);
    }

    [Fact]
    public async Task Authenticated_user_without_a_role_returns_403()
        => Assert.Equal(HttpStatusCode.Forbidden, (await _factory.ClientFor(Guid.NewGuid(), "OtherRole").GetAsync("/api/loan-accounts")).StatusCode);

    [Fact]
    public async Task Viewer_can_list_and_view_but_cannot_update()
    {
        var id = SeedFlaggedAccount();
        var viewer = _factory.ClientFor(Guid.NewGuid(), "AccountsViewer");
        var list = await viewer.GetAsync("/api/loan-accounts?pageSize=100");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync($"/api/loan-accounts/{id}")).StatusCode);
        var put = await viewer.PutAsJsonAsync($"/api/loan-accounts/{id}", new { accountName = "x", rowVersion = "" });
        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
    }

    [Fact]
    public async Task Unknown_account_returns_404_with_stable_code()
    {
        var response = await _factory.ClientFor(Guid.NewGuid(), "AccountsViewer").GetAsync($"/api/loan-accounts/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("ERR-ACC-404", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Invalid_page_size_returns_400_with_stable_code()
    {
        var response = await _factory.ClientFor(Guid.NewGuid(), "AccountsViewer").GetAsync("/api/loan-accounts?pageSize=101");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("ERR-ACC-400", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Maker_checker_over_http_enforces_roles_and_four_eyes()
    {
        var id = SeedFlaggedAccount();
        var makerAndApprover = Guid.NewGuid();
        var maker = _factory.ClientFor(makerAndApprover, "ApplicationProcessor", "KycApprover");

        var submit = await maker.PostAsJsonAsync($"/api/loan-accounts/{id}/kyc/resolution-requests", new { reason = "Verified originals at the branch" });
        Assert.Equal(HttpStatusCode.Created, submit.StatusCode);
        var requestId = (await submit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var approveUrl = $"/api/loan-accounts/{id}/kyc/resolution-requests/{requestId}/approve";

        var self = await maker.PostAsJsonAsync(approveUrl, new { comment = "self approval" });
        Assert.Equal(HttpStatusCode.Forbidden, self.StatusCode);
        Assert.Contains("ERR-ACC-403", await self.Content.ReadAsStringAsync());

        var processorOnly = _factory.ClientFor(Guid.NewGuid(), "ApplicationProcessor");
        Assert.Equal(HttpStatusCode.Forbidden, (await processorOnly.PostAsJsonAsync(approveUrl, new { comment = "no approver role" })).StatusCode);

        var duplicate = await maker.PostAsJsonAsync($"/api/loan-accounts/{id}/kyc/resolution-requests", new { reason = "Another valid reason" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var approver = _factory.ClientFor(Guid.NewGuid(), "KycApprover");
        var approved = await approver.PostAsJsonAsync(approveUrl, new { comment = "Documents match" });
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var body = await approved.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Passed", body.GetProperty("kycStatus").GetString());
        Assert.False(body.GetProperty("kycFlagged").GetBoolean());
    }

    [Fact]
    public async Task Approval_without_a_comment_returns_400()
    {
        var id = SeedFlaggedAccount();
        var maker = _factory.ClientFor(Guid.NewGuid(), "ApplicationProcessor");
        var submit = await maker.PostAsJsonAsync($"/api/loan-accounts/{id}/kyc/resolution-requests", new { reason = "Verified originals at the branch" });
        var requestId = (await submit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var response = await _factory.ClientFor(Guid.NewGuid(), "KycApprover")
            .PostAsJsonAsync($"/api/loan-accounts/{id}/kyc/resolution-requests/{requestId}/approve", new { comment = "  " });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Operations_require_the_operator_role()
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await _factory.ClientFor(Guid.NewGuid(), "AccountsViewer").GetAsync("/api/loan-accounts/operations/queue-status")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _factory.ClientFor(Guid.NewGuid(), "AccountsOperator").GetAsync("/api/loan-accounts/operations/queue-status")).StatusCode);
    }
}
