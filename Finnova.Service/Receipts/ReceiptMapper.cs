using Finnova.Models.Contracts.Receipts;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;

namespace Finnova.Service.Receipts;

public static class ReceiptMapper
{
    public static string StatusText(ReceiptStatus status) => status switch
    {
        ReceiptStatus.PendingAuthorization => "Pending Authorization",
        ReceiptStatus.Authorized => "Authorized",
        ReceiptStatus.Revoked => "Revoked",
        _ => status.ToString()
    };

    public static ReceiptResponse ToResponse(Receipt r, ReceiptCustomerResponse? customer) => new(
        r.Id, r.DocNo, r.SequenceNumber, r.EntryMode, r.ReceiptType, r.Status, StatusText(r.Status),
        r.DocDate, r.ValueDate, r.LineOfBusinessId, r.LineOfBusinessName, r.BranchId, r.BranchCode, r.BranchName,
        r.MarketingOfficerUserId, r.MarketingOfficerName, r.Mode, r.ReceiptFrom, r.GlBased, r.TrRefNo,
        r.CustomerCode, r.CustomerName, r.CurrencyCode, r.DocAmount, r.AmountInWords,
        r.Lines.OrderBy(x => x.LineNo).Select(x => new ReceiptLineResponse(
            x.Id, x.LineNo, x.PrimeAccountNo, x.SubAccountNo, x.AccountDescription, x.ComponentCode,
            x.CreditGlCode, x.SlAccountCode, x.Amount)).ToList(),
        r.AddLess.OrderBy(x => x.LineNo).Select(x => new ReceiptAddLessResponse(
            x.Id, x.LineNo, x.AccountNumber, x.VehicleOrMachineNo, x.Kind, x.AccountDescription,
            x.GlAccountCode, x.SlAccountCode, x.Amount, x.Remarks)).ToList(),
        r.Denominations.OrderByDescending(x => x.Kind == DenominationKind.Note).ThenByDescending(x => x.FaceValue)
            .Select(x => new CashDenominationResponse(x.Kind, x.FaceValue, x.Count, x.Amount)).ToList(),
        r.Cheque is null ? null : new ChequeDetailResponse(
            r.Cheque.InstrumentNo, r.Cheque.InstrumentDate, r.Cheque.DraweeBankId, r.Cheque.DraweeBankCode,
            r.Cheque.DraweeBankName, r.Cheque.DraweeBranchId, r.Cheque.DraweePlaceName,
            r.Cheque.PaymentGatewayRefNo, r.Cheque.AckNo),
        r.BankTransfer is null ? null : new BankTransferDetailResponse(
            r.BankTransfer.FaBankAccountId, r.BankTransfer.BankName, r.BankTransfer.AccountNo,
            r.BankTransfer.MicrCode, r.BankTransfer.BankBranch, r.BankTransfer.GlCode, r.BankTransfer.SlCode),
        customer, r.AuthorizedBy, r.AuthorizedAtUtc, r.CreatedBy, r.CreatedAtUtc);

    public static ReceiptListItemResponse ToListItem(Receipt r) => new(
        r.Id, r.DocNo, r.ReceiptType, r.Mode, r.Status, StatusText(r.Status), r.DocDate, r.ValueDate,
        r.CustomerCode, r.CustomerName, r.BranchName, r.DocAmount, r.CreatedAtUtc);

    public static ReceiptAuditEntryResponse ToAuditResponse(ReceiptAuditEntry e) => new(
        e.Id, e.ReceiptId, e.Action, e.DocNo, e.SequenceNumber, e.Actor, e.CorrelationId, e.ChangeSummary, e.OccurredAtUtc);
}