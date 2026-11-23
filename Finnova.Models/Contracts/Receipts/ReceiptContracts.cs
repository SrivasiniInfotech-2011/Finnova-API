using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Contracts.Receipts;

// ---------- Requests ----------
public sealed record ReceiptLineRequest(
    string PrimeAccountNo, string? SubAccountNo, string ComponentCode, string? GlAccountCode, string? SlAccountCode, decimal Amount);

public sealed record ReceiptAddLessRequest(
    string AccountNumber, string? VehicleOrMachineNo, AddLessKind Kind, string GlAccountCode,
    string? SlAccountCode, decimal Amount, string? Remarks);

/// <summary>Any amount sent by a client is ignored; the service computes FaceValue x Count (R4.5).</summary>
public sealed record CashDenominationRequest(DenominationKind Kind, decimal FaceValue, int Count);

public sealed record ChequeDetailRequest(
    string InstrumentNo, DateOnly? InstrumentDate, Guid DraweeBankId, Guid DraweeBranchId,
    string? PaymentGatewayRefNo, string? AckNo);

public sealed record BankTransferDetailRequest(Guid FaBankAccountId);

public sealed record CreateReceiptRequest(
    Guid ClientRequestId,
    ReceiptEntryMode EntryMode,
    string? DocNo,
    ReceiptType ReceiptType,
    DateOnly DocDate,
    DateOnly ValueDate,
    Guid LineOfBusinessId,
    Guid BranchId,
    Guid MarketingOfficerUserId,
    ReceiptMode Mode,
    ReceiptFrom ReceiptFrom,
    bool GlBased,
    string CustomerCode,
    string? TrRefNo,
    decimal DocAmount,
    IReadOnlyList<ReceiptLineRequest> Lines,
    IReadOnlyList<ReceiptAddLessRequest>? AddLess,
    IReadOnlyList<CashDenominationRequest>? Denominations,
    ChequeDetailRequest? Cheque,
    BankTransferDetailRequest? BankTransfer);

public sealed record ReceiptSearchRequest(
    string? Search = null, ReceiptType? ReceiptType = null, ReceiptMode? Mode = null, ReceiptStatus? Status = null,
    Guid? LineOfBusinessId = null, Guid? BranchId = null, DateOnly? DocDateFrom = null, DateOnly? DocDateTo = null,
    int Page = 1, int PageSize = 20);

// ---------- Responses ----------
public sealed record ReceiptLineResponse(
    Guid Id, int LineNo, string PrimeAccountNo, string? SubAccountNo, string AccountDescription,
    string ComponentCode, string CreditGlCode, string? SlAccountCode, decimal Amount);

public sealed record ReceiptAddLessResponse(
    Guid Id, int LineNo, string AccountNumber, string? VehicleOrMachineNo, AddLessKind Kind,
    string AccountDescription, string GlAccountCode, string? SlAccountCode, decimal Amount, string? Remarks);

public sealed record CashDenominationResponse(DenominationKind Kind, decimal FaceValue, int Count, decimal Amount);

public sealed record ChequeDetailResponse(
    string InstrumentNo, DateOnly InstrumentDate, Guid DraweeBankId, string DraweeBankCode, string DraweeBankName,
    Guid DraweeBranchId, string DraweePlaceName, string? PaymentGatewayRefNo, string? AckNo);

public sealed record BankTransferDetailResponse(
    Guid FaBankAccountId, string BankName, string AccountNo, string MicrCode, string BankBranch, string GlCode, string? SlCode);

public sealed record ReceiptCustomerResponse(
    Guid Id, string CustomerCode, string CustomerName, string? Address, string? Phone, string? Mobile,
    string? Email, string? Website);

public sealed record ReceiptResponse(
    Guid Id, string DocNo, long? SequenceNumber, ReceiptEntryMode EntryMode, ReceiptType ReceiptType,
    ReceiptStatus Status, string StatusText, DateOnly DocDate, DateOnly ValueDate,
    Guid LineOfBusinessId, string LineOfBusinessName, Guid BranchId, string BranchCode, string BranchName,
    Guid MarketingOfficerUserId, string MarketingOfficerName, ReceiptMode Mode, ReceiptFrom ReceiptFrom,
    bool GlBased, string? TrRefNo, string CustomerCode, string CustomerName, string CurrencyCode,
    decimal DocAmount, string AmountInWords,
    IReadOnlyList<ReceiptLineResponse> Lines, IReadOnlyList<ReceiptAddLessResponse> AddLess,
    IReadOnlyList<CashDenominationResponse> Denominations, ChequeDetailResponse? Cheque,
    BankTransferDetailResponse? BankTransfer, ReceiptCustomerResponse? Customer,
    string? AuthorizedBy, DateTime? AuthorizedAtUtc, string CreatedBy, DateTime CreatedAtUtc);

public sealed record ReceiptListItemResponse(
    Guid Id, string DocNo, ReceiptType ReceiptType, ReceiptMode Mode, ReceiptStatus Status, string StatusText,
    DateOnly DocDate, DateOnly ValueDate, string CustomerCode, string CustomerName, string BranchName,
    decimal DocAmount, DateTime CreatedAtUtc);

public sealed record ReceiptAuditEntryResponse(
    Guid Id, Guid ReceiptId, string Action, string DocNo, long? SequenceNumber, string Actor,
    string CorrelationId, string ChangeSummary, DateTime OccurredAtUtc);

// ---------- Reference data ----------
public sealed record ReceiptCustomerOption(Guid Id, string CustomerCode, string CustomerName);
public sealed record ReceiptContractOption(string AccountNumber, string AccountName);
public sealed record ReceiptOfficerOption(Guid Id, string UserCode, string Name);
public sealed record ReceiptDraweeBankOption(Guid Id, string BankCode, string BankName);
public sealed record ReceiptDraweeBranchOption(Guid Id, string PlaceCode, string PlaceName);
public sealed record ReceiptFaBankAccountOption(
    Guid Id, string BankName, string BankBranch, string AccountNumber, string MicrCode, string GlCode, string? SlCode);
public sealed record CashDenominationOption(DenominationKind Kind, decimal FaceValue, string Label);