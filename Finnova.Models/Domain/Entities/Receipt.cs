using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

/// <summary>
/// Receipt aggregate root (FINNOVA-61). Maps to table "receipts". India-only: amounts are INR with two
/// decimals. Name and bank values are snapshots so a later master rename does not alter a saved receipt.
/// </summary>
public class Receipt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClientRequestId { get; set; }                  // idempotency key (R2.9)
    public string DocNo { get; set; } = string.Empty;          // unique per ReceiptType
    public long? SequenceNumber { get; set; }                  // DCN sequence value; null for Manual (R11.1)
    public ReceiptEntryMode EntryMode { get; set; }
    public ReceiptType ReceiptType { get; set; }
    public ReceiptStatus Status { get; set; } = ReceiptStatus.PendingAuthorization;
    public DateOnly DocDate { get; set; }
    public DateOnly ValueDate { get; set; }

    public Guid LineOfBusinessId { get; set; }
    public string LineOfBusinessName { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public Guid MarketingOfficerUserId { get; set; }
    public string MarketingOfficerName { get; set; } = string.Empty;

    public ReceiptMode Mode { get; set; }
    public ReceiptFrom ReceiptFrom { get; set; } = ReceiptFrom.Customer;
    public bool GlBased { get; set; }
    public string? TrRefNo { get; set; }

    public Guid CustomerId { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public Guid CompanyId { get; set; }

    public string CurrencyCode { get; set; } = "INR";
    public decimal DocAmount { get; set; }
    public string AmountInWords { get; set; } = string.Empty;

    // Reserved for the authorize/revoke follow-up (Assumption A2). Nothing writes them here.
    public string? AuthorizedBy { get; set; }
    public DateTime? AuthorizedAtUtc { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public List<ReceiptLine> Lines { get; set; } = new();
    public List<ReceiptAddLess> AddLess { get; set; } = new();
    public List<ReceiptCashDenomination> Denominations { get; set; } = new();
    public ReceiptChequeDetail? Cheque { get; set; }
    public ReceiptBankTransferDetail? BankTransfer { get; set; }
}

/// <summary>One Account Details line. CreditGl* is resolved at save time (R8).</summary>
public class ReceiptLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReceiptId { get; set; }
    public int LineNo { get; set; }
    public Guid LoanAccountId { get; set; }
    public string PrimeAccountNo { get; set; } = string.Empty;
    public string? SubAccountNo { get; set; }
    public string AccountDescription { get; set; } = string.Empty;
    public string ComponentCode { get; set; } = string.Empty;
    public Guid CreditGlAccountId { get; set; }
    public string CreditGlCode { get; set; } = string.Empty;
    public string? SlAccountCode { get; set; }
    public decimal Amount { get; set; }
}

/// <summary>One Add/Less adjustment row (R9).</summary>
public class ReceiptAddLess
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReceiptId { get; set; }
    public int LineNo { get; set; }
    public Guid LoanAccountId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string? VehicleOrMachineNo { get; set; }
    public AddLessKind Kind { get; set; }
    public string AccountDescription { get; set; } = string.Empty;
    public Guid GlAccountId { get; set; }
    public string GlAccountCode { get; set; } = string.Empty;
    public string? SlAccountCode { get; set; }
    public decimal Amount { get; set; }
    public string? Remarks { get; set; }
}

/// <summary>One cash denomination row. Amount = FaceValue x Count, computed by the service (R4.5).</summary>
public class ReceiptCashDenomination
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReceiptId { get; set; }
    public DenominationKind Kind { get; set; }
    public decimal FaceValue { get; set; }
    public int Count { get; set; }
    public decimal Amount { get; set; }
}

/// <summary>Cheque details with Drawee Bank/Branch snapshots (R5).</summary>
public class ReceiptChequeDetail
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReceiptId { get; set; }
    public string InstrumentNo { get; set; } = string.Empty;   // exactly six digits
    public DateOnly InstrumentDate { get; set; }
    public Guid DraweeBankId { get; set; }
    public string DraweeBankCode { get; set; } = string.Empty;
    public string DraweeBankName { get; set; } = string.Empty;
    public Guid DraweeBranchId { get; set; }
    public string DraweePlaceName { get; set; } = string.Empty;
    public string? PaymentGatewayRefNo { get; set; }
    public string? AckNo { get; set; }
}

/// <summary>Bank Transfer details copied from the F&amp;A bank account (R6).</summary>
public class ReceiptBankTransferDetail
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReceiptId { get; set; }
    public Guid FaBankAccountId { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string AccountNo { get; set; } = string.Empty;
    public string MicrCode { get; set; } = string.Empty;
    public string BankBranch { get; set; } = string.Empty;
    public string GlCode { get; set; } = string.Empty;
    public string? SlCode { get; set; }
}