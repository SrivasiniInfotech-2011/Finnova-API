using Finnova.Models.Contracts.DocumentNumberControl;
using Finnova.Models.Contracts.Receipts;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Models.Domain.Receipts;
using Finnova.Repository.Context;
using Finnova.Repository.Repositories;
using Finnova.Service.DocumentNumberControl.Commands.IssueNumber;
using Finnova.Service.Receipts.Commands.CreateReceipt;
using Finnova.Tests.LoanAccounts;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Finnova.Tests.Receipts;

public sealed class ReceiptHandlerTests
{
    private sealed class Harness
    {
        private long _sequence;
        public FinnovaDbContext Db { get; } = LoanAccountTestData.NewDb();
        public ReceiptSeed Seed { get; }
        public Mock<IMediator> Mediator { get; } = new();

        public Harness()
        {
            Seed = ReceiptTestData.Seed(Db);
            Mediator.Setup(m => m.Send(It.IsAny<IssueNumberCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    _sequence++;
                    return new IssuedNumberResponse(Guid.NewGuid(), "RegularReceipt", $"RRC-2025-{_sequence:0000000}", _sequence, DateTime.UtcNow);
                });
        }

        public Task<CreateReceiptResult> Save(CreateReceiptRequest request) =>
            new CreateReceiptCommandHandler(
                new ReceiptRepository(Db), new ReceiptAuditRepository(Db), new ReceiptReferenceRepository(Db),
                new ApplicationUnitOfWork(Db), Mediator.Object)
                .Handle(new CreateReceiptCommand(request, "actor-1", "corr-1"), CancellationToken.None);
    }

    [Fact]
    public async Task Scenario1_regular_cheque_receipt_is_saved_pending_authorization_with_audit_and_sequence()
    {
        var h = new Harness();
        var result = await h.Save(ReceiptTestData.ChequeRequest(h.Seed));

        Assert.False(result.AlreadyExisted);
        Assert.Equal("RRC-2025-0000001", result.Receipt.DocNo);
        Assert.Equal(1, result.Receipt.SequenceNumber);
        Assert.Equal(ReceiptStatus.PendingAuthorization, result.Receipt.Status);
        Assert.Equal("Pending Authorization", result.Receipt.StatusText);
        Assert.Equal(3500m, result.Receipt.DocAmount);
        Assert.Equal("Rupees Three Thousand Five Hundred Only", result.Receipt.AmountInWords);
        Assert.Equal("458921", result.Receipt.Cheque!.InstrumentNo);
        Assert.Equal(2, result.Receipt.Lines.Count);

        var audit = await h.Db.ReceiptAuditEntries.SingleAsync();
        Assert.Equal("Created", audit.Action);
        Assert.Equal(result.Receipt.DocNo, audit.DocNo);
        Assert.Equal(1, audit.SequenceNumber);
        Assert.Equal("actor-1", audit.Actor);
        Assert.DoesNotContain("458921", audit.ChangeSummary);
        Assert.DoesNotContain(h.Seed.AccountNumber, audit.ChangeSummary);
    }

