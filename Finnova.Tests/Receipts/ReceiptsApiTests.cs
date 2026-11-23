using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.Receipts;
using Finnova.Models.Domain.Enums;
using Finnova.Tests.LoanAccounts;
using Xunit;

namespace Finnova.Tests.Receipts;

public sealed class ReceiptsApiTests : IClassFixture<LoanAccountsApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly LoanAccountsApiFactory _factory;
    public ReceiptsApiTests(LoanAccountsApiFactory factory) => _factory = factory;

    private ReceiptSeed SeedAll()
    {
        ReceiptSeed? seed = null;
        _factory.Seed(db => seed = ReceiptTestData.Seed(db));
        return seed!;
    }

    private static CreateReceiptRequest Manual(CreateReceiptRequest r) =>
        r with { EntryMode = ReceiptEntryMode.Manual, DocNo = "BOOK-" + Guid.NewGuid().ToString("N")[..8] };

    [Fact]
    public async Task Missing_token_returns_401()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("/api/receipts")).StatusCode);

    [Fact]
    public async Task User_without_a_receipt_role_returns_403()
        => Assert.Equal(HttpStatusCode.Forbidden,
            (await _factory.ClientFor(Guid.NewGuid(), "OtherRole").GetAsync("/api/receipts")).StatusCode);

    [Fact]
    public async Task Cashier_saves_the_cheque_scenario_and_can_read_it_back()
    {
        var seed = SeedAll();
        var client = _factory.ClientFor(Guid.NewGuid(), "Cashier");
        var request = Manual(ReceiptTestData.ChequeRequest(seed));

        var created = await client.PostAsJsonAsync("/api/receipts", request, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = (await created.Content.ReadFromJsonAsync<ReceiptResponse>(Json))!;
        Assert.Equal("Pending Authorization", body.StatusText);

        var again = await client.PostAsJsonAsync("/api/receipts", request, Json);   // same ClientRequestId
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);

        var detail = await client.GetFromJsonAsync<ReceiptResponse>($"/api/receipts/{body.Id}", Json);
        Assert.Equal("Ravi Kumar", detail!.CustomerName);

        var list = await client.GetFromJsonAsync<PaginatedResponse<ReceiptListItemResponse>>("/api/receipts?pageSize=100", Json);
        Assert.Contains(list!.Data, x => x.Id == body.Id);

        var audit = await client.GetFromJsonAsync<List<ReceiptAuditEntryResponse>>($"/api/receipts/{body.Id}/audit-trail", Json);
        Assert.Single(audit!);
    }

    [Fact]
    public async Task Cash_mismatch_returns_422_with_the_rule_key()
    {
        var seed = SeedAll();
        var client = _factory.ClientFor(Guid.NewGuid(), "BranchCollectionOfficer");
        var request = Manual(ReceiptTestData.CashRequest(seed)) with
        {
            Denominations = new[] { new CashDenominationRequest(DenominationKind.Note, 100m, 4), new CashDenominationRequest(DenominationKind.Note, 50m, 1) }
        };

        var response = await client.PostAsJsonAsync("/api/receipts", request, Json);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("ERR-RCP-422", doc.RootElement.GetProperty("code").GetString());
        Assert.Equal("CASH_DENOMINATION_MISMATCH", doc.RootElement.GetProperty("rule").GetString());
        Assert.Equal("Document Amount must match the sum of Cash Denominations", doc.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Shape_error_returns_400_with_the_receipt_code()
    {
        var seed = SeedAll();
        var client = _factory.ClientFor(Guid.NewGuid(), "Cashier");
        var bad = Manual(ReceiptTestData.ChequeRequest(seed)) with { ReceiptType = ReceiptType.Temporary, TrRefNo = "TR-1" };

        var response = await client.PostAsJsonAsync("/api/receipts", bad, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("ERR-RCP-400", doc.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Reference_endpoints_return_the_seeded_masters()
    {
        var seed = SeedAll();
        var client = _factory.ClientFor(Guid.NewGuid(), "Cashier");

        var contracts = await client.GetFromJsonAsync<List<ReceiptContractOption>>(
            $"/api/receipts/references/customers/{seed.CustomerCode}/contracts", Json);
        Assert.Contains(contracts!, c => c.AccountNumber == seed.AccountNumber);
        Assert.DoesNotContain(contracts!, c => c.AccountNumber == seed.ClosedAccountNumber);

        var denominations = await client.GetFromJsonAsync<List<CashDenominationOption>>("/api/receipts/references/denominations", Json);
        Assert.Equal(11, denominations!.Count);

        var branches = await client.GetFromJsonAsync<List<ReceiptDraweeBranchOption>>(
            $"/api/receipts/references/drawee-banks/{seed.DraweeBankId}/branches", Json);
        Assert.Single(branches!);
    }
}