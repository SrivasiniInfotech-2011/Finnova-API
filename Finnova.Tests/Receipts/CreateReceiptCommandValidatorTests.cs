using Finnova.Models.Contracts.Receipts;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Receipts;
using Finnova.Service.Receipts.Commands.CreateReceipt;
using Xunit;

namespace Finnova.Tests.Receipts;

public sealed class CreateReceiptCommandValidatorTests
{
    private static readonly ReceiptSeed Ids = new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "CUST-VALID1", "ACC-VALID1", "ACC-VALID2", "ACC-VALID3");

    private static FluentValidation.Results.ValidationResult Validate(CreateReceiptRequest r)
        => new CreateReceiptCommandValidator().Validate(new CreateReceiptCommand(r, "actor", "corr"));

    private static bool Has(FluentValidation.Results.ValidationResult v, string fragment)
        => v.Errors.Any(e => e.ErrorMessage.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    [Fact]
    public void Valid_cheque_request_passes() => Assert.True(Validate(ReceiptTestData.ChequeRequest(Ids)).IsValid);

    [Fact]
    public void Valid_cash_request_passes() => Assert.True(Validate(ReceiptTestData.CashRequest(Ids)).IsValid);

    [Fact]
    public void Temporary_receipt_rejects_a_tr_ref_no()
        => Assert.True(Has(Validate(ReceiptTestData.ChequeRequest(Ids) with { ReceiptType = ReceiptType.Temporary, TrRefNo = "TR-1" }),
            "not applicable to Temporary"));

    [Theory]
    [InlineData(ReceiptType.Regular)]
    [InlineData(ReceiptType.ThirdParty)]
    public void Regular_and_third_party_accept_a_tr_ref_no(ReceiptType type)
        => Assert.True(Validate(ReceiptTestData.ChequeRequest(Ids) with { ReceiptType = type, ReceiptFrom = ReceiptFrom.ThirdParty, TrRefNo = "TR-1" }).IsValid);

    [Fact]
    public void Third_party_receipt_requires_a_payer_other_than_customer()
        => Assert.True(Has(Validate(ReceiptTestData.ChequeRequest(Ids) with { ReceiptType = ReceiptType.ThirdParty, ReceiptFrom = ReceiptFrom.Customer }),
            "other than Customer"));

    [Theory]
    [InlineData("45892")]
    [InlineData("4589211")]
    [InlineData("45892A")]
    public void Instrument_no_must_be_six_digits(string instrumentNo)
    {
        var request = ReceiptTestData.ChequeRequest(Ids) with
        {
            Cheque = new ChequeDetailRequest(instrumentNo, null, Ids.DraweeBankId, Ids.DraweeBranchId, null, null)
        };
        Assert.True(Has(Validate(request), "six digits"));
    }

    [Fact]
    public void Cash_receipt_with_cheque_details_is_rejected()
        => Assert.True(Has(Validate(ReceiptTestData.CashRequest(Ids) with { Cheque = ReceiptTestData.ChequeRequest(Ids).Cheque }),
            "not applicable to Cash"));

    [Fact]
    public void Cheque_receipt_with_denominations_is_rejected()
        => Assert.True(Has(Validate(ReceiptTestData.ChequeRequest(Ids) with
        {
            Denominations = new[] { new CashDenominationRequest(DenominationKind.Note, 500m, 1) }
        }), "only to Cash"));

    [Fact]
    public void Cash_receipt_without_denominations_is_rejected()
        => Assert.True(Has(Validate(ReceiptTestData.CashRequest(Ids) with { Denominations = null }), "denomination"));

    [Fact]
    public void Denomination_outside_the_catalog_is_rejected()
        => Assert.True(Has(Validate(ReceiptTestData.CashRequest(Ids) with
        {
            Denominations = new[] { new CashDenominationRequest(DenominationKind.Note, 2000m, 1) }
        }), "catalog"));

    [Fact]
    public void Gl_based_yes_requires_a_gl_on_every_line()
        => Assert.True(Has(Validate(ReceiptTestData.CashRequest(Ids) with
        {
            GlBased = true,
            Lines = new[] { new ReceiptLineRequest(Ids.AccountNumber, null, "ODI", null, null, 500m) }
        }), "GL Account"));

    [Fact]
    public void Missing_client_request_id_is_rejected()
        => Assert.True(Has(Validate(ReceiptTestData.CashRequest(Ids) with { ClientRequestId = Guid.Empty }), "ClientRequestId"));

    [Fact]
    public void Amount_with_three_decimals_is_rejected()
        => Assert.True(Has(Validate(ReceiptTestData.CashRequest(Ids) with { DocAmount = 500.001m }), "two decimal"));

    [Fact]
    public void Value_date_before_doc_date_is_rejected()
    {
        var today = ReceiptClock.TodayIst();
        Assert.True(Has(Validate(ReceiptTestData.CashRequest(Ids) with { DocDate = today, ValueDate = today.AddDays(-1) }), "Value Date"));
    }

    [Fact]
    public void Manual_entry_requires_a_doc_no()
        => Assert.True(Has(Validate(ReceiptTestData.CashRequest(Ids) with { EntryMode = ReceiptEntryMode.Manual, DocNo = null }), "Doc No"));
}