    [Fact]
    public async Task Scenario2_cash_receipt_with_mismatching_denominations_is_rejected_without_a_number()
    {
        var h = new Harness();
        var request = ReceiptTestData.CashRequest(h.Seed) with
        {
            Denominations = new[]
            {
                new CashDenominationRequest(DenominationKind.Note, 100m, 4),
                new CashDenominationRequest(DenominationKind.Note, 50m, 1)   // 450.00
            }
        };

        var ex = await Assert.ThrowsAsync<ReceiptBusinessRuleException>(() => h.Save(request));

        Assert.Equal("Document Amount must match the sum of Cash Denominations", ex.Message);
        Assert.Equal(ReceiptRuleKeys.CashDenominationMismatch, ex.Rule);
        Assert.Equal(0, await h.Db.Receipts.CountAsync());
        Assert.Equal(0, await h.Db.ReceiptAuditEntries.CountAsync());
        h.Mediator.Verify(m => m.Send(It.IsAny<IssueNumberCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(450.00)]
    [InlineData(499.50)]
    [InlineData(501.00)]
    public async Task Cash_denomination_total_other_than_doc_amount_is_always_rejected(double total)
    {
        var h = new Harness();
        var coins = (decimal)total - 400m;   // four 100 notes plus a remainder expressed in 0.50 coins
        var rows = new List<CashDenominationRequest> { new(DenominationKind.Note, 100m, 4) };
        if (coins > 0) rows.Add(new(DenominationKind.Coin, 0.50m, (int)(coins / 0.50m)));
        var request = ReceiptTestData.CashRequest(h.Seed) with { Denominations = rows };

        var ex = await Assert.ThrowsAsync<ReceiptBusinessRuleException>(() => h.Save(request));
        Assert.Equal(ReceiptRuleKeys.CashDenominationMismatch, ex.Rule);
    }

    [Fact]
    public async Task Cash_receipt_with_matching_denominations_is_saved_without_bank_details()
    {
        var h = new Harness();
        var result = await h.Save(ReceiptTestData.CashRequest(h.Seed));
        Assert.Null(result.Receipt.Cheque);
        Assert.Null(result.Receipt.BankTransfer);
        var row = Assert.Single(result.Receipt.Denominations);
        Assert.Equal(500m, row.Amount);
    }

    [Fact]
    public async Task Same_client_request_id_returns_the_existing_receipt_without_a_second_number_or_audit()
    {
        var h = new Harness();
        var request = ReceiptTestData.ChequeRequest(h.Seed);
        var first = await h.Save(request);
        var second = await h.Save(request);

        Assert.True(second.AlreadyExisted);
        Assert.Equal(first.Receipt.Id, second.Receipt.Id);
        Assert.Equal(1, await h.Db.Receipts.CountAsync());
        Assert.Equal(1, await h.Db.ReceiptAuditEntries.CountAsync());
        h.Mediator.Verify(m => m.Send(It.IsAny<IssueNumberCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Doc_amount_that_differs_from_lines_is_rejected()
    {
        var h = new Harness();
        var ex = await Assert.ThrowsAsync<ReceiptBusinessRuleException>(
            () => h.Save(ReceiptTestData.ChequeRequest(h.Seed) with { DocAmount = 3600m }));
        Assert.Equal(ReceiptRuleKeys.DocAmountMismatch, ex.Rule);
    }

    [Fact]
    public async Task Add_and_less_rows_adjust_the_doc_amount()
    {
        var h = new Harness();
        var request = ReceiptTestData.ChequeRequest(h.Seed) with
        {
            DocAmount = 3400m,   // 3500 + 300 Add - 400 Less
            AddLess = new[]
            {
                new ReceiptAddLessRequest(h.Seed.AccountNumber, null, AddLessKind.Add, ReceiptTestData.GlCode, null, 300m, "Bounce charge"),
                new ReceiptAddLessRequest(h.Seed.AccountNumber, null, AddLessKind.Less, ReceiptTestData.GlCode, null, 400m, "Waiver")
            }
        };
        // lines 3200 + 300 = 3500; + 300 - 400 = 3400
        var result = await h.Save(request);
        Assert.Equal(3400m, result.Receipt.DocAmount);
        Assert.Equal(2, result.Receipt.AddLess.Count);
    }

    [Theory]
    [InlineData("unknown", ReceiptRuleKeys.AccountNotFound)]
    [InlineData("closed", ReceiptRuleKeys.AccountNotLive)]
    [InlineData("other", ReceiptRuleKeys.AccountCustomerMismatch)]
    public async Task Contract_guard_rejects_unknown_closed_or_foreign_accounts(string kind, string expectedRule)
    {
        var h = new Harness();
        var accountNumber = kind switch
        {
            "closed" => h.Seed.ClosedAccountNumber,
            "other" => h.Seed.OtherCustomerAccountNumber,
            _ => "ACC-UNKNOWN"
        };
        var request = ReceiptTestData.CashRequest(h.Seed) with
        {
            Lines = new[] { new ReceiptLineRequest(accountNumber, null, "INSTALLMENT_INFLOW", ReceiptTestData.GlCode, null, 500m) }
        };
        var ex = await Assert.ThrowsAsync<ReceiptBusinessRuleException>(() => h.Save(request));
        Assert.Equal(expectedRule, ex.Rule);
        Assert.Equal(0, await h.Db.Receipts.CountAsync());
    }

    [Fact]
    public async Task Gl_based_no_credits_every_line_to_the_customer_gl_and_ignores_line_gl()
    {
        var h = new Harness();
        var request = ReceiptTestData.CashRequest(h.Seed) with
        {
            GlBased = false,
            Lines = new[] { new ReceiptLineRequest(h.Seed.AccountNumber, null, "ODI", "999999999", "SL9", 500m) }
        };
        var result = await h.Save(request);
        var line = Assert.Single(result.Receipt.Lines);
        Assert.Equal(ReceiptTestData.GlCode, line.CreditGlCode);   // the customer GL, not the ignored 999999999
        Assert.Null(line.SlAccountCode);
    }

    [Fact]
    public async Task Gl_based_yes_rejects_a_gl_that_is_not_postable()
    {
        var h = new Harness();
        var request = ReceiptTestData.CashRequest(h.Seed) with
        {
            GlBased = true,
            Lines = new[] { new ReceiptLineRequest(h.Seed.AccountNumber, null, "ODI", "999999999", null, 500m) }
        };
        var ex = await Assert.ThrowsAsync<ReceiptBusinessRuleException>(() => h.Save(request));
        Assert.Equal(ReceiptRuleKeys.GlNotPostable, ex.Rule);
    }

    [Fact]
    public async Task Manual_doc_no_is_stored_without_consuming_a_sequence_and_duplicates_conflict()
    {
        var h = new Harness();
        var request = ReceiptTestData.CashRequest(h.Seed) with { EntryMode = ReceiptEntryMode.Manual, DocNo = "BOOK-0001" };
        var saved = await h.Save(request);
        Assert.Equal("BOOK-0001", saved.Receipt.DocNo);
        Assert.Null(saved.Receipt.SequenceNumber);
        h.Mediator.Verify(m => m.Send(It.IsAny<IssueNumberCommand>(), It.IsAny<CancellationToken>()), Times.Never);

        var duplicate = request with { ClientRequestId = Guid.NewGuid(), DocNo = "book-0001" };
        await Assert.ThrowsAsync<ReceiptDuplicateDocNoException>(() => h.Save(duplicate));
        Assert.Equal(1, await h.Db.Receipts.CountAsync());
    }

    [Fact]
    public async Task Missing_numbering_scheme_is_reported_as_a_business_rule()
    {
        var h = new Harness();
        h.Mediator.Setup(m => m.Send(It.IsAny<IssueNumberCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NumberingSchemeNotFoundException("RegularReceipt"));
        var ex = await Assert.ThrowsAsync<ReceiptBusinessRuleException>(() => h.Save(ReceiptTestData.ChequeRequest(h.Seed)));
        Assert.Equal(ReceiptRuleKeys.NumberingSchemeNotConfigured, ex.Rule);
        Assert.Equal(0, await h.Db.Receipts.CountAsync());
    }

    [Fact]
    public async Task Cheque_drawee_branch_of_another_bank_is_rejected()
    {
        var h = new Harness();
        var request = ReceiptTestData.ChequeRequest(h.Seed) with
        {
            Cheque = new ChequeDetailRequest("458921", null, h.Seed.DraweeBankId, Guid.NewGuid(), null, null)
        };
        await Assert.ThrowsAsync<ReceiptValidationException>(() => h.Save(request));
    }

    [Fact]
    public async Task Cheque_instrument_date_defaults_to_today()
    {
        var h = new Harness();
        var request = ReceiptTestData.ChequeRequest(h.Seed) with
        {
            Cheque = new ChequeDetailRequest("458921", null, h.Seed.DraweeBankId, h.Seed.DraweeBranchId, null, null)
        };
        var result = await h.Save(request);
        Assert.Equal(ReceiptClock.TodayIst(), result.Receipt.Cheque!.InstrumentDate);
    }

    [Fact]
    public async Task Bank_transfer_stores_the_fa_bank_snapshot()
    {
        var h = new Harness();
        var request = ReceiptTestData.CashRequest(h.Seed) with
        {
            Mode = ReceiptMode.BankTransfer,
            Denominations = null,
            BankTransfer = new BankTransferDetailRequest(h.Seed.FaBankId)
        };
        var result = await h.Save(request);
        Assert.Equal("50200012345678", result.Receipt.BankTransfer!.AccountNo);
        Assert.Equal("400240002", result.Receipt.BankTransfer.MicrCode);
        Assert.Equal("SL01", result.Receipt.BankTransfer.SlCode);
    }
}