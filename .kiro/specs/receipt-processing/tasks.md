# Implementation Plan: Receipt Processing

**Feature:** `receipt-processing` (JIRA FINNOVA-61)
**Source documents:** `requirements.md`, `design.md`
**Backend repository:** `d:\Projects\Finnova\Finnova-API` (solution `Finnova.Backend.slnx`). All backend paths below are relative to it.
**Frontend repository:** `D:\Projects\Finnova\Finnova-UI` (React 18 + TypeScript + MUI + Vite). All UI paths below are relative to it and start with `src/`.

## Overview

This plan is a copy-and-paste script. Each task is marked **CREATE** (a new file with complete content) or **MODIFY** (an existing file with the exact snippet and the place to put it). Apply the tasks in order. Nothing here has been built or tested; run the verification commands at the end of each group yourself.

The backend slice lives in `Finnova.AccountsService` (route `api/receipts`). The UI follows the service-layer pattern (interface, mock, real, toggle through `VITE_USE_MOCK_API`). No Redux or RxJS is used.

## Before you start (configuration that is not code)

- **Numbering schemes (Document Number Control master, System Administrator):** create three active schemes so that Online Document Numbers can be issued. Use scope Global, no Line of Business, reset rule Never. Suggested values: `DocumentType = TemporaryReceipt`, template `TRC-{YYYY}-{SEQ:7}`; `DocumentType = RegularReceipt`, template `RRC-{YYYY}-{SEQ:7}`; `DocumentType = ThirdPartyReceipt`, template `TPR-{YYYY}-{SEQ:7}`. Choose unique scheme codes (for example `RCPT-TEMP`, `RCPT-REG`, `RCPT-TP`). Do not use `{BRANCH}` or `{LOB}` tokens in Global schemes.
- **Lookup values (Lookup Master):** module `Receipts`, lookup type `RECEIPT_COMPONENT`, for example `INSTALLMENT_INFLOW` = "Installment Inflow" and `ODI` = "Overdue Interest (ODI)".
- **Roles:** after task 10 the roles `Cashier` and `BranchCollectionOfficer` exist in `app_roles`; assign them to users with the existing user-role tooling.
- **F&A banks:** task 11 provides a dev seed for `fa_bank_accounts`.

## Task Dependency Graph

Tasks in the same wave can be applied in any order. A wave starts only after the previous wave is applied.

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["2.1", "2.2", "2.3", "3.1", "3.2"] },
    { "id": 2, "tasks": ["4.1", "4.2", "4.3", "4.4", "5.1"] },
    { "id": 3, "tasks": ["6.1", "6.2", "7.1"] },
    { "id": 4, "tasks": ["7.2", "7.3", "7.4", "8.1"] },
    { "id": 5, "tasks": ["8.2", "8.3", "8.4", "9.1", "9.2"] },
    { "id": 6, "tasks": ["10.1", "10.2", "10.3", "10.4", "10.5", "10.6", "11.1"] },
    { "id": 7, "tasks": ["6.3", "10.7"] },
    { "id": 8, "tasks": ["12.1", "12.2", "12.3", "12.4", "12.5"] },
    { "id": 9, "tasks": ["13.1", "14.1", "14.2", "14.3"] },
    { "id": 10, "tasks": ["13.2", "13.8"] },
    { "id": 11, "tasks": ["13.3", "13.4", "13.5"] },
    { "id": 12, "tasks": ["13.6", "13.7"] },
    { "id": 13, "tasks": ["15.1", "15.2", "15.3", "15.4", "15.5", "15.6", "15.7", "15.8"] },
    { "id": 14, "tasks": ["15.9", "15.10"] },
    { "id": 15, "tasks": ["16.1", "16.2", "16.3", "16.4"] },
    { "id": 16, "tasks": ["17.1"] }
  ]
}
```
## Tasks

- [ ] 1. Backend domain: enums
  - [ ] 1.1 CREATE `Finnova.Models/Domain/Enums/ReceiptEnums.cs`

```csharp
namespace Finnova.Models.Domain.Enums;

/// <summary>Receipt type (R1). Layout and fields are identical; only TR Ref No differs.</summary>
public enum ReceiptType { Temporary = 0, Regular = 1, ThirdParty = 2 }

/// <summary>Payment mode (R4 to R6).</summary>
public enum ReceiptMode { Cash = 0, Cheque = 1, BankTransfer = 2 }

/// <summary>Online: the system issues the Document Number. Manual: the user keys it (R2).</summary>
public enum ReceiptEntryMode { Online = 0, Manual = 1 }

/// <summary>Only PendingAuthorization is produced by this feature (Assumption A2).</summary>
public enum ReceiptStatus { PendingAuthorization = 0, Authorized = 1, Revoked = 2 }

/// <summary>Who paid (Assumption A11).</summary>
public enum ReceiptFrom { Customer = 0, Guarantor = 1, ThirdParty = 2 }

/// <summary>Add increases and Less decreases the Document Amount (R9).</summary>
public enum AddLessKind { Add = 0, Less = 1 }

public enum DenominationKind { Note = 0, Coin = 1 }
```

- [ ] 2. Backend domain: entities
  - [ ] 2.1 CREATE `Finnova.Models/Domain/Entities/Receipt.cs`

```csharp
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
```

  - [ ] 2.2 CREATE `Finnova.Models/Domain/Entities/ReceiptAuditEntry.cs`

```csharp
namespace Finnova.Models.Domain.Entities;

/// <summary>Immutable audit row. The repository exposes Add and read only (R11.3).</summary>
public sealed class ReceiptAuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReceiptId { get; set; }
    public string Action { get; set; } = "Created";
    public string DocNo { get; set; } = string.Empty;
    public long? SequenceNumber { get; set; }                  // logged per the story (R11.1)
    public string Actor { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string ChangeSummary { get; set; } = string.Empty;  // non-sensitive only (R11.6)
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
```

  - [ ] 2.3 CREATE `Finnova.Models/Domain/Entities/FaBankAccount.cs`

```csharp
namespace Finnova.Models.Domain.Entities;

/// <summary>
/// Minimal read-only F&amp;A bank master row used as the Bank Transfer deposit bank (Assumption A8).
/// Maintenance is out of scope; a future F&amp;A Bank Master feature is expected to own this table.
/// </summary>
public class FaBankAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string BankName { get; set; } = string.Empty;
    public string BankBranch { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string MicrCode { get; set; } = string.Empty;       // nine digits
    public string GlCode { get; set; } = string.Empty;         // nine digits
    public string? SlCode { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] 3. Backend domain: exceptions and messages
  - [ ] 3.1 CREATE `Finnova.Models/Domain/Exceptions/ReceiptExceptions.cs`

```csharp
namespace Finnova.Models.Domain.Exceptions;

/// <summary>Stable rule keys carried in the problem response "rule" extension (R15.4).</summary>
public static class ReceiptRuleKeys
{
    public const string CashDenominationMismatch = "CASH_DENOMINATION_MISMATCH";
    public const string DocAmountMismatch = "DOC_AMOUNT_MISMATCH";
    public const string DocAmountNotPositive = "DOC_AMOUNT_NOT_POSITIVE";
    public const string AccountNotFound = "ACCOUNT_NOT_FOUND";
    public const string AccountNotLive = "ACCOUNT_NOT_LIVE";
    public const string AccountCustomerMismatch = "ACCOUNT_CUSTOMER_MISMATCH";
    public const string GlNotPostable = "GL_NOT_POSTABLE";
    public const string CustomerGlNotConfigured = "CUSTOMER_GL_NOT_CONFIGURED";
    public const string NumberingSchemeNotConfigured = "NUMBERING_SCHEME_NOT_CONFIGURED";
}

/// <summary>Base of the receipt error family. Mapped to ProblemDetails by the AccountsService middleware.</summary>
public abstract class ReceiptException : Exception
{
    protected ReceiptException(string message) : base(message) { }
    public abstract string ErrorCode { get; }
    public abstract int StatusCode { get; }
    public virtual string? Rule => null;
}

public sealed class ReceiptValidationException : ReceiptException
{
    public ReceiptValidationException(string message) : base(message) { }
    public override string ErrorCode => "ERR-RCP-400";
    public override int StatusCode => 400;
}

public sealed class ReceiptNotFoundException : ReceiptException
{
    public ReceiptNotFoundException(string message) : base(message) { }
    public static ReceiptNotFoundException ForReceipt(Guid id) => new($"Receipt '{id}' was not found.");
    public override string ErrorCode => "ERR-RCP-404";
    public override int StatusCode => 404;
}

public sealed class ReceiptDuplicateDocNoException : ReceiptException
{
    public ReceiptDuplicateDocNoException() : base("Document number already exists") { }
    public override string ErrorCode => "ERR-RCP-409";
    public override int StatusCode => 409;
}

public sealed class ReceiptBusinessRuleException : ReceiptException
{
    private readonly string _rule;
    public ReceiptBusinessRuleException(string rule, string message) : base(message) => _rule = rule;
    public override string ErrorCode => "ERR-RCP-422";
    public override int StatusCode => 422;
    public override string? Rule => _rule;
}
```

  - [ ] 3.2 CREATE `Finnova.Models/Domain/Receipts/ReceiptMessages.cs`

```csharp
namespace Finnova.Models.Domain.Receipts;

public static class ReceiptMessages
{
    /// <summary>Exact text required by the story (R4.6, R4.10).</summary>
    public const string DenominationMismatch = "Document Amount must match the sum of Cash Denominations";
    public const string DocAmountMismatch = "Doc Amount must equal the sum of the lines adjusted by the Add/Less rows.";
    public const string BankDetailsNotApplicableToCash = "Bank details are not applicable to Cash.";
    public const string DenominationsOnlyForCash = "Denominations are applicable only to Cash.";
}

/// <summary>Lookup coordinates of the line component values (Assumption A13).</summary>
public static class ReceiptLookup
{
    public const string Module = "Receipts";
    public const string ComponentType = "RECEIPT_COMPONENT";
}

public static class ReceiptDocumentTypes
{
    public const string Temporary = "TemporaryReceipt";
    public const string Regular = "RegularReceipt";
    public const string ThirdParty = "ThirdPartyReceipt";
}
```

- [ ] 4. Backend pure helpers
  - [ ] 4.1 CREATE `Finnova.Models/Domain/Receipts/ReceiptCalculator.cs`

```csharp
using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Receipts;

/// <summary>Pure amount arithmetic (R4.5, R9.1). Decimal sums of two-decimal inputs are exact.</summary>
public static class ReceiptCalculator
{
    public const decimal MaxAmount = 999999999.99m;   // 99,99,99,999.99 (A18)

    public static decimal LinesTotal(IEnumerable<decimal> lineAmounts) => lineAmounts.Sum();

    /// <summary>Sum of Add rows minus the sum of Less rows.</summary>
    public static decimal AdjustmentTotal(IEnumerable<(AddLessKind Kind, decimal Amount)> rows)
        => rows.Sum(r => r.Kind == AddLessKind.Add ? r.Amount : -r.Amount);

    /// <summary>Doc Amount = sum(Lines) + sum(Add) - sum(Less).</summary>
    public static decimal DocAmount(IEnumerable<decimal> lineAmounts, IEnumerable<(AddLessKind Kind, decimal Amount)> addLess)
        => LinesTotal(lineAmounts) + AdjustmentTotal(addLess);

    public static decimal DenominationAmount(decimal faceValue, int count) => faceValue * count;

    public static decimal DenominationTotal(IEnumerable<(decimal FaceValue, int Count)> rows)
        => rows.Sum(r => DenominationAmount(r.FaceValue, r.Count));

    public static bool HasAtMostTwoDecimals(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero) == value;
}
```

  - [ ] 4.2 CREATE `Finnova.Models/Domain/Receipts/AmountInWords.cs`

```csharp
using System.Text;

namespace Finnova.Models.Domain.Receipts;

/// <summary>
/// INR amount to words using the Indian numbering system (R10). Range 0.00 to 99,99,99,999.99.
/// Example: 3500.00 -> "Rupees Three Thousand Five Hundred Only".
/// </summary>
public static class AmountInWords
{
    private static readonly string[] Ones =
    {
        "Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten",
        "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen"
    };

    private static readonly string[] Tens =
    {
        "", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety"
    };

    public static string ToWords(decimal amount)
    {
        if (amount < 0m || amount > ReceiptCalculator.MaxAmount)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be between 0.00 and 99,99,99,999.99.");

        var rounded = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        var rupees = (long)decimal.Truncate(rounded);
        var paise = (int)decimal.Round((rounded - rupees) * 100m, 0, MidpointRounding.AwayFromZero);

        var sb = new StringBuilder("Rupees ");
        sb.Append(rupees == 0 ? "Zero" : Words(rupees));
        if (paise > 0) sb.Append(" and ").Append(Words(paise)).Append(" Paise");
        sb.Append(" Only");
        return sb.ToString();
    }

    private static string Words(long n)
    {
        var parts = new List<string>();
        var crore = (int)(n / 10_000_000); n %= 10_000_000;
        var lakh = (int)(n / 100_000); n %= 100_000;
        var thousand = (int)(n / 1_000); n %= 1_000;
        var hundred = (int)(n / 100);
        var rest = (int)(n % 100);

        if (crore > 0) parts.Add(Below100(crore) + " Crore");
        if (lakh > 0) parts.Add(Below100(lakh) + " Lakh");
        if (thousand > 0) parts.Add(Below100(thousand) + " Thousand");
        if (hundred > 0) parts.Add(Ones[hundred] + " Hundred");
        if (rest > 0) parts.Add(Below100(rest));
        return string.Join(' ', parts);
    }

    private static string Below100(int n)
        => n < 20 ? Ones[n] : Tens[n / 10] + (n % 10 == 0 ? string.Empty : "-" + Ones[n % 10]);
}
```

  - [ ] 4.3 CREATE `Finnova.Models/Domain/Receipts/CashDenominationCatalog.cs`

```csharp
using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Receipts;

public sealed record DenominationEntry(DenominationKind Kind, decimal FaceValue, string Label);

/// <summary>Indian denominations accepted on a Cash receipt (Assumption A1). Fixed, ordered list.</summary>
public static class CashDenominationCatalog
{
    public static readonly IReadOnlyList<DenominationEntry> Entries = new[]
    {
        new DenominationEntry(DenominationKind.Note, 500m, "Note 500"),
        new DenominationEntry(DenominationKind.Note, 200m, "Note 200"),
        new DenominationEntry(DenominationKind.Note, 100m, "Note 100"),
        new DenominationEntry(DenominationKind.Note, 50m, "Note 50"),
        new DenominationEntry(DenominationKind.Note, 20m, "Note 20"),
        new DenominationEntry(DenominationKind.Note, 10m, "Note 10"),
        new DenominationEntry(DenominationKind.Coin, 10m, "Coin 10"),
        new DenominationEntry(DenominationKind.Coin, 5m, "Coin 5"),
        new DenominationEntry(DenominationKind.Coin, 2m, "Coin 2"),
        new DenominationEntry(DenominationKind.Coin, 1m, "Coin 1"),
        new DenominationEntry(DenominationKind.Coin, 0.50m, "Coin 0.50 (50 paise)"),
    };

    public static bool Contains(DenominationKind kind, decimal faceValue)
        => Entries.Any(e => e.Kind == kind && e.FaceValue == faceValue);
}
```

  - [ ] 4.4 CREATE `Finnova.Models/Domain/Receipts/ReceiptClock.cs`

```csharp
namespace Finnova.Models.Domain.Receipts;

/// <summary>"Today" in the business time zone Asia/Kolkata (Assumption A19).</summary>
public static class ReceiptClock
{
    private static readonly TimeZoneInfo Ist = Resolve();

    private static TimeZoneInfo Resolve()
    {
        foreach (var id in new[] { "Asia/Kolkata", "India Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.CreateCustomTimeZone("IST", TimeSpan.FromHours(5.5), "IST", "IST");
    }

    public static DateOnly TodayIst() => TodayIst(DateTime.UtcNow);

    public static DateOnly TodayIst(DateTime utcNow)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), Ist));
}
```

- [ ] 5. Backend contracts
  - [ ] 5.1 CREATE `Finnova.Models/Contracts/Receipts/ReceiptContracts.cs`

```csharp
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
```

- [ ] 6. Backend EF configuration, DbSets, migration
  - [ ] 6.1 CREATE `Finnova.Repository/Configuration/ReceiptConfiguration.cs`

```csharp
using Finnova.Models.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finnova.Repository.Configuration;

public sealed class ReceiptConfiguration : IEntityTypeConfiguration<Receipt>
{
    public void Configure(EntityTypeBuilder<Receipt> b)
    {
        b.ToTable("receipts", t => t.HasCheckConstraint("CK_receipts_doc_amount", "[DocAmount] > 0"));
        b.HasKey(x => x.Id);
        b.Property(x => x.DocNo).IsRequired().HasMaxLength(100);
        b.Property(x => x.EntryMode).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.ReceiptType).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        b.Property(x => x.Mode).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.ReceiptFrom).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.LineOfBusinessName).IsRequired().HasMaxLength(200);
        b.Property(x => x.BranchCode).IsRequired().HasMaxLength(20);
        b.Property(x => x.BranchName).IsRequired().HasMaxLength(100);
        b.Property(x => x.MarketingOfficerName).IsRequired().HasMaxLength(50);
        b.Property(x => x.TrRefNo).HasMaxLength(30);
        b.Property(x => x.CustomerCode).IsRequired().HasMaxLength(20);
        b.Property(x => x.CustomerName).IsRequired().HasMaxLength(300);
        b.Property(x => x.CurrencyCode).IsRequired().HasMaxLength(3);
        b.Property(x => x.DocAmount).HasPrecision(18, 2);
        b.Property(x => x.AmountInWords).IsRequired().HasMaxLength(300);
        b.Property(x => x.AuthorizedBy).HasMaxLength(200);
        b.Property(x => x.CreatedBy).IsRequired().HasMaxLength(200);
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasIndex(x => x.ClientRequestId).IsUnique();
        b.HasIndex(x => new { x.ReceiptType, x.DocNo }).IsUnique();
        b.HasIndex(x => new { x.BranchId, x.DocDate });
        b.HasIndex(x => x.CustomerCode);
        b.HasIndex(x => new { x.DocDate, x.CreatedAtUtc });

        b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.ReceiptId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.AddLess).WithOne().HasForeignKey(x => x.ReceiptId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Denominations).WithOne().HasForeignKey(x => x.ReceiptId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Cheque).WithOne().HasForeignKey<ReceiptChequeDetail>(x => x.ReceiptId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.BankTransfer).WithOne().HasForeignKey<ReceiptBankTransferDetail>(x => x.ReceiptId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ReceiptLineConfiguration : IEntityTypeConfiguration<ReceiptLine>
{
    public void Configure(EntityTypeBuilder<ReceiptLine> b)
    {
        b.ToTable("receipt_lines");
        b.HasKey(x => x.Id);
        b.Property(x => x.PrimeAccountNo).IsRequired().HasMaxLength(100);
        b.Property(x => x.SubAccountNo).HasMaxLength(30);
        b.Property(x => x.AccountDescription).IsRequired().HasMaxLength(200);
        b.Property(x => x.ComponentCode).IsRequired().HasMaxLength(50);
        b.Property(x => x.CreditGlCode).IsRequired().HasMaxLength(9);
        b.Property(x => x.SlAccountCode).HasMaxLength(20);
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.HasIndex(x => new { x.ReceiptId, x.LineNo }).IsUnique();
        b.HasOne<LoanAccount>().WithMany().HasForeignKey(x => x.LoanAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ReceiptAddLessConfiguration : IEntityTypeConfiguration<ReceiptAddLess>
{
    public void Configure(EntityTypeBuilder<ReceiptAddLess> b)
    {
        b.ToTable("receipt_add_less");
        b.HasKey(x => x.Id);
        b.Property(x => x.AccountNumber).IsRequired().HasMaxLength(100);
        b.Property(x => x.VehicleOrMachineNo).HasMaxLength(50);
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(10).IsRequired();
        b.Property(x => x.AccountDescription).IsRequired().HasMaxLength(200);
        b.Property(x => x.GlAccountCode).IsRequired().HasMaxLength(9);
        b.Property(x => x.SlAccountCode).HasMaxLength(20);
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.Remarks).HasMaxLength(200);
        b.HasIndex(x => new { x.ReceiptId, x.LineNo }).IsUnique();
        b.HasOne<LoanAccount>().WithMany().HasForeignKey(x => x.LoanAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ReceiptCashDenominationConfiguration : IEntityTypeConfiguration<ReceiptCashDenomination>
{
    public void Configure(EntityTypeBuilder<ReceiptCashDenomination> b)
    {
        b.ToTable("receipt_cash_denominations");
        b.HasKey(x => x.Id);
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(10).IsRequired();
        b.Property(x => x.FaceValue).HasPrecision(9, 2);
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.HasIndex(x => new { x.ReceiptId, x.Kind, x.FaceValue }).IsUnique();
    }
}

public sealed class ReceiptChequeDetailConfiguration : IEntityTypeConfiguration<ReceiptChequeDetail>
{
    public void Configure(EntityTypeBuilder<ReceiptChequeDetail> b)
    {
        b.ToTable("receipt_cheque_details");
        b.HasKey(x => x.Id);
        b.Property(x => x.InstrumentNo).IsRequired().HasMaxLength(6).IsFixedLength();
        b.Property(x => x.DraweeBankCode).IsRequired().HasMaxLength(20);
        b.Property(x => x.DraweeBankName).IsRequired().HasMaxLength(150);
        b.Property(x => x.DraweePlaceName).IsRequired().HasMaxLength(150);
        b.Property(x => x.PaymentGatewayRefNo).HasMaxLength(50);
        b.Property(x => x.AckNo).HasMaxLength(50);
        b.HasIndex(x => x.ReceiptId).IsUnique();
    }
}

public sealed class ReceiptBankTransferDetailConfiguration : IEntityTypeConfiguration<ReceiptBankTransferDetail>
{
    public void Configure(EntityTypeBuilder<ReceiptBankTransferDetail> b)
    {
        b.ToTable("receipt_bank_transfer_details");
        b.HasKey(x => x.Id);
        b.Property(x => x.BankName).IsRequired().HasMaxLength(150);
        b.Property(x => x.AccountNo).IsRequired().HasMaxLength(34);
        b.Property(x => x.MicrCode).IsRequired().HasMaxLength(9);
        b.Property(x => x.BankBranch).IsRequired().HasMaxLength(150);
        b.Property(x => x.GlCode).IsRequired().HasMaxLength(9);
        b.Property(x => x.SlCode).HasMaxLength(20);
        b.HasIndex(x => x.ReceiptId).IsUnique();
    }
}

public sealed class ReceiptAuditEntryConfiguration : IEntityTypeConfiguration<ReceiptAuditEntry>
{
    public void Configure(EntityTypeBuilder<ReceiptAuditEntry> b)
    {
        b.ToTable("receipt_audit_entries");
        b.HasKey(x => x.Id);
        b.Property(x => x.Action).IsRequired().HasMaxLength(40);
        b.Property(x => x.DocNo).IsRequired().HasMaxLength(100);
        b.Property(x => x.Actor).IsRequired().HasMaxLength(200);
        b.Property(x => x.CorrelationId).IsRequired().HasMaxLength(64);
        b.Property(x => x.ChangeSummary).IsRequired().HasMaxLength(1000);
        // No FK: audit entries outlive the receipt and an unknown id reads as an empty list (R11.5).
        b.HasIndex(x => new { x.ReceiptId, x.OccurredAtUtc });
    }
}

public sealed class FaBankAccountConfiguration : IEntityTypeConfiguration<FaBankAccount>
{
    public void Configure(EntityTypeBuilder<FaBankAccount> b)
    {
        b.ToTable("fa_bank_accounts");
        b.HasKey(x => x.Id);
        b.Property(x => x.BankName).IsRequired().HasMaxLength(150);
        b.Property(x => x.BankBranch).IsRequired().HasMaxLength(150);
        b.Property(x => x.AccountNumber).IsRequired().HasMaxLength(34);
        b.Property(x => x.MicrCode).IsRequired().HasMaxLength(9);
        b.Property(x => x.GlCode).IsRequired().HasMaxLength(9);
        b.Property(x => x.SlCode).HasMaxLength(20);
        b.HasIndex(x => new { x.BankName, x.AccountNumber }).IsUnique();
    }
}
```

  - [ ] 6.2 MODIFY `Finnova.Repository/Context/FinnovaDbContext.cs`. Insert these DbSets after the line `public DbSet<DemandAuditEntry> DemandAuditEntries => Set<DemandAuditEntry>();` and before `protected override void OnModelCreating`.

```csharp
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<ReceiptLine> ReceiptLines => Set<ReceiptLine>();
    public DbSet<ReceiptAddLess> ReceiptAddLessRows => Set<ReceiptAddLess>();
    public DbSet<ReceiptCashDenomination> ReceiptCashDenominations => Set<ReceiptCashDenomination>();
    public DbSet<ReceiptChequeDetail> ReceiptChequeDetails => Set<ReceiptChequeDetail>();
    public DbSet<ReceiptBankTransferDetail> ReceiptBankTransferDetails => Set<ReceiptBankTransferDetail>();
    public DbSet<ReceiptAuditEntry> ReceiptAuditEntries => Set<ReceiptAuditEntry>();
    public DbSet<FaBankAccount> FaBankAccounts => Set<FaBankAccount>();
```

  - [ ] 6.3 Create the migration **after task 10.1** (the role catalog change adds two seeded roles to the same migration). Run from the solution root; do not run it yet if task 10.1 is not applied.

```powershell
dotnet ef migrations add AddReceiptProcessing --project Finnova.Repository --startup-project Finnova.AccountsService
dotnet ef database update --project Finnova.Repository --startup-project Finnova.AccountsService
```

- [ ] 7. Backend repositories, interfaces, DI
  - [ ] 7.1 CREATE `Finnova.Repository/Interfaces/IReceiptRepository.cs`

```csharp
using Finnova.Models.Contracts.Lookups;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;

namespace Finnova.Repository.Interfaces;

public sealed record ReceiptSearchFilter(
    string? Search, ReceiptType? ReceiptType, ReceiptMode? Mode, ReceiptStatus? Status,
    Guid? LineOfBusinessId, Guid? BranchId, DateOnly? DocDateFrom, DateOnly? DocDateTo, int Page, int PageSize);

/// <summary>Add does NOT save; callers commit through <see cref="IApplicationUnitOfWork"/> (atomic save, R2.3).</summary>
public interface IReceiptRepository
{
    Task AddAsync(Receipt receipt, CancellationToken ct);
    Task<Receipt?> GetByClientRequestIdAsync(Guid clientRequestId, CancellationToken ct);
    Task<Receipt?> GetDetailAsync(Guid id, CancellationToken ct);
    Task<bool> DocNoExistsAsync(ReceiptType type, string docNo, CancellationToken ct);
    Task<(List<Receipt> Items, int Total)> SearchAsync(ReceiptSearchFilter filter, CancellationToken ct);
}

/// <summary>Append-only: intentionally no update or delete method (R11.3).</summary>
public interface IReceiptAuditRepository
{
    Task AddAsync(ReceiptAuditEntry entry, CancellationToken ct);
    Task<List<ReceiptAuditEntry>> GetByReceiptIdAsync(Guid receiptId, CancellationToken ct);
}

public sealed record RefLob(Guid Id, string Name);
public sealed record RefBranch(Guid Id, string Code, string Name);
public sealed record RefOfficer(Guid Id, string UserCode, string Name);
public sealed record RefCustomer(
    Guid Id, string Code, string Name, Guid CompanyId, Guid? GlAccountId,
    string? Address, string? Phone, string? Mobile, string? Email, string? Website);
public sealed record RefContract(Guid Id, string AccountNumber, string AccountName, Guid? CustomerId, AccountStatus Status, DateTime? ClosedAt);
public sealed record RefGl(Guid Id, string Code);
public sealed record RefDraweeBank(Guid Id, string Code, string Name);
public sealed record RefDraweeBranch(Guid Id, string PlaceCode, string PlaceName, DateOnly StartDate, DateOnly? EndDate)
{
    public bool IsEffectiveOn(DateOnly date) => StartDate <= date && (EndDate is null || EndDate >= date);
}

/// <summary>Read-only projections over the masters used by receipt validation and the screen dropdowns.</summary>
public interface IReceiptReferenceRepository
{
    Task<RefLob?> GetActiveLobAsync(Guid id, CancellationToken ct);
    Task<RefBranch?> GetActiveBranchAsync(Guid id, CancellationToken ct);
    Task<RefOfficer?> GetActiveOfficerAsync(Guid id, CancellationToken ct);
    Task<List<RefOfficer>> GetActiveOfficersAsync(CancellationToken ct);

    Task<RefCustomer?> GetActiveCustomerByCodeAsync(string code, CancellationToken ct);
    Task<RefCustomer?> GetCustomerByIdAsync(Guid id, CancellationToken ct);
    Task<(List<RefCustomer> Items, int Total)> SearchActiveCustomersAsync(string? term, int page, int pageSize, CancellationToken ct);

    Task<Dictionary<string, RefContract>> GetContractsAsync(IReadOnlyCollection<string> accountNumbers, CancellationToken ct);
    Task<List<RefContract>> GetLiveContractsOfCustomerAsync(Guid customerId, CancellationToken ct);

    /// <summary>Same criteria as IGLAccountRepository.GetActiveApprovedPostableByCompanyAndCodeAsync.</summary>
    Task<RefGl?> GetPostableGlByCodeAsync(Guid companyId, string code, CancellationToken ct);
    Task<RefGl?> GetPostableGlByIdAsync(Guid companyId, Guid glAccountId, CancellationToken ct);

    Task<RefDraweeBank?> GetActiveDraweeBankAsync(Guid id, CancellationToken ct);
    Task<List<RefDraweeBank>> GetActiveDraweeBanksAsync(CancellationToken ct);
    Task<RefDraweeBranch?> GetDraweeBranchAsync(Guid bankId, Guid branchId, CancellationToken ct);
    Task<List<RefDraweeBranch>> GetDraweeBranchesAsync(Guid bankId, DateOnly asOf, CancellationToken ct);

    Task<FaBankAccount?> GetActiveFaBankAccountAsync(Guid id, CancellationToken ct);
    Task<List<FaBankAccount>> GetActiveFaBankAccountsAsync(CancellationToken ct);

    Task<List<LookupDropdownItemResponse>> GetActiveComponentsAsync(CancellationToken ct);
}
```

  - [ ] 7.2 CREATE `Finnova.Repository/Repositories/ReceiptRepository.cs`

```csharp
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Finnova.Repository.Repositories;

public sealed class ReceiptRepository(FinnovaDbContext db) : IReceiptRepository
{
    public async Task AddAsync(Receipt receipt, CancellationToken ct) => await db.Receipts.AddAsync(receipt, ct);

    public Task<Receipt?> GetByClientRequestIdAsync(Guid clientRequestId, CancellationToken ct) =>
        Detail().FirstOrDefaultAsync(x => x.ClientRequestId == clientRequestId, ct);

    public Task<Receipt?> GetDetailAsync(Guid id, CancellationToken ct) =>
        Detail().FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<bool> DocNoExistsAsync(ReceiptType type, string docNo, CancellationToken ct)
    {
        var key = docNo.Trim().ToUpper();
        return db.Receipts.AsNoTracking().AnyAsync(x => x.ReceiptType == type && x.DocNo.ToUpper() == key, ct);
    }

    public async Task<(List<Receipt> Items, int Total)> SearchAsync(ReceiptSearchFilter f, CancellationToken ct)
    {
        var q = db.Receipts.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var term = f.Search.Trim();
            q = q.Where(x => x.DocNo.Contains(term) || x.CustomerCode.Contains(term));
        }
        if (f.ReceiptType is { } t) q = q.Where(x => x.ReceiptType == t);
        if (f.Mode is { } m) q = q.Where(x => x.Mode == m);
        if (f.Status is { } s) q = q.Where(x => x.Status == s);
        if (f.LineOfBusinessId is { } lob) q = q.Where(x => x.LineOfBusinessId == lob);
        if (f.BranchId is { } br) q = q.Where(x => x.BranchId == br);
        if (f.DocDateFrom is { } from) q = q.Where(x => x.DocDate >= from);
        if (f.DocDateTo is { } to) q = q.Where(x => x.DocDate <= to);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(x => x.DocDate).ThenByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id)
            .Skip((f.Page - 1) * f.PageSize).Take(f.PageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    private IQueryable<Receipt> Detail() => db.Receipts.AsNoTracking().AsSplitQuery()
        .Include(x => x.Lines).Include(x => x.AddLess).Include(x => x.Denominations)
        .Include(x => x.Cheque).Include(x => x.BankTransfer);
}

public sealed class ReceiptAuditRepository(FinnovaDbContext db) : IReceiptAuditRepository
{
    public async Task AddAsync(ReceiptAuditEntry entry, CancellationToken ct) => await db.ReceiptAuditEntries.AddAsync(entry, ct);

    public Task<List<ReceiptAuditEntry>> GetByReceiptIdAsync(Guid receiptId, CancellationToken ct) =>
        db.ReceiptAuditEntries.AsNoTracking().Where(x => x.ReceiptId == receiptId)
            .OrderByDescending(x => x.OccurredAtUtc).ThenByDescending(x => x.Id).ToListAsync(ct);
}
```

  - [ ] 7.3 CREATE `Finnova.Repository/Repositories/ReceiptReferenceRepository.cs`

```csharp
using Finnova.Models.Contracts.Lookups;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Receipts;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Finnova.Repository.Repositories;

public sealed class ReceiptReferenceRepository(FinnovaDbContext db) : IReceiptReferenceRepository
{
    public Task<RefLob?> GetActiveLobAsync(Guid id, CancellationToken ct) =>
        db.LinesOfBusiness.AsNoTracking().Where(x => x.Id == id && x.IsActive)
            .Select(x => new RefLob(x.Id, x.LOB_Name)).FirstOrDefaultAsync(ct);

    public Task<RefBranch?> GetActiveBranchAsync(Guid id, CancellationToken ct) =>
        db.Locations.AsNoTracking().Where(x => x.Id == id && x.IsActive && x.Level == 5)
            .Select(x => new RefBranch(x.Id, x.Code, x.Name)).FirstOrDefaultAsync(ct);

    public Task<RefOfficer?> GetActiveOfficerAsync(Guid id, CancellationToken ct) =>
        db.UserAccounts.AsNoTracking().Where(x => x.Id == id && x.IsActive)
            .Select(x => new RefOfficer(x.Id, x.UserCode, x.Name)).FirstOrDefaultAsync(ct);

    public Task<List<RefOfficer>> GetActiveOfficersAsync(CancellationToken ct) =>
        db.UserAccounts.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name)
            .Select(x => new RefOfficer(x.Id, x.UserCode, x.Name)).ToListAsync(ct);

    public async Task<RefCustomer?> GetActiveCustomerByCodeAsync(string code, CancellationToken ct)
    {
        var c = await db.Customers.AsNoTracking().Include(x => x.Addresses)
            .FirstOrDefaultAsync(x => x.CustomerCode == code && x.Status == CustomerLifecycleStatus.Active, ct);
        return c is null ? null : ToCustomerRef(c);
    }

    public async Task<RefCustomer?> GetCustomerByIdAsync(Guid id, CancellationToken ct)
    {
        var c = await db.Customers.AsNoTracking().Include(x => x.Addresses).FirstOrDefaultAsync(x => x.Id == id, ct);
        return c is null ? null : ToCustomerRef(c);
    }

    public async Task<(List<RefCustomer> Items, int Total)> SearchActiveCustomersAsync(string? term, int page, int pageSize, CancellationToken ct)
    {
        var q = db.Customers.AsNoTracking().Where(x => x.Status == CustomerLifecycleStatus.Active);
        var t = term?.Trim();
        if (!string.IsNullOrEmpty(t))
            q = q.Where(x => x.CustomerCode.Contains(t) || (x.FirstName != null && x.FirstName.Contains(t)));
        var total = await q.CountAsync(ct);
        var rows = await q.Include(x => x.Addresses).OrderBy(x => x.CustomerCode)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (rows.Select(ToCustomerRef).ToList(), total);
    }

    public async Task<Dictionary<string, RefContract>> GetContractsAsync(IReadOnlyCollection<string> accountNumbers, CancellationToken ct)
    {
        var list = await db.LoanAccounts.AsNoTracking().Where(x => accountNumbers.Contains(x.AccountNumber))
            .Select(x => new RefContract(x.Id, x.AccountNumber, x.AccountName, x.CustomerId, x.Status, x.ClosedAt))
            .ToListAsync(ct);
        return list.ToDictionary(x => x.AccountNumber, StringComparer.OrdinalIgnoreCase);
    }

    public Task<List<RefContract>> GetLiveContractsOfCustomerAsync(Guid customerId, CancellationToken ct) =>
        db.LoanAccounts.AsNoTracking()
            .Where(x => x.CustomerId == customerId && x.Status == AccountStatus.Active && x.ClosedAt == null)
            .OrderBy(x => x.AccountNumber)
            .Select(x => new RefContract(x.Id, x.AccountNumber, x.AccountName, x.CustomerId, x.Status, x.ClosedAt))
            .ToListAsync(ct);

    public Task<RefGl?> GetPostableGlByCodeAsync(Guid companyId, string code, CancellationToken ct) =>
        db.GLAccounts.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.GLCode == code && x.Status == GLStatus.Active
                        && x.AuthorizedVersionNumber > 0 && x.IsPostable)
            .Select(x => new RefGl(x.Id, x.GLCode)).FirstOrDefaultAsync(ct);

    public Task<RefGl?> GetPostableGlByIdAsync(Guid companyId, Guid glAccountId, CancellationToken ct) =>
        db.GLAccounts.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.Id == glAccountId && x.Status == GLStatus.Active
                        && x.AuthorizedVersionNumber > 0 && x.IsPostable)
            .Select(x => new RefGl(x.Id, x.GLCode)).FirstOrDefaultAsync(ct);

    public Task<RefDraweeBank?> GetActiveDraweeBankAsync(Guid id, CancellationToken ct) =>
        db.DraweeBanks.AsNoTracking().Where(x => x.Id == id && x.IsActive)
            .Select(x => new RefDraweeBank(x.Id, x.BankCode, x.BankName)).FirstOrDefaultAsync(ct);

    public Task<List<RefDraweeBank>> GetActiveDraweeBanksAsync(CancellationToken ct) =>
        db.DraweeBanks.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.BankName).ThenBy(x => x.BankCode)
            .Select(x => new RefDraweeBank(x.Id, x.BankCode, x.BankName)).ToListAsync(ct);

    public async Task<RefDraweeBranch?> GetDraweeBranchAsync(Guid bankId, Guid branchId, CancellationToken ct)
    {
        var b = await db.DraweeBranches.AsNoTracking().FirstOrDefaultAsync(x => x.Id == branchId && x.DraweeBankId == bankId, ct);
        return b is null ? null : ToBranchRef(b);
    }

    public async Task<List<RefDraweeBranch>> GetDraweeBranchesAsync(Guid bankId, DateOnly asOf, CancellationToken ct)
    {
        var rows = await db.DraweeBranches.AsNoTracking().Where(x => x.DraweeBankId == bankId).ToListAsync(ct);
        return rows.Select(ToBranchRef).Where(x => x.IsEffectiveOn(asOf)).OrderBy(x => x.PlaceName).ToList();
    }

    public Task<FaBankAccount?> GetActiveFaBankAccountAsync(Guid id, CancellationToken ct) =>
        db.FaBankAccounts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);

    public Task<List<FaBankAccount>> GetActiveFaBankAccountsAsync(CancellationToken ct) =>
        db.FaBankAccounts.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.BankName).ThenBy(x => x.AccountNumber).ToListAsync(ct);

    public Task<List<LookupDropdownItemResponse>> GetActiveComponentsAsync(CancellationToken ct) =>
        db.LookupValues.AsNoTracking()
            .Where(x => x.Module == ReceiptLookup.Module && x.LookupType == ReceiptLookup.ComponentType && x.IsActive)
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Value)
            .Select(x => new LookupDropdownItemResponse(x.Code, x.Value)).ToListAsync(ct);

    // ---- mapping helpers ----
    private static RefDraweeBranch ToBranchRef(DraweeBranch b) => new(
        b.Id, b.PlaceCode, b.PlaceName, DateOnly.FromDateTime(b.StartDate),
        b.EndDate is null ? null : DateOnly.FromDateTime(b.EndDate.Value));

    /// <summary>Customer name = the non-empty name parts joined by a space.</summary>
    private static string NameOf(Customer c) => string.Join(' ',
        new[] { c.FirstName, c.SecondName, c.ThirdName, c.FourthName, c.FifthName, c.SixthName }
            .Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()));

    private static RefCustomer ToCustomerRef(Customer c)
    {
        // Communication address first; fall back to Permanent (R13.3).
        var a = c.Addresses.OrderBy(x => x.AddressType == CustomerAddressType.Communication ? 0 : 1).FirstOrDefault();
        return new RefCustomer(c.Id, c.CustomerCode, NameOf(c), c.CompanyId, c.GLAccountId,
            AddressText(a), a?.Telephone, a?.Mobile, a?.Email, a?.Website);
    }

    private static string? AddressText(CustomerAddress? a)
    {
        if (a is null) return null;
        var text = string.Join(", ", new[] { a.HouseNumber, a.BlockNumber, a.WayNumber, a.POBoxNumber, a.City, a.State, a.PostalCode, a.Country }
            .Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()));
        return text.Length == 0 ? null : text;
    }
}
```

  - [ ] 7.4 MODIFY `Finnova.Repository/DependencyInjection.cs`. Insert these lines after `services.AddScoped<IDemandRepository, DemandRepository>();` and before `return services;`.

```csharp
        services.AddScoped<IReceiptRepository, ReceiptRepository>();
        services.AddScoped<IReceiptAuditRepository, ReceiptAuditRepository>();
        services.AddScoped<IReceiptReferenceRepository, ReceiptReferenceRepository>();
```

- [ ] 8. Backend service: CreateReceipt command slice
  - [ ] 8.1 CREATE `Finnova.Service/Receipts/ReceiptMapper.cs`

```csharp
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
```

  - [ ] 8.2 CREATE `Finnova.Service/Receipts/Commands/CreateReceipt/CreateReceiptCommand.cs`

```csharp
using Finnova.Models.Contracts.Receipts;
using MediatR;

namespace Finnova.Service.Receipts.Commands.CreateReceipt;

/// <summary>Actor and correlation id come from the validated token and the HTTP context, never the body (R14.4).</summary>
public sealed record CreateReceiptCommand(CreateReceiptRequest Request, string Actor, string CorrelationId)
    : IRequest<CreateReceiptResult>;

/// <summary>AlreadyExisted is true when the ClientRequestId matched a saved receipt (HTTP 200 instead of 201).</summary>
public sealed record CreateReceiptResult(ReceiptResponse Receipt, bool AlreadyExisted);
```

  - [ ] 8.3 CREATE `Finnova.Service/Receipts/Commands/CreateReceipt/CreateReceiptCommandValidator.cs`

```csharp
using System.Text.RegularExpressions;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Receipts;
using FluentValidation;

namespace Finnova.Service.Receipts.Commands.CreateReceipt;

/// <summary>Shape and field rules (HTTP 400). Reference and ledger rules live in the handler (HTTP 422).</summary>
public sealed class CreateReceiptCommandValidator : AbstractValidator<CreateReceiptCommand>
{
    private const int MaxRows = 50;
    private static readonly Regex SixDigits = new(@"^\d{6}$", RegexOptions.Compiled);
    private static readonly Regex NineDigits = new(@"^\d{9}$", RegexOptions.Compiled);

    public CreateReceiptCommandValidator()
    {
        RuleFor(x => x.Actor).NotEmpty().WithMessage("The acting user could not be determined.");
        RuleFor(x => x.Request).NotNull().WithMessage("Request body is required.");

        When(x => x.Request is not null, () =>
        {
            // ---- identity, enums, document number ----
            RuleFor(x => x.Request.ClientRequestId).NotEqual(Guid.Empty).WithMessage("ClientRequestId is required.");
            RuleFor(x => x.Request.EntryMode).IsInEnum().WithMessage("Entry Mode is invalid.");
            RuleFor(x => x.Request.ReceiptType).IsInEnum().WithMessage("Receipt Type is invalid.");
            RuleFor(x => x.Request.Mode).IsInEnum().WithMessage("Mode is invalid.");
            RuleFor(x => x.Request.ReceiptFrom).IsInEnum().WithMessage("Receipt From is invalid.");
            RuleFor(x => x.Request.DocNo)
                .NotEmpty().WithMessage("Doc No is required for Manual entry.")
                .MaximumLength(30).WithMessage("Doc No must be at most 30 characters.")
                .When(x => x.Request.EntryMode == ReceiptEntryMode.Manual);

            // ---- header ----
            RuleFor(x => x.Request.DocDate).NotEqual(default(DateOnly)).WithMessage("Doc Date is required.")
                .Must(d => d <= ReceiptClock.TodayIst()).WithMessage("Doc Date cannot be in the future.");
            RuleFor(x => x.Request.ValueDate).NotEqual(default(DateOnly)).WithMessage("Value Date is required.")
                .GreaterThanOrEqualTo(x => x.Request.DocDate).WithMessage("Value Date cannot be earlier than Doc Date.");
            RuleFor(x => x.Request.LineOfBusinessId).NotEqual(Guid.Empty).WithMessage("Line of Business is required.");
            RuleFor(x => x.Request.BranchId).NotEqual(Guid.Empty).WithMessage("Location is required.");
            RuleFor(x => x.Request.MarketingOfficerUserId).NotEqual(Guid.Empty).WithMessage("Marketing Officer is required.");
            RuleFor(x => x.Request.CustomerCode).NotEmpty().WithMessage("Customer Code is required.")
                .MaximumLength(20).WithMessage("Customer Code must be at most 20 characters.");
            RuleFor(x => x.Request.TrRefNo).MaximumLength(30).WithMessage("TR Ref No must be at most 30 characters.");
            RuleFor(x => x.Request.TrRefNo).Must(string.IsNullOrWhiteSpace)
                .When(x => x.Request.ReceiptType == ReceiptType.Temporary)
                .WithMessage("TR Ref No is not applicable to Temporary receipts.");
            RuleFor(x => x.Request.ReceiptFrom).NotEqual(ReceiptFrom.Customer)
                .When(x => x.Request.ReceiptType == ReceiptType.ThirdParty)
                .WithMessage("A Third-Party receipt requires a Receipt From other than Customer.");
            RuleFor(x => x.Request.DocAmount)
                .GreaterThan(0m).WithMessage("Doc Amount must be greater than zero.")
                .LessThanOrEqualTo(ReceiptCalculator.MaxAmount).WithMessage("Doc Amount must not exceed 99,99,99,999.99.")
                .Must(ReceiptCalculator.HasAtMostTwoDecimals).WithMessage("Doc Amount must have at most two decimal places.");

            // ---- Account Details lines ----
            RuleFor(x => x.Request.Lines)
                .Must(l => l is { Count: > 0 }).WithMessage("At least one Account Details line is required.")
                .Must(l => l is null || l.Count <= MaxRows).WithMessage($"At most {MaxRows} Account Details lines are allowed.");
            RuleForEach(x => x.Request.Lines).ChildRules(l =>
            {
                l.RuleFor(i => i.PrimeAccountNo).NotEmpty().WithMessage("Line: Prime Account No is required.")
                    .MaximumLength(100).WithMessage("Line: Prime Account No must be at most 100 characters.");
                l.RuleFor(i => i.SubAccountNo).MaximumLength(30).WithMessage("Line: Sub Account No must be at most 30 characters.");
                l.RuleFor(i => i.ComponentCode).NotEmpty().WithMessage("Line: Component is required.")
                    .MaximumLength(50).WithMessage("Line: Component must be at most 50 characters.");
                l.RuleFor(i => i.SlAccountCode).MaximumLength(20).WithMessage("Line: SL Account must be at most 20 characters.");
                l.RuleFor(i => i.Amount).GreaterThan(0m).WithMessage("Line: Amount must be greater than zero.")
                    .Must(ReceiptCalculator.HasAtMostTwoDecimals).WithMessage("Line: Amount must have at most two decimal places.");
            });
            RuleFor(x => x.Request).Must(r => (r.Lines ?? []).All(l => l.GlAccountCode is not null && NineDigits.IsMatch(l.GlAccountCode.Trim())))
                .When(x => x.Request.GlBased)
                .WithMessage("GL Based is Yes: every line requires a nine-digit GL Account.");

            // ---- Add/Less rows ----
            RuleFor(x => x.Request.AddLess)
                .Must(a => a is null || a.Count <= MaxRows).WithMessage($"At most {MaxRows} Add/Less rows are allowed.");
            RuleForEach(x => x.Request.AddLess).ChildRules(a =>
            {
                a.RuleFor(i => i.AccountNumber).NotEmpty().WithMessage("Add/Less: Account Number is required.")
                    .MaximumLength(100).WithMessage("Add/Less: Account Number must be at most 100 characters.");
                a.RuleFor(i => i.Kind).IsInEnum().WithMessage("Add/Less: Add or Less is required.");
                a.RuleFor(i => i.GlAccountCode).NotEmpty().WithMessage("Add/Less: GL Account is required.")
                    .Must(c => c is not null && NineDigits.IsMatch(c.Trim())).WithMessage("Add/Less: GL Account must be nine digits.");
                a.RuleFor(i => i.SlAccountCode).MaximumLength(20).WithMessage("Add/Less: SL Account must be at most 20 characters.");
                a.RuleFor(i => i.VehicleOrMachineNo).MaximumLength(50).WithMessage("Add/Less: Vehicle/Machine No must be at most 50 characters.");
                a.RuleFor(i => i.Remarks).MaximumLength(200).WithMessage("Add/Less: Remarks must be at most 200 characters.");
                a.RuleFor(i => i.Amount).GreaterThan(0m).WithMessage("Add/Less: Amount must be greater than zero.")
                    .Must(ReceiptCalculator.HasAtMostTwoDecimals).WithMessage("Add/Less: Amount must have at most two decimal places.");
            });

            // ---- Cash denominations ----
            RuleFor(x => x.Request).Must(r => (r.Denominations ?? []).Any(d => d.Count > 0))
                .When(x => x.Request.Mode == ReceiptMode.Cash)
                .WithMessage("At least one denomination with a count greater than zero is required for Cash.");
            RuleFor(x => x.Request).Must(r => !(r.Denominations ?? []).Any(d => d.Count > 0))
                .When(x => x.Request.Mode != ReceiptMode.Cash)
                .WithMessage(ReceiptMessages.DenominationsOnlyForCash);
            RuleForEach(x => x.Request.Denominations).ChildRules(d =>
            {
                d.RuleFor(i => i.Kind).IsInEnum().WithMessage("Denomination: kind is invalid.");
                d.RuleFor(i => i.Count).InclusiveBetween(0, 1_000_000)
                    .WithMessage("Denomination: count must be an integer from 0 to 1,000,000.");
                d.RuleFor(i => i).Must(i => CashDenominationCatalog.Contains(i.Kind, i.FaceValue))
                    .WithMessage("Denomination: the face value is not in the denomination catalog.");
            });
            RuleFor(x => x.Request.Denominations)
                .Must(d => d is null || d.Select(i => (i.Kind, i.FaceValue)).Distinct().Count() == d.Count)
                .WithMessage("Denomination: a face value appears more than once.");

            // ---- mode exclusivity ----
            RuleFor(x => x.Request).Must(r => r.Cheque is null && r.BankTransfer is null)
                .When(x => x.Request.Mode == ReceiptMode.Cash)
                .WithMessage(ReceiptMessages.BankDetailsNotApplicableToCash);
            RuleFor(x => x.Request.Cheque).NotNull().WithMessage("Cheque details are required for the Cheque mode.")
                .When(x => x.Request.Mode == ReceiptMode.Cheque);
            RuleFor(x => x.Request.BankTransfer).Null().WithMessage("Bank Transfer details are not applicable to Cheque.")
                .When(x => x.Request.Mode == ReceiptMode.Cheque);
            RuleFor(x => x.Request.BankTransfer).NotNull().WithMessage("Deposit Bank is required for the Bank Transfer mode.")
                .When(x => x.Request.Mode == ReceiptMode.BankTransfer);
            RuleFor(x => x.Request.Cheque).Null().WithMessage("Cheque details are not applicable to Bank Transfer.")
                .When(x => x.Request.Mode == ReceiptMode.BankTransfer);

            When(x => x.Request.Cheque is not null, () =>
            {
                RuleFor(x => x.Request.Cheque!.InstrumentNo).NotEmpty().WithMessage("Instrument No must be six digits.")
                    .Must(n => n is not null && SixDigits.IsMatch(n)).WithMessage("Instrument No must be six digits.");
                RuleFor(x => x.Request.Cheque!.DraweeBankId).NotEqual(Guid.Empty).WithMessage("Drawee Bank is required.");
                RuleFor(x => x.Request.Cheque!.DraweeBranchId).NotEqual(Guid.Empty).WithMessage("Drawee Branch is required.");
                RuleFor(x => x.Request.Cheque!.PaymentGatewayRefNo).MaximumLength(50)
                    .WithMessage("Payment Gateway Ref No must be at most 50 characters.");
                RuleFor(x => x.Request.Cheque!.AckNo).MaximumLength(50).WithMessage("ACK No must be at most 50 characters.");
            });

            When(x => x.Request.BankTransfer is not null, () =>
            {
                RuleFor(x => x.Request.BankTransfer!.FaBankAccountId).NotEqual(Guid.Empty).WithMessage("Deposit Bank is required.");
            });
        });
    }
}
```

  - [ ] 8.4 CREATE `Finnova.Service/Receipts/Commands/CreateReceipt/CreateReceiptCommandHandler.cs`

```csharp
using System.Globalization;
using Finnova.Models.Contracts.Receipts;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Models.Domain.Receipts;
using Finnova.Repository.Interfaces;
using Finnova.Service.DocumentNumberControl.Commands.IssueNumber;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Finnova.Service.Receipts.Commands.CreateReceipt;

/// <summary>
/// Saves a receipt (R2 to R11). All reads and rule checks run before the Document Number is issued so a rejected
/// request never consumes a sequence value (R2.12). Receipt, children and audit entry commit in one SaveChanges.
/// </summary>
public sealed class CreateReceiptCommandHandler(
    IReceiptRepository receipts,
    IReceiptAuditRepository audits,
    IReceiptReferenceRepository refs,
    IApplicationUnitOfWork uow,
    IMediator mediator)
    : IRequestHandler<CreateReceiptCommand, CreateReceiptResult>
{
    public async Task<CreateReceiptResult> Handle(CreateReceiptCommand command, CancellationToken ct)
    {
        var r = command.Request;

        // 1. Idempotency (R2.9)
        var existing = await receipts.GetByClientRequestIdAsync(r.ClientRequestId, ct);
        if (existing is not null) return new CreateReceiptResult(ReceiptMapper.ToResponse(existing, null), true);

        // 2. Header references (R3)
        var lob = await refs.GetActiveLobAsync(r.LineOfBusinessId, ct)
            ?? throw new ReceiptValidationException("Line of Business does not exist or is inactive.");
        var branch = await refs.GetActiveBranchAsync(r.BranchId, ct)
            ?? throw new ReceiptValidationException("Location does not exist, is inactive, or is not a branch.");
        var officer = await refs.GetActiveOfficerAsync(r.MarketingOfficerUserId, ct)
            ?? throw new ReceiptValidationException("Marketing Officer is not an active user.");
        var customer = await refs.GetActiveCustomerByCodeAsync(r.CustomerCode.Trim(), ct)
            ?? throw new ReceiptNotFoundException($"Customer '{r.CustomerCode.Trim()}' was not found or is inactive.");

        // 3. Amounts (R9)
        var addLess = r.AddLess ?? Array.Empty<ReceiptAddLessRequest>();
        var computed = ReceiptCalculator.DocAmount(
            r.Lines.Select(l => l.Amount), addLess.Select(a => (a.Kind, a.Amount)));
        if (computed <= 0m)
            throw new ReceiptBusinessRuleException(ReceiptRuleKeys.DocAmountNotPositive, "The computed Document Amount must be greater than zero.");
        if (computed != r.DocAmount)
            throw new ReceiptBusinessRuleException(ReceiptRuleKeys.DocAmountMismatch, ReceiptMessages.DocAmountMismatch);

        // 4. Cash denominations (R4)
        var denominations = new List<ReceiptCashDenomination>();
        if (r.Mode == ReceiptMode.Cash)
        {
            var rows = (r.Denominations ?? Array.Empty<CashDenominationRequest>()).Where(d => d.Count > 0).ToList();
            var total = ReceiptCalculator.DenominationTotal(rows.Select(d => (d.FaceValue, d.Count)));
            if (total != r.DocAmount)
                throw new ReceiptBusinessRuleException(ReceiptRuleKeys.CashDenominationMismatch, ReceiptMessages.DenominationMismatch);
            denominations.AddRange(rows.Select(d => new ReceiptCashDenomination
            {
                Kind = d.Kind, FaceValue = d.FaceValue, Count = d.Count,
                Amount = ReceiptCalculator.DenominationAmount(d.FaceValue, d.Count)
            }));
        }

        // 5. Contracts (R7)
        var accountNumbers = r.Lines.Select(l => l.PrimeAccountNo.Trim())
            .Concat(addLess.Select(a => a.AccountNumber.Trim()))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var contracts = await refs.GetContractsAsync(accountNumbers, ct);
        foreach (var no in accountNumbers)
        {
            if (!contracts.TryGetValue(no, out var c))
                throw new ReceiptBusinessRuleException(ReceiptRuleKeys.AccountNotFound, $"Account {no} was not found.");
            if (c.Status != AccountStatus.Active || c.ClosedAt is not null)
                throw new ReceiptBusinessRuleException(ReceiptRuleKeys.AccountNotLive, $"Account {no} is not live.");
            if (c.CustomerId != customer.Id)
                throw new ReceiptBusinessRuleException(ReceiptRuleKeys.AccountCustomerMismatch,
                    $"Account {no} does not belong to customer {customer.Code}.");
        }

        // 6. Mode-specific details (R5, R6)
        ReceiptChequeDetail? cheque = null;
        ReceiptBankTransferDetail? transfer = null;
        if (r.Mode == ReceiptMode.Cheque)
        {
            var c = r.Cheque!;
            var bank = await refs.GetActiveDraweeBankAsync(c.DraweeBankId, ct)
                ?? throw new ReceiptValidationException("Drawee Bank does not exist or is inactive.");
            var place = await refs.GetDraweeBranchAsync(c.DraweeBankId, c.DraweeBranchId, ct);
            if (place is null || !place.IsEffectiveOn(r.ValueDate))
                throw new ReceiptValidationException("Drawee Branch does not belong to the Drawee Bank or is not effective on the Value Date.");
            cheque = new ReceiptChequeDetail
            {
                InstrumentNo = c.InstrumentNo.Trim(),
                InstrumentDate = c.InstrumentDate ?? ReceiptClock.TodayIst(),
                DraweeBankId = bank.Id, DraweeBankCode = bank.Code, DraweeBankName = bank.Name,
                DraweeBranchId = place.Id, DraweePlaceName = place.PlaceName,
                PaymentGatewayRefNo = Blank(c.PaymentGatewayRefNo), AckNo = Blank(c.AckNo)
            };
        }
        else if (r.Mode == ReceiptMode.BankTransfer)
        {
            var fa = await refs.GetActiveFaBankAccountAsync(r.BankTransfer!.FaBankAccountId, ct)
                ?? throw new ReceiptValidationException("Deposit Bank does not exist or is inactive.");
            transfer = new ReceiptBankTransferDetail
            {
                FaBankAccountId = fa.Id, BankName = fa.BankName, AccountNo = fa.AccountNumber, MicrCode = fa.MicrCode,
                BankBranch = fa.BankBranch, GlCode = fa.GlCode, SlCode = fa.SlCode
            };
        }

        // 7. Line components (R8.3)
        var components = (await refs.GetActiveComponentsAsync(ct)).Select(x => x.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var l in r.Lines)
            if (!components.Contains(l.ComponentCode.Trim()))
                throw new ReceiptValidationException($"Component '{l.ComponentCode.Trim()}' is not an active receipt component.");

        // 8. GL credit rule (R8) and Add/Less GL (R9.6)
        var glCache = new Dictionary<string, RefGl>(StringComparer.Ordinal);
        async Task<RefGl> ResolveGlAsync(string code)
        {
            var key = code.Trim();
            if (glCache.TryGetValue(key, out var hit)) return hit;
            var gl = await refs.GetPostableGlByCodeAsync(customer.CompanyId, key, ct)
                ?? throw new ReceiptBusinessRuleException(ReceiptRuleKeys.GlNotPostable,
                    $"GL account {key} is not an active, approved, postable account.");
            glCache[key] = gl;
            return gl;
        }

        RefGl? customerGl = null;
        if (!r.GlBased)
        {
            customerGl = customer.GlAccountId is { } gid
                ? await refs.GetPostableGlByIdAsync(customer.CompanyId, gid, ct)
                : null;
            if (customerGl is null)
                throw new ReceiptBusinessRuleException(ReceiptRuleKeys.CustomerGlNotConfigured,
                    "The customer GL account is not configured or is not an active, approved, postable account.");
        }

        var lineEntities = new List<ReceiptLine>();
        var lineNo = 0;
        foreach (var l in r.Lines)
        {
            lineNo++;
            var contract = contracts[l.PrimeAccountNo.Trim()];
            var gl = r.GlBased ? await ResolveGlAsync(l.GlAccountCode!) : customerGl!;
            lineEntities.Add(new ReceiptLine
            {
                LineNo = lineNo, LoanAccountId = contract.Id, PrimeAccountNo = contract.AccountNumber,
                SubAccountNo = Blank(l.SubAccountNo), AccountDescription = contract.AccountName,
                ComponentCode = l.ComponentCode.Trim(), CreditGlAccountId = gl.Id, CreditGlCode = gl.Code,
                SlAccountCode = r.GlBased ? Blank(l.SlAccountCode) : null,   // ignored when GL Based = No (A12)
                Amount = l.Amount
            });
        }

        var addLessEntities = new List<ReceiptAddLess>();
        var adjNo = 0;
        foreach (var a in addLess)
        {
            adjNo++;
            var contract = contracts[a.AccountNumber.Trim()];
            var gl = await ResolveGlAsync(a.GlAccountCode);
            addLessEntities.Add(new ReceiptAddLess
            {
                LineNo = adjNo, LoanAccountId = contract.Id, AccountNumber = contract.AccountNumber,
                VehicleOrMachineNo = Blank(a.VehicleOrMachineNo), Kind = a.Kind, AccountDescription = contract.AccountName,
                GlAccountId = gl.Id, GlAccountCode = gl.Code, SlAccountCode = Blank(a.SlAccountCode),
                Amount = a.Amount, Remarks = Blank(a.Remarks)
            });
        }

        // 9. Document Number (R2.1, R2.5, R2.6, R2.8)
        string docNo;
        long? sequence = null;
        if (r.EntryMode == ReceiptEntryMode.Manual)
        {
            docNo = r.DocNo!.Trim();
            if (await receipts.DocNoExistsAsync(r.ReceiptType, docNo, ct)) throw new ReceiptDuplicateDocNoException();
        }
        else
        {
            try
            {
                var issued = await mediator.Send(new IssueNumberCommand(
                    DocumentTypeFor(r.ReceiptType), NumberScope.Global, null, command.Actor,
                    r.LineOfBusinessId, r.BranchId, UseFallback: true), ct);
                docNo = issued.Number;
                sequence = issued.SequenceValue;
            }
            catch (Exception ex) when (ex is Finnova.Models.Domain.Exceptions.NumberingSchemeNotFoundException
                                          or Finnova.Models.Domain.Exceptions.NumberingSchemeInactiveException
                                          or Finnova.Models.Domain.Exceptions.NumberingSchemeValidationException)
            {
                throw new ReceiptBusinessRuleException(ReceiptRuleKeys.NumberingSchemeNotConfigured,
                    $"No active numbering scheme is configured for {r.ReceiptType} receipts.");
            }
        }

        // 10. Persist atomically (R2.3, R11.1)
        var now = DateTime.UtcNow;
        var receipt = new Receipt
        {
            ClientRequestId = r.ClientRequestId, DocNo = docNo, SequenceNumber = sequence,
            EntryMode = r.EntryMode, ReceiptType = r.ReceiptType, Status = ReceiptStatus.PendingAuthorization,
            DocDate = r.DocDate, ValueDate = r.ValueDate,
            LineOfBusinessId = lob.Id, LineOfBusinessName = lob.Name,
            BranchId = branch.Id, BranchCode = branch.Code, BranchName = branch.Name,
            MarketingOfficerUserId = officer.Id, MarketingOfficerName = officer.Name,
            Mode = r.Mode, ReceiptFrom = r.ReceiptFrom, GlBased = r.GlBased, TrRefNo = Blank(r.TrRefNo),
            CustomerId = customer.Id, CustomerCode = customer.Code, CustomerName = customer.Name, CompanyId = customer.CompanyId,
            CurrencyCode = "INR", DocAmount = computed, AmountInWords = AmountInWords.ToWords(computed),
            CreatedBy = command.Actor, CreatedAtUtc = now, UpdatedAtUtc = now,
            Lines = lineEntities, AddLess = addLessEntities, Denominations = denominations,
            Cheque = cheque, BankTransfer = transfer
        };

        var audit = new ReceiptAuditEntry
        {
            ReceiptId = receipt.Id, Action = "Created", DocNo = docNo, SequenceNumber = sequence,
            Actor = command.Actor, CorrelationId = command.CorrelationId, OccurredAtUtc = now,
            // Non-sensitive only: no instrument numbers, account numbers, or contact data (R11.6).
            ChangeSummary = string.Create(CultureInfo.InvariantCulture,
                $"Receipt {docNo} created: {r.ReceiptType} {r.Mode}, amount {computed:0.00} INR, {lineEntities.Count} line(s), {addLessEntities.Count} adjustment(s).")
        };

        await receipts.AddAsync(receipt, ct);
        await audits.AddAsync(audit, ct);
        try
        {
            await uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // A concurrent double submit or Manual Doc No race hit a unique index: resolve to the stored outcome.
            uow.Context.ChangeTracker.Clear();
            var raced = await receipts.GetByClientRequestIdAsync(r.ClientRequestId, ct);
            if (raced is not null) return new CreateReceiptResult(ReceiptMapper.ToResponse(raced, null), true);
            if (r.EntryMode == ReceiptEntryMode.Manual && await receipts.DocNoExistsAsync(r.ReceiptType, docNo, ct))
                throw new ReceiptDuplicateDocNoException();
            throw;
        }

        return new CreateReceiptResult(ReceiptMapper.ToResponse(receipt, null), false);
    }

    private static string DocumentTypeFor(ReceiptType type) => type switch
    {
        ReceiptType.Temporary => ReceiptDocumentTypes.Temporary,
        ReceiptType.Regular => ReceiptDocumentTypes.Regular,
        _ => ReceiptDocumentTypes.ThirdParty
    };

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
```

- [ ] 9. Backend service: queries
  - [ ] 9.1 CREATE `Finnova.Service/Receipts/Queries/ReceiptQueries.cs`

```csharp
using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.Receipts;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using FluentValidation;
using MediatR;

namespace Finnova.Service.Receipts.Queries;

public sealed record GetReceiptByIdQuery(Guid Id) : IRequest<ReceiptResponse>;
public sealed record GetReceiptsPagedQuery(ReceiptSearchRequest Request) : IRequest<PaginatedResponse<ReceiptListItemResponse>>;
public sealed record GetReceiptAuditTrailQuery(Guid ReceiptId) : IRequest<List<ReceiptAuditEntryResponse>>;

public sealed class GetReceiptsPagedQueryValidator : AbstractValidator<GetReceiptsPagedQuery>
{
    public GetReceiptsPagedQueryValidator()
    {
        RuleFor(x => x.Request.Page).GreaterThanOrEqualTo(1).WithMessage("Page must be at least 1.");
        RuleFor(x => x.Request.PageSize).InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");
    }
}

public sealed class GetReceiptByIdQueryHandler(IReceiptRepository receipts, IReceiptReferenceRepository refs)
    : IRequestHandler<GetReceiptByIdQuery, ReceiptResponse>
{
    public async Task<ReceiptResponse> Handle(GetReceiptByIdQuery q, CancellationToken ct)
    {
        var receipt = await receipts.GetDetailAsync(q.Id, ct) ?? throw ReceiptNotFoundException.ForReceipt(q.Id);
        // Contact data is read live, not snapshotted (data minimization).
        var c = await refs.GetCustomerByIdAsync(receipt.CustomerId, ct);
        var customer = c is null ? null
            : new ReceiptCustomerResponse(c.Id, c.Code, c.Name, c.Address, c.Phone, c.Mobile, c.Email, c.Website);
        return ReceiptMapper.ToResponse(receipt, customer);
    }
}

public sealed class GetReceiptsPagedQueryHandler(IReceiptRepository receipts)
    : IRequestHandler<GetReceiptsPagedQuery, PaginatedResponse<ReceiptListItemResponse>>
{
    public async Task<PaginatedResponse<ReceiptListItemResponse>> Handle(GetReceiptsPagedQuery q, CancellationToken ct)
    {
        var r = q.Request;
        var (items, total) = await receipts.SearchAsync(new ReceiptSearchFilter(
            r.Search, r.ReceiptType, r.Mode, r.Status, r.LineOfBusinessId, r.BranchId,
            r.DocDateFrom, r.DocDateTo, r.Page, r.PageSize), ct);
        var totalPages = (int)Math.Ceiling(total / (double)r.PageSize);
        return new PaginatedResponse<ReceiptListItemResponse>(
            items.Select(ReceiptMapper.ToListItem).ToList(), total, r.Page, r.PageSize, totalPages);
    }
}

public sealed class GetReceiptAuditTrailQueryHandler(IReceiptAuditRepository audits)
    : IRequestHandler<GetReceiptAuditTrailQuery, List<ReceiptAuditEntryResponse>>
{
    public async Task<List<ReceiptAuditEntryResponse>> Handle(GetReceiptAuditTrailQuery q, CancellationToken ct)
        => (await audits.GetByReceiptIdAsync(q.ReceiptId, ct)).Select(ReceiptMapper.ToAuditResponse).ToList();
}
```

  - [ ] 9.2 CREATE `Finnova.Service/Receipts/Queries/ReceiptReferenceQueries.cs`

```csharp
using Finnova.Models.Contracts.Applications;
using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.Lookups;
using Finnova.Models.Contracts.Receipts;
using Finnova.Models.Domain.Exceptions;
using Finnova.Models.Domain.Receipts;
using Finnova.Models.Contracts.UserManagement;
using Finnova.Repository.Interfaces;
using MediatR;

namespace Finnova.Service.Receipts.Queries;

public sealed record GetReceiptLinesOfBusinessQuery : IRequest<List<LineOfBusinessRefResponse>>;
public sealed record GetReceiptBranchesQuery : IRequest<List<ApplicationReferenceOptionResponse>>;
public sealed record SearchReceiptCustomersQuery(string? Search, int Page, int PageSize) : IRequest<PaginatedResponse<ReceiptCustomerOption>>;
public sealed record GetReceiptCustomerQuery(string CustomerCode) : IRequest<ReceiptCustomerResponse>;
public sealed record GetReceiptContractsQuery(string CustomerCode) : IRequest<List<ReceiptContractOption>>;
public sealed record GetReceiptOfficersQuery : IRequest<List<ReceiptOfficerOption>>;
public sealed record GetReceiptDraweeBanksQuery : IRequest<List<ReceiptDraweeBankOption>>;
public sealed record GetReceiptDraweeBranchesQuery(Guid BankId, DateOnly? AsOf) : IRequest<List<ReceiptDraweeBranchOption>>;
public sealed record GetReceiptBankAccountsQuery : IRequest<List<ReceiptFaBankAccountOption>>;
public sealed record GetReceiptComponentsQuery : IRequest<List<LookupDropdownItemResponse>>;
public sealed record GetCashDenominationsQuery : IRequest<List<CashDenominationOption>>;

/// <summary>One handler class for all read-only reference queries (R13). Reuses the existing LOB and branch ports.</summary>
public sealed class ReceiptReferenceQueryHandlers(
    IReceiptReferenceRepository refs,
    ILineOfBusinessReferencePort lobs,
    IBranchReferencePort branches)
    : IRequestHandler<GetReceiptLinesOfBusinessQuery, List<LineOfBusinessRefResponse>>,
      IRequestHandler<GetReceiptBranchesQuery, List<ApplicationReferenceOptionResponse>>,
      IRequestHandler<SearchReceiptCustomersQuery, PaginatedResponse<ReceiptCustomerOption>>,
      IRequestHandler<GetReceiptCustomerQuery, ReceiptCustomerResponse>,
      IRequestHandler<GetReceiptContractsQuery, List<ReceiptContractOption>>,
      IRequestHandler<GetReceiptOfficersQuery, List<ReceiptOfficerOption>>,
      IRequestHandler<GetReceiptDraweeBanksQuery, List<ReceiptDraweeBankOption>>,
      IRequestHandler<GetReceiptDraweeBranchesQuery, List<ReceiptDraweeBranchOption>>,
      IRequestHandler<GetReceiptBankAccountsQuery, List<ReceiptFaBankAccountOption>>,
      IRequestHandler<GetReceiptComponentsQuery, List<LookupDropdownItemResponse>>,
      IRequestHandler<GetCashDenominationsQuery, List<CashDenominationOption>>
{
    public Task<List<LineOfBusinessRefResponse>> Handle(GetReceiptLinesOfBusinessQuery q, CancellationToken ct)
        => lobs.GetActiveAsync(ct);

    public Task<List<ApplicationReferenceOptionResponse>> Handle(GetReceiptBranchesQuery q, CancellationToken ct)
        => branches.GetActiveAsync(null, ct);

    public async Task<PaginatedResponse<ReceiptCustomerOption>> Handle(SearchReceiptCustomersQuery q, CancellationToken ct)
    {
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 100);
        var (items, total) = await refs.SearchActiveCustomersAsync(q.Search, page, size, ct);
        return new PaginatedResponse<ReceiptCustomerOption>(
            items.Select(c => new ReceiptCustomerOption(c.Id, c.Code, c.Name)).ToList(),
            total, page, size, (int)Math.Ceiling(total / (double)size));
    }

    public async Task<ReceiptCustomerResponse> Handle(GetReceiptCustomerQuery q, CancellationToken ct)
    {
        var c = await refs.GetActiveCustomerByCodeAsync(q.CustomerCode.Trim(), ct)
            ?? throw new ReceiptNotFoundException($"Customer '{q.CustomerCode.Trim()}' was not found or is inactive.");
        return new ReceiptCustomerResponse(c.Id, c.Code, c.Name, c.Address, c.Phone, c.Mobile, c.Email, c.Website);
    }

    public async Task<List<ReceiptContractOption>> Handle(GetReceiptContractsQuery q, CancellationToken ct)
    {
        var c = await refs.GetActiveCustomerByCodeAsync(q.CustomerCode.Trim(), ct)
            ?? throw new ReceiptNotFoundException($"Customer '{q.CustomerCode.Trim()}' was not found or is inactive.");
        return (await refs.GetLiveContractsOfCustomerAsync(c.Id, ct))
            .Select(x => new ReceiptContractOption(x.AccountNumber, x.AccountName)).ToList();
    }

    public async Task<List<ReceiptOfficerOption>> Handle(GetReceiptOfficersQuery q, CancellationToken ct)
        => (await refs.GetActiveOfficersAsync(ct)).Select(x => new ReceiptOfficerOption(x.Id, x.UserCode, x.Name)).ToList();

    public async Task<List<ReceiptDraweeBankOption>> Handle(GetReceiptDraweeBanksQuery q, CancellationToken ct)
        => (await refs.GetActiveDraweeBanksAsync(ct)).Select(x => new ReceiptDraweeBankOption(x.Id, x.Code, x.Name)).ToList();

    public async Task<List<ReceiptDraweeBranchOption>> Handle(GetReceiptDraweeBranchesQuery q, CancellationToken ct)
        => (await refs.GetDraweeBranchesAsync(q.BankId, q.AsOf ?? ReceiptClock.TodayIst(), ct))
            .Select(x => new ReceiptDraweeBranchOption(x.Id, x.PlaceCode, x.PlaceName)).ToList();

    public async Task<List<ReceiptFaBankAccountOption>> Handle(GetReceiptBankAccountsQuery q, CancellationToken ct)
        => (await refs.GetActiveFaBankAccountsAsync(ct)).Select(x => new ReceiptFaBankAccountOption(
            x.Id, x.BankName, x.BankBranch, x.AccountNumber, x.MicrCode, x.GlCode, x.SlCode)).ToList();

    public Task<List<LookupDropdownItemResponse>> Handle(GetReceiptComponentsQuery q, CancellationToken ct)
        => refs.GetActiveComponentsAsync(ct);

    public Task<List<CashDenominationOption>> Handle(GetCashDenominationsQuery q, CancellationToken ct)
        => Task.FromResult(CashDenominationCatalog.Entries
            .Select(e => new CashDenominationOption(e.Kind, e.FaceValue, e.Label)).ToList());
}
```

- [ ] 10. Backend host: roles, policies, middleware, controller, gateway
  - [ ] 10.1 MODIFY `Finnova.Models/Domain/Roles/AppRoleCatalog.cs` (add two constants after `KycApprover`, and two definitions at the end of the `Roles` array)

```csharp
    public const string Cashier = "Cashier";
    public const string BranchCollectionOfficer = "BranchCollectionOfficer";
```

```csharp
        new AppRoleDefinition(new Guid("00000000-0000-0000-0003-000000000005"), Cashier, "Captures receipts at the branch counter."),
        new AppRoleDefinition(new Guid("00000000-0000-0000-0003-000000000006"), BranchCollectionOfficer, "Captures collection receipts for branch customers."),
```

  - [ ] 10.2 MODIFY `Finnova.Tests/LoanAccounts/RoleSeedingAndGatewayTests.cs`. In `Seeding_creates_the_four_roles_exactly_once_even_when_applied_twice`, replace `Assert.Equal(4, db.AppRoles.Count());` with:

```csharp
        Assert.Equal(AppRoleCatalog.Roles.Count, db.AppRoles.Count());
```

  - [ ] 10.3 MODIFY `Finnova.AccountsService/Program.cs`. Inside `builder.Services.AddAuthorization(options => { ... })`, after the `AccountsOps` policy and before the closing `});`, add:

```csharp
    // Receipt Processing (FINNOVA-61). Both policies accept the same roles; they are separate so reads can be widened later.
    options.AddPolicy("ReceiptEntry", p => p.RequireAuthenticatedUser().RequireAssertion(ctx =>
        ctx.User.IsInRole("Cashier") || ctx.User.IsInRole("BranchCollectionOfficer") || ctx.User.IsInRole("SystemAdmin")));
    options.AddPolicy("ReceiptRead", p => p.RequireAuthenticatedUser().RequireAssertion(ctx =>
        ctx.User.IsInRole("Cashier") || ctx.User.IsInRole("BranchCollectionOfficer") || ctx.User.IsInRole("SystemAdmin")));
```

  - [ ] 10.4 MODIFY `Finnova.AccountsService/Middlewares/ExceptionHandlingMiddleware.cs` (four edits).

Edit A. After the line `var isLoan = ctx.Request.Path.StartsWithSegments("/api/loan-accounts", StringComparison.OrdinalIgnoreCase);` add:

```csharp
            var isReceipt = ctx.Request.Path.StartsWithSegments("/api/receipts", StringComparison.OrdinalIgnoreCase);
```

Edit B. In the switch, immediately before the arm `LoanAccountException l => (l.ErrorCode switch`, add:

```csharp
                ReceiptException rc => (rc.StatusCode, rc.ErrorCode, rc.Message),
```

Edit C. Replace the three shared arms so they are receipt aware. Replace `(StatusCodes.Status409Conflict, isLoan ? "ERR-ACC-409" : "ERR-GL-409",` with:

```csharp
                    (StatusCodes.Status409Conflict, isReceipt ? "ERR-RCP-409" : isLoan ? "ERR-ACC-409" : "ERR-GL-409",
```

Replace the line starting `FluentValidation.ValidationException v =>` with:

```csharp
                FluentValidation.ValidationException v => (StatusCodes.Status400BadRequest,
                    isReceipt ? "ERR-RCP-400" : isLoan ? "ERR-ACC-400" : validationCode,
                    isReceipt ? string.Join("; ", v.Errors.Select(e => e.ErrorMessage).Distinct()) : v.Message),
```

Replace the line starting `_ => (StatusCodes.Status500InternalServerError,` with:

```csharp
                _ => (StatusCodes.Status500InternalServerError, isReceipt ? "ERR-RCP-500" : isLoan ? "ERR-ACC-500" : fallbackCode, "Unexpected error.")
```

Edit D. After the line `problem.Extensions["correlationId"] = ctx.TraceIdentifier;` add:

```csharp
            if (ex is ReceiptException { Rule: { } rule }) problem.Extensions["rule"] = rule;
```

  - [ ] 10.5 CREATE `Finnova.AccountsService/Controllers/ReceiptsController.cs`

```csharp
using System.Security.Claims;
using Finnova.Models.Contracts.Applications;
using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.Lookups;
using Finnova.Models.Contracts.Receipts;
using Finnova.Models.Contracts.UserManagement;
using Finnova.Service.Receipts.Commands.CreateReceipt;
using Finnova.Service.Receipts.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finnova.AccountsService.Controllers;

[ApiController, Route("api/receipts")]
public sealed class ReceiptsController(IMediator mediator) : ControllerBase
{
    // Identity comes from the validated JWT, never from the request body (R14.4).
    private string ActorText => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? "authenticated-user";

    [HttpPost, Authorize(Policy = "ReceiptEntry")]
    public async Task<ActionResult<ReceiptResponse>> Create(CreateReceiptRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new CreateReceiptCommand(request, ActorText, HttpContext.TraceIdentifier), ct);
        if (result.AlreadyExisted) return Ok(result.Receipt);
        return CreatedAtAction(nameof(GetById), new { id = result.Receipt.Id }, result.Receipt);
    }

    [HttpGet, Authorize(Policy = "ReceiptRead")]
    public async Task<ActionResult<PaginatedResponse<ReceiptListItemResponse>>> List([FromQuery] ReceiptSearchRequest request, CancellationToken ct)
        => Ok(await mediator.Send(new GetReceiptsPagedQuery(request), ct));

    [HttpGet("{id:guid}"), Authorize(Policy = "ReceiptRead")]
    public async Task<ActionResult<ReceiptResponse>> GetById(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new GetReceiptByIdQuery(id), ct));

    [HttpGet("{id:guid}/audit-trail"), Authorize(Policy = "ReceiptRead")]
    public async Task<ActionResult<List<ReceiptAuditEntryResponse>>> AuditTrail(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new GetReceiptAuditTrailQuery(id), ct));

    // ---------- reference data (R13) ----------
    [HttpGet("references/lines-of-business"), Authorize(Policy = "ReceiptRead")]
    public async Task<ActionResult<List<LineOfBusinessRefResponse>>> LinesOfBusiness(CancellationToken ct)
        => Ok(await mediator.Send(new GetReceiptLinesOfBusinessQuery(), ct));

    [HttpGet("references/branches"), Authorize(Policy = "ReceiptRead")]
    public async Task<ActionResult<List<ApplicationReferenceOptionResponse>>> Branches(CancellationToken ct)
        => Ok(await mediator.Send(new GetReceiptBranchesQuery(), ct));

    [HttpGet("references/customers"), Authorize(Policy = "ReceiptRead")]
    public async Task<ActionResult<PaginatedResponse<ReceiptCustomerOption>>> Customers(
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await mediator.Send(new SearchReceiptCustomersQuery(search, page, pageSize), ct));

    [HttpGet("references/customers/{code}"), Authorize(Policy = "ReceiptRead")]
    public async Task<ActionResult<ReceiptCustomerResponse>> Customer(string code, CancellationToken ct)
        => Ok(await mediator.Send(new GetReceiptCustomerQuery(code), ct));

    [HttpGet("references/customers/{code}/contracts"), Authorize(Policy = "ReceiptRead")]
    public async Task<ActionResult<List<ReceiptContractOption>>> Contracts(string code, CancellationToken ct)
        => Ok(await mediator.Send(new GetReceiptContractsQuery(code), ct));

    [HttpGet("references/marketing-officers"), Authorize(Policy = "ReceiptRead")]
    public async Task<ActionResult<List<ReceiptOfficerOption>>> MarketingOfficers(CancellationToken ct)
        => Ok(await mediator.Send(new GetReceiptOfficersQuery(), ct));

    [HttpGet("references/drawee-banks"), Authorize(Policy = "ReceiptRead")]
    public async Task<ActionResult<List<ReceiptDraweeBankOption>>> DraweeBanks(CancellationToken ct)
        => Ok(await mediator.Send(new GetReceiptDraweeBanksQuery(), ct));

    [HttpGet("references/drawee-banks/{id:guid}/branches"), Authorize(Policy = "ReceiptRead")]
    public async Task<ActionResult<List<ReceiptDraweeBranchOption>>> DraweeBranches(Guid id, [FromQuery] DateOnly? asOf, CancellationToken ct)
        => Ok(await mediator.Send(new GetReceiptDraweeBranchesQuery(id, asOf), ct));

    [HttpGet("references/bank-accounts"), Authorize(Policy = "ReceiptRead")]
    public async Task<ActionResult<List<ReceiptFaBankAccountOption>>> BankAccounts(CancellationToken ct)
        => Ok(await mediator.Send(new GetReceiptBankAccountsQuery(), ct));

    [HttpGet("references/components"), Authorize(Policy = "ReceiptRead")]
    public async Task<ActionResult<List<LookupDropdownItemResponse>>> Components(CancellationToken ct)
        => Ok(await mediator.Send(new GetReceiptComponentsQuery(), ct));

    [HttpGet("references/denominations"), Authorize(Policy = "ReceiptRead")]
    public async Task<ActionResult<List<CashDenominationOption>>> Denominations(CancellationToken ct)
        => Ok(await mediator.Send(new GetCashDenominationsQuery(), ct));
}
```

  - [ ] 10.6 MODIFY `Finnova.ApiGateway/appsettings.json`. Inside `ReverseProxy.Routes`, directly after the `ua-loan-accounts-alias-root` route object, add (keep the comma between route objects):

```json
      "ua-receipts-alias-route": {
        "ClusterId": "accounts-cluster",
        "Match": { "Path": "/api/ua/api/receipts/{**catch-all}" },
        "Transforms": [
          { "PathRemovePrefix": "/api/ua/api" },
          { "PathPrefix": "/api" }
        ]
      },
      "ua-receipts-alias-root": {
        "ClusterId": "accounts-cluster",
        "Match": { "Path": "/api/ua/api/receipts" },
        "Transforms": [
          { "PathRemovePrefix": "/api/ua/api" },
          { "PathPrefix": "/api" }
        ]
      },
```

  - [ ] 10.7 Build and migrate (run yourself, not part of the copy-paste):

```powershell
dotnet build Finnova.Backend.slnx
dotnet ef migrations add AddReceiptProcessing --project Finnova.Repository --startup-project Finnova.AccountsService
dotnet ef database update --project Finnova.Repository --startup-project Finnova.AccountsService
```

- [ ] 11. Dev seed script for F&A bank accounts
  - [ ] 11.1 CREATE `docs/receipt-processing-dev-seed.sql` (development only; run after the migration; India-only sample values)

```sql
-- Receipt Processing dev seed (FINNOVA-61). Do not run in production: the F&A Bank Master owns this table later.
INSERT INTO fa_bank_accounts (Id, BankName, BankBranch, AccountNumber, MicrCode, GlCode, SlCode, IsActive, CreatedAt, UpdatedAt)
VALUES
  (NEWID(), 'HDFC Bank Ltd',      'Mumbai Fort',  '50200012345678', '400240002', '110100101', NULL,   1, SYSUTCDATETIME(), SYSUTCDATETIME()),
  (NEWID(), 'State Bank of India', 'Pune Camp',    '30012345678',    '411002003', '110100102', 'SL01', 1, SYSUTCDATETIME(), SYSUTCDATETIME());
```

- [ ] 12. Backend tests (xUnit, FsCheck.Xunit, Moq, EF InMemory, WebApplicationFactory)
  - [ ] 12.1 CREATE `Finnova.Tests/Receipts/ReceiptHelperProperties.cs` (properties 1, 2, 3, 5; at least 100 iterations each)

```csharp
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Receipts;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace Finnova.Tests.Receipts;

public sealed class ReceiptHelperProperties
{
    // Feature: receipt-processing, Property 1: Doc Amount identity
    [Property(MaxTest = 200)]
    public bool DocAmount_equals_lines_plus_add_minus_less(NonEmptyArray<PositiveInt> lines, PositiveInt[] adds, PositiveInt[] lesses)
    {
        var l = lines.Get.Select(x => x.Get / 100m).ToList();
        var rows = adds.Select(x => (AddLessKind.Add, x.Get / 100m))
            .Concat(lesses.Select(x => (AddLessKind.Less, x.Get / 100m))).ToList();
        var expected = l.Sum() + adds.Sum(x => x.Get / 100m) - lesses.Sum(x => x.Get / 100m);
        var forward = ReceiptCalculator.DocAmount(l, rows);
        var reversed = ReceiptCalculator.DocAmount(Enumerable.Reverse(l), Enumerable.Reverse(rows));
        return forward == expected && reversed == expected;
    }

    // Feature: receipt-processing, Property 2: Adjustment inverse
    [Property(MaxTest = 200)]
    public bool Add_and_Less_of_the_same_value_cancel(NonEmptyArray<PositiveInt> lines, PositiveInt x)
    {
        var l = lines.Get.Select(v => v.Get / 100m).ToList();
        var amount = x.Get / 100m;
        var withPair = new[] { (AddLessKind.Add, amount), (AddLessKind.Less, amount) };
        return ReceiptCalculator.DocAmount(l, withPair) == ReceiptCalculator.LinesTotal(l);
    }

    // Feature: receipt-processing, Property 3: Denomination total
    [Property(MaxTest = 200)]
    public bool Denomination_total_is_the_sum_of_face_value_times_count(NonNegativeInt[] counts)
    {
        var entries = CashDenominationCatalog.Entries;
        var rows = counts.Take(entries.Count).Select((c, i) => (entries[i].FaceValue, c.Get % 1000)).ToList();
        var expected = rows.Sum(r => r.FaceValue * r.Item2);
        var perRow = rows.Sum(r => ReceiptCalculator.DenominationAmount(r.FaceValue, r.Item2));
        return ReceiptCalculator.DenominationTotal(rows) == expected && perRow == expected;
    }

    // Feature: receipt-processing, Property 5: Amount in words
    [Property(MaxTest = 200)]
    public bool Amount_in_words_has_the_required_shape(NonNegativeInt rupees, byte paiseRaw)
    {
        var paise = paiseRaw % 100;
        var amount = (rupees.Get % 999_999_999) + paise / 100m;
        var words = AmountInWords.ToWords(amount);
        return words.StartsWith("Rupees ", StringComparison.Ordinal)
            && words.EndsWith(" Only", StringComparison.Ordinal)
            && words.Contains("Paise", StringComparison.Ordinal) == (paise != 0)
            && words == AmountInWords.ToWords(amount);
    }

    [Theory]
    [InlineData("0", "Rupees Zero Only")]
    [InlineData("0.05", "Rupees Zero and Five Paise Only")]
    [InlineData("3500", "Rupees Three Thousand Five Hundred Only")]
    [InlineData("100000", "Rupees One Lakh Only")]
    [InlineData("10000000", "Rupees One Crore Only")]
    [InlineData("1234567.50", "Rupees Twelve Lakh Thirty-Four Thousand Five Hundred Sixty-Seven and Fifty Paise Only")]
    [InlineData("999999999.99", "Rupees Ninety-Nine Crore Ninety-Nine Lakh Ninety-Nine Thousand Nine Hundred Ninety-Nine and Ninety-Nine Paise Only")]
    public void Amount_in_words_known_vectors(string amount, string expected)
        => Assert.Equal(expected, AmountInWords.ToWords(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture)));

    [Theory]
    [InlineData("-0.01")]
    [InlineData("1000000000")]
    public void Amount_in_words_rejects_out_of_range(string amount)
        => Assert.Throws<ArgumentOutOfRangeException>(() =>
            AmountInWords.ToWords(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void Catalog_contains_the_indian_denominations_only()
    {
        Assert.True(CashDenominationCatalog.Contains(DenominationKind.Note, 500m));
        Assert.True(CashDenominationCatalog.Contains(DenominationKind.Coin, 0.50m));
        Assert.False(CashDenominationCatalog.Contains(DenominationKind.Note, 2000m));
        Assert.False(CashDenominationCatalog.Contains(DenominationKind.Coin, 500m));
    }
}
```

  - [ ] 12.2 CREATE `Finnova.Tests/Receipts/ReceiptTestData.cs` (shared seed and request builders)

```csharp
using Finnova.Models.Contracts.Receipts;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Receipts;
using Finnova.Repository.Context;

namespace Finnova.Tests.Receipts;

internal sealed record ReceiptSeed(
    Guid LobId, Guid BranchId, Guid OfficerId, Guid CustomerId, Guid CompanyId, Guid GlId,
    Guid DraweeBankId, Guid DraweeBranchId, Guid FaBankId,
    string CustomerCode, string AccountNumber, string ClosedAccountNumber, string OtherCustomerAccountNumber);

internal static class ReceiptTestData
{
    public const string GlCode = "110100101";

    private static Customer NewCustomer(Guid companyId, string code, string firstName, Guid? glId)
    {
        var c = new Customer();
        c.SetGeneratedCode(code);
        c.Initialize(companyId, true, false, false, "IND",
            null, null, null, glId, null, null, null, null, null, null, null, null,   // groupCode .. subSectorCode (12)
            firstName, null, null, null, null, null,                                   // firstName .. sixthName (6)
            null, null, null, null, null, null, null, null);                           // notes .. employerEconomicActivityCode (8)
        return c;
    }

    public static ReceiptSeed Seed(FinnovaDbContext db)
    {
        // Unique codes per call so several seeds can share one in-memory database (API tests).
        var tag = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var customerCode = "CUST-" + tag;
        var accountNo = "ACC-" + tag;
        var closedNo = "ACC-C-" + tag;
        var otherNo = "ACC-O-" + tag;
        var companyId = Guid.NewGuid();
        var gl = new GLAccount
        {
            CompanyId = companyId, GLCode = GlCode, Name = "Loan Receivable", Level = GLLevel.L4,
            Status = GLStatus.Active, AuthorizedVersionNumber = 1, IsPostable = true
        };
        var customer = NewCustomer(companyId, customerCode, "Ravi Kumar", gl.Id);
        var other = NewCustomer(companyId, "CUSTO-" + tag, "Asha Rao", gl.Id);
        var lob = new LineOfBusiness { LOB_Name = "Vehicle", LOB_Description = "Vehicle finance" };
        var branch = new Location { Code = "BR001", Name = "Mumbai Fort Branch", Level = 5, IsActive = true };
        var officer = new UserAccount { UserCode = "MO001", UserName = "mo1", Name = "Meera Shah", IsActive = true };
        var bank = new DraweeBank { BankCode = "HDFC", BankName = "HDFC Bank Ltd" };
        var place = new DraweeBranch
        {
            DraweeBankId = bank.Id, PlaceCode = "MUM", PlaceName = "Mumbai", PostalCode = "400001",
            Address = "Fort", StartDate = new DateTime(2020, 1, 1)
        };
        var fa = new FaBankAccount
        {
            BankName = "HDFC Bank Ltd", BankBranch = "Mumbai Fort", AccountNumber = "50200012345678",
            MicrCode = "400240002", GlCode = "110100101", SlCode = "SL01"
        };

        db.GLAccounts.Add(gl);
        db.Customers.AddRange(customer, other);
        db.LinesOfBusiness.Add(lob);
        db.Locations.Add(branch);
        db.UserAccounts.Add(officer);
        db.DraweeBanks.Add(bank);
        db.DraweeBranches.Add(place);
        db.FaBankAccounts.Add(fa);
        db.LoanAccounts.AddRange(
            new LoanAccount { AccountNumber = accountNo, AccountName = "Vehicle Loan - Ravi Kumar", CustomerId = customer.Id, Status = AccountStatus.Active },
            new LoanAccount { AccountNumber = closedNo, AccountName = "Closed Loan", CustomerId = customer.Id, Status = AccountStatus.Closed, ClosedAt = DateTime.UtcNow },
            new LoanAccount { AccountNumber = otherNo, AccountName = "Other Loan", CustomerId = other.Id, Status = AccountStatus.Active });
        if (!db.LookupValues.Any(x => x.Module == ReceiptLookup.Module && x.LookupType == ReceiptLookup.ComponentType))
            db.LookupValues.AddRange(
                new LookupValue { Module = ReceiptLookup.Module, LookupType = ReceiptLookup.ComponentType, Code = "INSTALLMENT_INFLOW", Value = "Installment Inflow" },
                new LookupValue { Module = ReceiptLookup.Module, LookupType = ReceiptLookup.ComponentType, Code = "ODI", Value = "Overdue Interest (ODI)" });
        db.SaveChanges();
        return new ReceiptSeed(lob.Id, branch.Id, officer.Id, customer.Id, companyId, gl.Id, bank.Id, place.Id, fa.Id,
            customerCode, accountNo, closedNo, otherNo);
    }

    /// <summary>Gherkin scenario 1: Regular Cheque receipt, 3200.00 Installment Inflow + 300.00 ODI, GL Based = Yes.</summary>
    public static CreateReceiptRequest ChequeRequest(ReceiptSeed s)
    {
        var today = ReceiptClock.TodayIst();
        return new CreateReceiptRequest(
            Guid.NewGuid(), ReceiptEntryMode.Online, null, ReceiptType.Regular, today, today,
            s.LobId, s.BranchId, s.OfficerId, ReceiptMode.Cheque, ReceiptFrom.Customer, true, s.CustomerCode, null, 3500m,
            new[]
            {
                new ReceiptLineRequest(s.AccountNumber, null, "INSTALLMENT_INFLOW", GlCode, null, 3200m),
                new ReceiptLineRequest(s.AccountNumber, null, "ODI", GlCode, null, 300m)
            },
            null, null, new ChequeDetailRequest("458921", today, s.DraweeBankId, s.DraweeBranchId, null, null), null);
    }

    /// <summary>Cash receipt of 500.00 with one 500 note.</summary>
    public static CreateReceiptRequest CashRequest(ReceiptSeed s) => ChequeRequest(s) with
    {
        Mode = ReceiptMode.Cash, DocAmount = 500m, Cheque = null,
        Lines = new[] { new ReceiptLineRequest(s.AccountNumber, null, "INSTALLMENT_INFLOW", GlCode, null, 500m) },
        Denominations = new[] { new CashDenominationRequest(DenominationKind.Note, 500m, 1) }
    };
}
```

  - [ ] 12.3 CREATE `Finnova.Tests/Receipts/ReceiptHandlerTests.cs` (properties 6 to 9 and the Jira scenarios)

```csharp
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
            Mode = ReceiptMode.BankTransfer, Denominations = null,
            BankTransfer = new BankTransferDetailRequest(h.Seed.FaBankId)
        };
        var result = await h.Save(request);
        Assert.Equal("50200012345678", result.Receipt.BankTransfer!.AccountNo);
        Assert.Equal("400240002", result.Receipt.BankTransfer.MicrCode);
        Assert.Equal("SL01", result.Receipt.BankTransfer.SlCode);
    }
}
```

  - [ ] 12.4 CREATE `Finnova.Tests/Receipts/CreateReceiptCommandValidatorTests.cs`

```csharp
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
```

  - [ ] 12.5 CREATE `Finnova.Tests/Receipts/ReceiptsApiTests.cs` (authorization, 201/422, list). Uses Manual entry so that no DCN scheme is needed. Requires `FINNOVA_TEST_JWT_KEY` like the other API tests.

```csharp
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
```

- [ ] 13. Frontend: model, service interface, mock, real, toggle, barrels (repository `D:\Projects\Finnova\Finnova-UI`)
  - [ ] 13.1 CREATE `src/models/receipt.model.ts`

```ts
import type { PaginatedResponse } from './api.model';

export type ReceiptType = 'Temporary' | 'Regular' | 'ThirdParty';
export type ReceiptMode = 'Cash' | 'Cheque' | 'BankTransfer';
export type ReceiptEntryMode = 'Online' | 'Manual';
export type ReceiptStatus = 'PendingAuthorization' | 'Authorized' | 'Revoked';
export type ReceiptFrom = 'Customer' | 'Guarantor' | 'ThirdParty';
export type AddLessKind = 'Add' | 'Less';
export type DenominationKind = 'Note' | 'Coin';

// ---------- requests ----------
export interface ReceiptLineRequest {
  primeAccountNo: string;
  subAccountNo?: string | null;
  componentCode: string;
  glAccountCode?: string | null;
  slAccountCode?: string | null;
  amount: number;
}

export interface ReceiptAddLessRequest {
  accountNumber: string;
  vehicleOrMachineNo?: string | null;
  kind: AddLessKind;
  glAccountCode: string;
  slAccountCode?: string | null;
  amount: number;
  remarks?: string | null;
}

export interface CashDenominationRequest { kind: DenominationKind; faceValue: number; count: number }

export interface ChequeDetailRequest {
  instrumentNo: string;
  instrumentDate?: string | null;
  draweeBankId: string;
  draweeBranchId: string;
  paymentGatewayRefNo?: string | null;
  ackNo?: string | null;
}

export interface BankTransferDetailRequest { faBankAccountId: string }

export interface CreateReceiptRequest {
  clientRequestId: string;
  entryMode: ReceiptEntryMode;
  docNo: string | null;
  receiptType: ReceiptType;
  docDate: string;
  valueDate: string;
  lineOfBusinessId: string;
  branchId: string;
  marketingOfficerUserId: string;
  mode: ReceiptMode;
  receiptFrom: ReceiptFrom;
  glBased: boolean;
  customerCode: string;
  trRefNo: string | null;
  docAmount: number;
  lines: ReceiptLineRequest[];
  addLess: ReceiptAddLessRequest[] | null;
  denominations: CashDenominationRequest[] | null;
  cheque: ChequeDetailRequest | null;
  bankTransfer: BankTransferDetailRequest | null;
}

// ---------- responses ----------
export interface ReceiptLine {
  id: string; lineNo: number; primeAccountNo: string; subAccountNo: string | null; accountDescription: string;
  componentCode: string; creditGlCode: string; slAccountCode: string | null; amount: number;
}

export interface ReceiptAddLess {
  id: string; lineNo: number; accountNumber: string; vehicleOrMachineNo: string | null; kind: AddLessKind;
  accountDescription: string; glAccountCode: string; slAccountCode: string | null; amount: number; remarks: string | null;
}

export interface CashDenomination { kind: DenominationKind; faceValue: number; count: number; amount: number }

export interface ChequeDetail {
  instrumentNo: string; instrumentDate: string; draweeBankId: string; draweeBankCode: string; draweeBankName: string;
  draweeBranchId: string; draweePlaceName: string; paymentGatewayRefNo: string | null; ackNo: string | null;
}

export interface BankTransferDetail {
  faBankAccountId: string; bankName: string; accountNo: string; micrCode: string; bankBranch: string;
  glCode: string; slCode: string | null;
}

export interface ReceiptCustomer {
  id: string; customerCode: string; customerName: string; address: string | null; phone: string | null;
  mobile: string | null; email: string | null; website: string | null;
}

export interface Receipt {
  id: string; docNo: string; sequenceNumber: number | null; entryMode: ReceiptEntryMode; receiptType: ReceiptType;
  status: ReceiptStatus; statusText: string; docDate: string; valueDate: string;
  lineOfBusinessId: string; lineOfBusinessName: string; branchId: string; branchCode: string; branchName: string;
  marketingOfficerUserId: string; marketingOfficerName: string; mode: ReceiptMode; receiptFrom: ReceiptFrom;
  glBased: boolean; trRefNo: string | null; customerCode: string; customerName: string; currencyCode: string;
  docAmount: number; amountInWords: string;
  lines: ReceiptLine[]; addLess: ReceiptAddLess[]; denominations: CashDenomination[];
  cheque: ChequeDetail | null; bankTransfer: BankTransferDetail | null; customer: ReceiptCustomer | null;
  authorizedBy: string | null; authorizedAtUtc: string | null; createdBy: string; createdAtUtc: string;
}

export interface ReceiptListItem {
  id: string; docNo: string; receiptType: ReceiptType; mode: ReceiptMode; status: ReceiptStatus; statusText: string;
  docDate: string; valueDate: string; customerCode: string; customerName: string; branchName: string;
  docAmount: number; createdAtUtc: string;
}

export interface ReceiptAuditEntry {
  id: string; receiptId: string; action: string; docNo: string; sequenceNumber: number | null; actor: string;
  correlationId: string; changeSummary: string; occurredAtUtc: string;
}

export interface CreateReceiptResult { receipt: Receipt; alreadyExisted: boolean }
export type ReceiptPage = PaginatedResponse<ReceiptListItem>;

// ---------- reference data ----------
export interface ReceiptLobOption { id: string; lobName: string; lobDescription: string }
export interface ReceiptBranchOption { id: string; code: string; label: string }
export interface ReceiptCustomerOption { id: string; customerCode: string; customerName: string }
export interface ReceiptContractOption { accountNumber: string; accountName: string }
export interface ReceiptOfficerOption { id: string; userCode: string; name: string }
export interface ReceiptDraweeBankOption { id: string; bankCode: string; bankName: string }
export interface ReceiptDraweeBranchOption { id: string; placeCode: string; placeName: string }
export interface ReceiptFaBankAccountOption {
  id: string; bankName: string; bankBranch: string; accountNumber: string; micrCode: string; glCode: string; slCode: string | null;
}
export interface ReceiptComponentOption { code: string; label: string }
export interface CashDenominationOption { kind: DenominationKind; faceValue: number; label: string }

export const RECEIPT_TYPE_LABELS: Record<ReceiptType, string> = {
  Temporary: 'Temporary Receipt', Regular: 'Regular Receipt', ThirdParty: 'Third-Party Receipt',
};
export const RECEIPT_MODE_LABELS: Record<ReceiptMode, string> = {
  Cash: 'Cash', Cheque: 'Cheque', BankTransfer: 'Bank Transfer',
};
export const RECEIPT_FROM_LABELS: Record<ReceiptFrom, string> = {
  Customer: 'Customer', Guarantor: 'Guarantor', ThirdParty: 'Third Party',
};
```

  - [ ] 13.2 CREATE `src/services/interfaces/receipt.interface.ts`

```ts
import type {
  CashDenominationOption, CreateReceiptRequest, CreateReceiptResult, Receipt, ReceiptAuditEntry, ReceiptBranchOption,
  ReceiptComponentOption, ReceiptContractOption, ReceiptCustomer, ReceiptCustomerOption, ReceiptDraweeBankOption,
  ReceiptDraweeBranchOption, ReceiptFaBankAccountOption, ReceiptLobOption, ReceiptMode, ReceiptOfficerOption,
  ReceiptPage, ReceiptStatus, ReceiptType,
} from '../../models/receipt.model';
import type { PaginatedResponse } from '../../models/api.model';

export interface ReceiptListQuery {
  search?: string;
  receiptType?: ReceiptType;
  mode?: ReceiptMode;
  status?: ReceiptStatus;
  lineOfBusinessId?: string;
  branchId?: string;
  docDateFrom?: string;
  docDateTo?: string;
  page?: number;
  pageSize?: number;
}

export interface IReceiptService {
  create(request: CreateReceiptRequest): Promise<CreateReceiptResult>;
  list(query: ReceiptListQuery): Promise<ReceiptPage>;
  getById(id: string): Promise<Receipt>;
  getAuditTrail(id: string): Promise<ReceiptAuditEntry[]>;

  getLinesOfBusiness(): Promise<ReceiptLobOption[]>;
  getBranches(): Promise<ReceiptBranchOption[]>;
  searchCustomers(search: string, page?: number, pageSize?: number): Promise<PaginatedResponse<ReceiptCustomerOption>>;
  getCustomer(customerCode: string): Promise<ReceiptCustomer>;
  getContracts(customerCode: string): Promise<ReceiptContractOption[]>;
  getMarketingOfficers(): Promise<ReceiptOfficerOption[]>;
  getDraweeBanks(): Promise<ReceiptDraweeBankOption[]>;
  getDraweeBranches(bankId: string, asOf?: string): Promise<ReceiptDraweeBranchOption[]>;
  getBankAccounts(): Promise<ReceiptFaBankAccountOption[]>;
  getComponents(): Promise<ReceiptComponentOption[]>;
  getDenominations(): Promise<CashDenominationOption[]>;
}
```

  - [ ] 13.3 CREATE `src/services/real/receipt.real.ts`

```ts
import api from '../api';
import type { IReceiptService, ReceiptListQuery } from '../interfaces/receipt.interface';
import type {
  CashDenominationOption, CreateReceiptRequest, CreateReceiptResult, Receipt, ReceiptAuditEntry, ReceiptBranchOption,
  ReceiptComponentOption, ReceiptContractOption, ReceiptCustomer, ReceiptCustomerOption, ReceiptDraweeBankOption,
  ReceiptDraweeBranchOption, ReceiptFaBankAccountOption, ReceiptLobOption, ReceiptOfficerOption, ReceiptPage,
} from '../../models/receipt.model';
import type { PaginatedResponse } from '../../models/api.model';

// The shared axios base URL already carries /api/ua/api; the gateway aliases /api/ua/api/receipts/**
// onto the accounts cluster (/api/receipts/**).
const base = '/receipts';
const ref = `${base}/references`;

export const receiptRealService: IReceiptService = {
  async create(request: CreateReceiptRequest): Promise<CreateReceiptResult> {
    const res = await api.post<Receipt>(base, request);
    return { receipt: res.data, alreadyExisted: res.status === 200 };
  },
  async list(query: ReceiptListQuery): Promise<ReceiptPage> {
    const { data } = await api.get<ReceiptPage>(base, { params: query });
    return data;
  },
  async getById(id: string): Promise<Receipt> {
    const { data } = await api.get<Receipt>(`${base}/${id}`);
    return data;
  },
  async getAuditTrail(id: string): Promise<ReceiptAuditEntry[]> {
    const { data } = await api.get<ReceiptAuditEntry[]>(`${base}/${id}/audit-trail`);
    return data;
  },

  async getLinesOfBusiness(): Promise<ReceiptLobOption[]> {
    const { data } = await api.get<ReceiptLobOption[]>(`${ref}/lines-of-business`);
    return data;
  },
  async getBranches(): Promise<ReceiptBranchOption[]> {
    const { data } = await api.get<ReceiptBranchOption[]>(`${ref}/branches`);
    return data;
  },
  async searchCustomers(search: string, page = 1, pageSize = 20): Promise<PaginatedResponse<ReceiptCustomerOption>> {
    const { data } = await api.get<PaginatedResponse<ReceiptCustomerOption>>(`${ref}/customers`, { params: { search, page, pageSize } });
    return data;
  },
  async getCustomer(customerCode: string): Promise<ReceiptCustomer> {
    const { data } = await api.get<ReceiptCustomer>(`${ref}/customers/${encodeURIComponent(customerCode)}`);
    return data;
  },
  async getContracts(customerCode: string): Promise<ReceiptContractOption[]> {
    const { data } = await api.get<ReceiptContractOption[]>(`${ref}/customers/${encodeURIComponent(customerCode)}/contracts`);
    return data;
  },
  async getMarketingOfficers(): Promise<ReceiptOfficerOption[]> {
    const { data } = await api.get<ReceiptOfficerOption[]>(`${ref}/marketing-officers`);
    return data;
  },
  async getDraweeBanks(): Promise<ReceiptDraweeBankOption[]> {
    const { data } = await api.get<ReceiptDraweeBankOption[]>(`${ref}/drawee-banks`);
    return data;
  },
  async getDraweeBranches(bankId: string, asOf?: string): Promise<ReceiptDraweeBranchOption[]> {
    const { data } = await api.get<ReceiptDraweeBranchOption[]>(`${ref}/drawee-banks/${bankId}/branches`, { params: { asOf } });
    return data;
  },
  async getBankAccounts(): Promise<ReceiptFaBankAccountOption[]> {
    const { data } = await api.get<ReceiptFaBankAccountOption[]>(`${ref}/bank-accounts`);
    return data;
  },
  async getComponents(): Promise<ReceiptComponentOption[]> {
    const { data } = await api.get<ReceiptComponentOption[]>(`${ref}/components`);
    return data;
  },
  async getDenominations(): Promise<CashDenominationOption[]> {
    const { data } = await api.get<CashDenominationOption[]>(`${ref}/denominations`);
    return data;
  },
};
```

  - [ ] 13.4 CREATE `src/services/mock/receipt.mock.ts`

```ts
import type { IReceiptService, ReceiptListQuery } from '../interfaces/receipt.interface';
import type {
  CashDenominationOption, CreateReceiptRequest, CreateReceiptResult, Receipt, ReceiptAuditEntry, ReceiptBranchOption,
  ReceiptComponentOption, ReceiptContractOption, ReceiptCustomer, ReceiptCustomerOption, ReceiptDraweeBankOption,
  ReceiptDraweeBranchOption, ReceiptFaBankAccountOption, ReceiptLobOption, ReceiptOfficerOption, ReceiptPage, ReceiptType,
} from '../../models/receipt.model';
import type { PaginatedResponse } from '../../models/api.model';
import { amountInWords } from '../../utils/amountInWords';
import { denominationTotal, docAmount as computeDocAmount, toPaise } from '../../utils/receiptCalculator';
import { DENOMINATION_MISMATCH_MESSAGE } from '../../utils/receiptForm';

// India-only, English-only sample data.
const lobs: ReceiptLobOption[] = [
  { id: 'lob-vehicle', lobName: 'Vehicle', lobDescription: 'Vehicle finance' },
  { id: 'lob-retail', lobName: 'Retail Lending', lobDescription: 'Retail loans' },
];
const branches: ReceiptBranchOption[] = [
  { id: 'br-001', code: 'BR001', label: 'Mumbai Fort Branch' },
  { id: 'br-002', code: 'BR002', label: 'Pune Camp Branch' },
];
const officers: ReceiptOfficerOption[] = [
  { id: 'mo-001', userCode: 'MO001', name: 'Meera Shah' },
  { id: 'mo-002', userCode: 'MO002', name: 'Arjun Iyer' },
];
const customers: ReceiptCustomer[] = [
  { id: 'cu-1', customerCode: 'CUST-100234', customerName: 'Ravi Kumar', address: '12, Fort Road, Mumbai, Maharashtra, 400001',
    phone: '022-23456789', mobile: '9820012345', email: 'ravi.kumar@example.com', website: null },
  { id: 'cu-2', customerCode: 'CUST-100235', customerName: 'Asha Rao', address: '5, Camp Street, Pune, Maharashtra, 411001',
    phone: null, mobile: '9890012345', email: 'asha.rao@example.com', website: null },
];
const liveContracts: Record<string, ReceiptContractOption[]> = {
  'CUST-100234': [{ accountNumber: 'ACC-889021', accountName: 'Vehicle Loan - Ravi Kumar' }],
  'CUST-100235': [{ accountNumber: 'ACC-889022', accountName: 'Vehicle Loan - Asha Rao' }],
};
const draweeBanks: ReceiptDraweeBankOption[] = [
  { id: 'db-hdfc', bankCode: 'HDFC', bankName: 'HDFC Bank Ltd' },
  { id: 'db-sbi', bankCode: 'SBI', bankName: 'State Bank of India' },
];
const draweeBranches: Record<string, ReceiptDraweeBranchOption[]> = {
  'db-hdfc': [{ id: 'dbr-hdfc-mum', placeCode: 'MUM', placeName: 'Mumbai' }, { id: 'dbr-hdfc-pun', placeCode: 'PUN', placeName: 'Pune' }],
  'db-sbi': [{ id: 'dbr-sbi-mum', placeCode: 'MUM', placeName: 'Mumbai' }],
};
const faAccounts: ReceiptFaBankAccountOption[] = [
  { id: 'fa-1', bankName: 'HDFC Bank Ltd', bankBranch: 'Mumbai Fort', accountNumber: '50200012345678', micrCode: '400240002', glCode: '110100101', slCode: null },
  { id: 'fa-2', bankName: 'State Bank of India', bankBranch: 'Pune Camp', accountNumber: '30012345678', micrCode: '411002003', glCode: '110100102', slCode: 'SL01' },
];
const components: ReceiptComponentOption[] = [
  { code: 'INSTALLMENT_INFLOW', label: 'Installment Inflow' },
  { code: 'ODI', label: 'Overdue Interest (ODI)' },
];
const denominations: CashDenominationOption[] = [
  { kind: 'Note', faceValue: 500, label: 'Note 500' }, { kind: 'Note', faceValue: 200, label: 'Note 200' },
  { kind: 'Note', faceValue: 100, label: 'Note 100' }, { kind: 'Note', faceValue: 50, label: 'Note 50' },
  { kind: 'Note', faceValue: 20, label: 'Note 20' }, { kind: 'Note', faceValue: 10, label: 'Note 10' },
  { kind: 'Coin', faceValue: 10, label: 'Coin 10' }, { kind: 'Coin', faceValue: 5, label: 'Coin 5' },
  { kind: 'Coin', faceValue: 2, label: 'Coin 2' }, { kind: 'Coin', faceValue: 1, label: 'Coin 1' },
  { kind: 'Coin', faceValue: 0.5, label: 'Coin 0.50 (50 paise)' },
];

const store: Receipt[] = [];
const audit: ReceiptAuditEntry[] = [];
const sequences: Record<ReceiptType, number> = { Temporary: 0, Regular: 0, ThirdParty: 0 };
const prefixes: Record<ReceiptType, string> = { Temporary: 'TRC', Regular: 'RRC', ThirdParty: 'TPR' };
let idCounter = 0;
const nextId = (p: string) => `${p}-${++idCounter}`;

/** Mimics the shape of an axios error so pages read response.data.message the same way as in real mode. */
function apiError(status: number, code: string, message: string, rule?: string): Error {
  return Object.assign(new Error(message), { response: { status, data: { code, message, rule } } });
}

export function resetReceiptMock(): void {
  store.length = 0;
  audit.length = 0;
  sequences.Temporary = sequences.Regular = sequences.ThirdParty = 0;
}

export const receiptMockService: IReceiptService = {
  async create(req: CreateReceiptRequest): Promise<CreateReceiptResult> {
    const existing = store.find(r => (r as Receipt & { clientRequestId?: string }).clientRequestId === req.clientRequestId);
    if (existing) return { receipt: existing, alreadyExisted: true };

    const customer = customers.find(c => c.customerCode === req.customerCode.trim());
    if (!customer) throw apiError(404, 'ERR-RCP-404', `Customer '${req.customerCode}' was not found or is inactive.`);

    const computed = computeDocAmount(req.lines.map(l => l.amount), (req.addLess ?? []).map(a => ({ kind: a.kind, amount: a.amount })));
    if (toPaise(computed) <= 0) throw apiError(422, 'ERR-RCP-422', 'The computed Document Amount must be greater than zero.', 'DOC_AMOUNT_NOT_POSITIVE');
    if (toPaise(computed) !== toPaise(req.docAmount)) {
      throw apiError(422, 'ERR-RCP-422', 'Doc Amount must equal the sum of the lines adjusted by the Add/Less rows.', 'DOC_AMOUNT_MISMATCH');
    }
    if (req.mode === 'Cash') {
      const total = denominationTotal((req.denominations ?? []).map(d => ({ faceValue: d.faceValue, count: d.count })));
      if (toPaise(total) !== toPaise(req.docAmount)) throw apiError(422, 'ERR-RCP-422', DENOMINATION_MISMATCH_MESSAGE, 'CASH_DENOMINATION_MISMATCH');
    }
    const owned = liveContracts[customer.customerCode] ?? [];
    for (const no of [...req.lines.map(l => l.primeAccountNo), ...(req.addLess ?? []).map(a => a.accountNumber)]) {
      if (!owned.some(c => c.accountNumber === no)) {
        throw apiError(422, 'ERR-RCP-422', `Account ${no} is not live or does not belong to customer ${customer.customerCode}.`, 'ACCOUNT_NOT_LIVE');
      }
    }

    let docNo = req.docNo ?? '';
    let sequenceNumber: number | null = null;
    if (req.entryMode === 'Online') {
      sequenceNumber = ++sequences[req.receiptType];
      docNo = `${prefixes[req.receiptType]}-${new Date().getFullYear()}-${String(sequenceNumber).padStart(7, '0')}`;
    } else if (store.some(r => r.receiptType === req.receiptType && r.docNo.toLowerCase() === docNo.toLowerCase())) {
      throw apiError(409, 'ERR-RCP-409', 'Document number already exists');
    }

    const lob = lobs.find(l => l.id === req.lineOfBusinessId);
    const branch = branches.find(b => b.id === req.branchId);
    const officer = officers.find(o => o.id === req.marketingOfficerUserId);
    if (!lob || !branch || !officer) throw apiError(400, 'ERR-RCP-400', 'Line of Business, Location, or Marketing Officer is invalid.');

    const bank = draweeBanks.find(b => b.id === req.cheque?.draweeBankId);
    const place = (draweeBranches[req.cheque?.draweeBankId ?? ''] ?? []).find(b => b.id === req.cheque?.draweeBranchId);
    const fa = faAccounts.find(a => a.id === req.bankTransfer?.faBankAccountId);

    const now = new Date().toISOString();
    const receipt: Receipt = {
      id: nextId('rcp'), docNo, sequenceNumber, entryMode: req.entryMode, receiptType: req.receiptType,
      status: 'PendingAuthorization', statusText: 'Pending Authorization', docDate: req.docDate, valueDate: req.valueDate,
      lineOfBusinessId: lob.id, lineOfBusinessName: lob.lobName, branchId: branch.id, branchCode: branch.code, branchName: branch.label,
      marketingOfficerUserId: officer.id, marketingOfficerName: officer.name, mode: req.mode, receiptFrom: req.receiptFrom,
      glBased: req.glBased, trRefNo: req.trRefNo, customerCode: customer.customerCode, customerName: customer.customerName,
      currencyCode: 'INR', docAmount: computed, amountInWords: amountInWords(computed),
      lines: req.lines.map((l, i) => ({
        id: nextId('ln'), lineNo: i + 1, primeAccountNo: l.primeAccountNo, subAccountNo: l.subAccountNo ?? null,
        accountDescription: owned.find(c => c.accountNumber === l.primeAccountNo)?.accountName ?? '',
        componentCode: l.componentCode, creditGlCode: req.glBased ? (l.glAccountCode ?? '') : '110100100',
        slAccountCode: req.glBased ? (l.slAccountCode ?? null) : null, amount: l.amount,
      })),
      addLess: (req.addLess ?? []).map((a, i) => ({
        id: nextId('al'), lineNo: i + 1, accountNumber: a.accountNumber, vehicleOrMachineNo: a.vehicleOrMachineNo ?? null,
        kind: a.kind, accountDescription: owned.find(c => c.accountNumber === a.accountNumber)?.accountName ?? '',
        glAccountCode: a.glAccountCode, slAccountCode: a.slAccountCode ?? null, amount: a.amount, remarks: a.remarks ?? null,
      })),
      denominations: (req.denominations ?? []).filter(d => d.count > 0)
        .map(d => ({ kind: d.kind, faceValue: d.faceValue, count: d.count, amount: d.faceValue * d.count })),
      cheque: req.cheque && bank && place ? {
        instrumentNo: req.cheque.instrumentNo, instrumentDate: req.cheque.instrumentDate ?? req.docDate,
        draweeBankId: bank.id, draweeBankCode: bank.bankCode, draweeBankName: bank.bankName,
        draweeBranchId: place.id, draweePlaceName: place.placeName,
        paymentGatewayRefNo: req.cheque.paymentGatewayRefNo ?? null, ackNo: req.cheque.ackNo ?? null,
      } : null,
      bankTransfer: fa ? {
        faBankAccountId: fa.id, bankName: fa.bankName, accountNo: fa.accountNumber, micrCode: fa.micrCode,
        bankBranch: fa.bankBranch, glCode: fa.glCode, slCode: fa.slCode,
      } : null,
      customer, authorizedBy: null, authorizedAtUtc: null, createdBy: 'mock-user', createdAtUtc: now,
    };
    (receipt as Receipt & { clientRequestId?: string }).clientRequestId = req.clientRequestId;
    store.unshift(receipt);
    audit.unshift({
      id: nextId('au'), receiptId: receipt.id, action: 'Created', docNo, sequenceNumber, actor: 'mock-user',
      correlationId: 'mock', changeSummary: `Receipt ${docNo} created.`, occurredAtUtc: now,
    });
    return { receipt, alreadyExisted: false };
  },

  async list(query: ReceiptListQuery): Promise<ReceiptPage> {
    const term = query.search?.trim().toLowerCase();
    const filtered = store.filter(r =>
      (!term || r.docNo.toLowerCase().includes(term) || r.customerCode.toLowerCase().includes(term)) &&
      (!query.receiptType || r.receiptType === query.receiptType) &&
      (!query.mode || r.mode === query.mode) &&
      (!query.status || r.status === query.status));
    const page = query.page ?? 1;
    const pageSize = query.pageSize ?? 20;
    const data = filtered.slice((page - 1) * pageSize, page * pageSize).map(r => ({
      id: r.id, docNo: r.docNo, receiptType: r.receiptType, mode: r.mode, status: r.status, statusText: r.statusText,
      docDate: r.docDate, valueDate: r.valueDate, customerCode: r.customerCode, customerName: r.customerName,
      branchName: r.branchName, docAmount: r.docAmount, createdAtUtc: r.createdAtUtc,
    }));
    return { data, total: filtered.length, page, pageSize, totalPages: Math.ceil(filtered.length / pageSize) };
  },

  async getById(id: string): Promise<Receipt> {
    const r = store.find(x => x.id === id);
    if (!r) throw apiError(404, 'ERR-RCP-404', `Receipt '${id}' was not found.`);
    return r;
  },
  async getAuditTrail(id: string): Promise<ReceiptAuditEntry[]> { return audit.filter(a => a.receiptId === id); },

  async getLinesOfBusiness() { return lobs.map(x => ({ ...x })); },
  async getBranches() { return branches.map(x => ({ ...x })); },
  async searchCustomers(search: string, page = 1, pageSize = 20): Promise<PaginatedResponse<ReceiptCustomerOption>> {
    const term = search.trim().toLowerCase();
    const hits = customers.filter(c => !term || c.customerCode.toLowerCase().includes(term) || c.customerName.toLowerCase().includes(term));
    return {
      data: hits.slice((page - 1) * pageSize, page * pageSize).map(c => ({ id: c.id, customerCode: c.customerCode, customerName: c.customerName })),
      total: hits.length, page, pageSize, totalPages: Math.ceil(hits.length / pageSize),
    };
  },
  async getCustomer(customerCode: string): Promise<ReceiptCustomer> {
    const c = customers.find(x => x.customerCode === customerCode.trim());
    if (!c) throw apiError(404, 'ERR-RCP-404', `Customer '${customerCode}' was not found or is inactive.`);
    return { ...c };
  },
  async getContracts(customerCode: string) { return (liveContracts[customerCode.trim()] ?? []).map(x => ({ ...x })); },
  async getMarketingOfficers() { return officers.map(x => ({ ...x })); },
  async getDraweeBanks() { return draweeBanks.map(x => ({ ...x })); },
  async getDraweeBranches(bankId: string) { return (draweeBranches[bankId] ?? []).map(x => ({ ...x })); },
  async getBankAccounts() { return faAccounts.map(x => ({ ...x })); },
  async getComponents() { return components.map(x => ({ ...x })); },
  async getDenominations() { return denominations.map(x => ({ ...x })); },
};
```

  - [ ] 13.5 CREATE `src/services/receipt.service.ts`

```ts
import type { IReceiptService } from './interfaces/receipt.interface';
import { receiptMockService } from './mock/receipt.mock';
import { receiptRealService } from './real/receipt.real';

/**
 * Toggle between mock and real API using VITE_USE_MOCK_API (true = mock data).
 */
const useMock = import.meta.env.VITE_USE_MOCK_API === 'true';
export const receiptService: IReceiptService = useMock ? receiptMockService : receiptRealService;
```

  - [ ] 13.6 MODIFY `src/services/index.ts`. Append:

```ts
export { receiptService } from './receipt.service';
export type { IReceiptService, ReceiptListQuery } from './interfaces/receipt.interface';
```

  - [ ] 13.7 MODIFY `src/services/interfaces/index.ts`. Append:

```ts
export type { IReceiptService, ReceiptListQuery } from './receipt.interface';
```

    If `src/services/index.ts` already re-exports everything from `./interfaces`, keep only one of the two type exports to avoid a duplicate-export error.

  - [ ] 13.8 MODIFY `src/models/index.ts`. Append:

```ts
export * from './receipt.model';
```

- [ ] 14. Frontend helpers
  - [ ] 14.1 CREATE `src/utils/amountInWords.ts`

```ts
const ONES = [
  'Zero', 'One', 'Two', 'Three', 'Four', 'Five', 'Six', 'Seven', 'Eight', 'Nine', 'Ten', 'Eleven', 'Twelve',
  'Thirteen', 'Fourteen', 'Fifteen', 'Sixteen', 'Seventeen', 'Eighteen', 'Nineteen',
];
const TENS = ['', '', 'Twenty', 'Thirty', 'Forty', 'Fifty', 'Sixty', 'Seventy', 'Eighty', 'Ninety'];

export const MAX_RECEIPT_AMOUNT = 999999999.99; // 99,99,99,999.99

const below100 = (n: number): string => (n < 20 ? ONES[n] : TENS[Math.floor(n / 10)] + (n % 10 === 0 ? '' : `-${ONES[n % 10]}`));

function words(value: number): string {
  const parts: string[] = [];
  let n = value;
  const crore = Math.floor(n / 10_000_000); n %= 10_000_000;
  const lakh = Math.floor(n / 100_000); n %= 100_000;
  const thousand = Math.floor(n / 1_000); n %= 1_000;
  const hundred = Math.floor(n / 100);
  const rest = n % 100;
  if (crore > 0) parts.push(`${below100(crore)} Crore`);
  if (lakh > 0) parts.push(`${below100(lakh)} Lakh`);
  if (thousand > 0) parts.push(`${below100(thousand)} Thousand`);
  if (hundred > 0) parts.push(`${ONES[hundred]} Hundred`);
  if (rest > 0) parts.push(below100(rest));
  return parts.join(' ');
}

/**
 * INR amount to words in the Indian numbering system. Twin of the backend AmountInWords helper; both are tested
 * against the same vectors. Returns an empty string for values outside 0.00 to 99,99,99,999.99.
 */
export function amountInWords(amount: number): string {
  if (!Number.isFinite(amount) || amount < 0 || amount > MAX_RECEIPT_AMOUNT) return '';
  const totalPaise = Math.round(amount * 100);
  const rupees = Math.floor(totalPaise / 100);
  const paise = totalPaise % 100;
  let text = `Rupees ${rupees === 0 ? 'Zero' : words(rupees)}`;
  if (paise > 0) text += ` and ${words(paise)} Paise`;
  return `${text} Only`;
}
```

  - [ ] 14.2 CREATE `src/utils/receiptCalculator.ts`

```ts
import type { AddLessKind } from '../models/receipt.model';

/** Amounts are summed as integer paise so floating point never leaks into the totals. */
export const toPaise = (value: number | string): number => {
  const n = typeof value === 'string' ? Number(value) : value;
  return Number.isFinite(n) ? Math.round(n * 100) : 0;
};

export const fromPaise = (paise: number): number => paise / 100;

/** Doc Amount = sum(lines) + sum(Add) - sum(Less). */
export function docAmount(lineAmounts: number[], addLess: { kind: AddLessKind; amount: number }[]): number {
  const lines = lineAmounts.reduce((sum, a) => sum + toPaise(a), 0);
  const adjustments = addLess.reduce((sum, r) => sum + (r.kind === 'Add' ? toPaise(r.amount) : -toPaise(r.amount)), 0);
  return fromPaise(lines + adjustments);
}

/** Sum of face value x count. */
export function denominationTotal(rows: { faceValue: number; count: number }[]): number {
  return fromPaise(rows.reduce((sum, r) => sum + toPaise(r.faceValue) * Math.max(0, Math.trunc(r.count || 0)), 0));
}

export const formatInr = (value: number): string =>
  new Intl.NumberFormat('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value);
```

  - [ ] 14.3 CREATE `src/utils/receiptForm.ts`

```ts
import type {
  AddLessKind, CashDenominationOption, CreateReceiptRequest, DenominationKind, ReceiptEntryMode, ReceiptFrom,
  ReceiptMode, ReceiptType,
} from '../models/receipt.model';
import { denominationTotal, docAmount as computeDocAmount, toPaise } from './receiptCalculator';

export const DENOMINATION_MISMATCH_MESSAGE = 'Document Amount must match the sum of Cash Denominations';

export interface ReceiptLineForm {
  key: string; primeAccountNo: string; subAccountNo: string; componentCode: string;
  glAccountCode: string; slAccountCode: string; amount: string;
}

export interface AddLessForm {
  key: string; accountNumber: string; vehicleOrMachineNo: string; kind: AddLessKind;
  glAccountCode: string; slAccountCode: string; amount: string; remarks: string;
}

export interface ChequeForm {
  instrumentNo: string; instrumentDate: string; draweeBankId: string; draweeBranchId: string;
  paymentGatewayRefNo: string; ackNo: string;
}

export interface ReceiptForm {
  clientRequestId: string;
  entryMode: ReceiptEntryMode;
  docNo: string;
  receiptType: ReceiptType;
  docDate: string;
  valueDate: string;
  lineOfBusinessId: string;
  branchId: string;
  marketingOfficerUserId: string;
  mode: ReceiptMode;
  receiptFrom: ReceiptFrom;
  glBased: boolean;
  customerCode: string;
  trRefNo: string;
  docAmount: string;
  lines: ReceiptLineForm[];
  addLess: AddLessForm[];
  /** Counts keyed by `${kind}:${faceValue}`. */
  denominationCounts: Record<string, number>;
  cheque: ChequeForm;
  faBankAccountId: string;
}

export function uuidv4(): string {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, c => {
    const r = Math.floor(Math.random() * 16);
    return (c === 'x' ? r : (r % 4) + 8).toString(16);
  });
}

export const newKey = (): string => uuidv4();

/** Current calendar date in Asia/Kolkata as yyyy-MM-dd. */
export const todayIso = (): string =>
  new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Kolkata', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date());

export const denominationKey = (kind: DenominationKind, faceValue: number): string => `${kind}:${faceValue}`;

export const trRefVisible = (type: ReceiptType): boolean => type !== 'Temporary';

export const emptyLine = (): ReceiptLineForm => ({
  key: newKey(), primeAccountNo: '', subAccountNo: '', componentCode: '', glAccountCode: '', slAccountCode: '', amount: '',
});

export const emptyAddLess = (): AddLessForm => ({
  key: newKey(), accountNumber: '', vehicleOrMachineNo: '', kind: 'Add', glAccountCode: '', slAccountCode: '', amount: '', remarks: '',
});

export function createEmptyForm(): ReceiptForm {
  const today = todayIso();
  return {
    clientRequestId: uuidv4(), entryMode: 'Online', docNo: '', receiptType: 'Temporary', docDate: today, valueDate: today,
    lineOfBusinessId: '', branchId: '', marketingOfficerUserId: '', mode: 'Cheque', receiptFrom: 'Customer', glBased: false,
    customerCode: '', trRefNo: '', docAmount: '', lines: [emptyLine()], addLess: [], denominationCounts: {},
    cheque: { instrumentNo: '', instrumentDate: today, draweeBankId: '', draweeBranchId: '', paymentGatewayRefNo: '', ackNo: '' },
    faBankAccountId: '',
  };
}

const num = (s: string): number => (s.trim() === '' ? 0 : Number(s));

/** Total of the denomination grid in rupees. */
export function formDenominationTotal(form: ReceiptForm): number {
  return denominationTotal(Object.entries(form.denominationCounts).map(([key, count]) => ({
    faceValue: Number(key.split(':')[1]), count,
  })));
}

/** Doc Amount computed from the lines and Add/Less rows. */
export function formComputedDocAmount(form: ReceiptForm): number {
  return computeDocAmount(form.lines.map(l => num(l.amount)), form.addLess.map(a => ({ kind: a.kind, amount: num(a.amount) })));
}

/** Live checks that block Save (R4.10, R9.9). */
export function liveIssues(form: ReceiptForm): { denominationMismatch: boolean; docAmountMismatch: boolean } {
  const entered = toPaise(num(form.docAmount));
  return {
    denominationMismatch: form.mode === 'Cash' && toPaise(formDenominationTotal(form)) !== entered,
    docAmountMismatch: toPaise(formComputedDocAmount(form)) !== entered,
  };
}

/** Client-side validation run when Save is selected. The server repeats every rule. */
export function validateForm(form: ReceiptForm): string[] {
  const errors: string[] = [];
  const need = (ok: boolean, msg: string) => { if (!ok) errors.push(msg); };

  need(form.entryMode !== 'Manual' || form.docNo.trim().length > 0, 'Doc No is required for Manual entry.');
  need(!!form.docDate, 'Doc Date is required.');
  need(!!form.valueDate, 'Value Date is required.');
  need(!form.docDate || !form.valueDate || form.valueDate >= form.docDate, 'Value Date cannot be earlier than Doc Date.');
  need(!!form.lineOfBusinessId, 'Line of Business is required.');
  need(!!form.branchId, 'Location is required.');
  need(!!form.marketingOfficerUserId, 'Marketing Officer is required.');
  need(form.customerCode.trim().length > 0, 'Customer is required.');
  need(form.receiptType !== 'ThirdParty' || form.receiptFrom !== 'Customer', 'A Third-Party receipt requires a Receipt From other than Customer.');
  need(num(form.docAmount) > 0, 'Doc Amount must be greater than zero.');
  need(form.trRefNo.length <= 30, 'TR Ref No must be at most 30 characters.');

  need(form.lines.length > 0, 'At least one Account Details line is required.');
  form.lines.forEach((l, i) => {
    const n = i + 1;
    need(l.primeAccountNo.trim().length > 0, `Line ${n}: Account Number is required.`);
    need(l.componentCode.trim().length > 0, `Line ${n}: Component is required.`);
    need(num(l.amount) > 0, `Line ${n}: Amount must be greater than zero.`);
    need(!form.glBased || /^\d{9}$/.test(l.glAccountCode.trim()), `Line ${n}: GL Account must be nine digits when GL Based is Yes.`);
  });
  form.addLess.forEach((a, i) => {
    const n = i + 1;
    need(a.accountNumber.trim().length > 0, `Add/Less ${n}: Account Number is required.`);
    need(/^\d{9}$/.test(a.glAccountCode.trim()), `Add/Less ${n}: GL Account must be nine digits.`);
    need(num(a.amount) > 0, `Add/Less ${n}: Amount must be greater than zero.`);
  });

  const issues = liveIssues(form);
  need(!issues.docAmountMismatch, 'Doc Amount must equal the sum of the lines adjusted by the Add/Less rows.');
  need(!issues.denominationMismatch, DENOMINATION_MISMATCH_MESSAGE);

  if (form.mode === 'Cash') {
    need(Object.values(form.denominationCounts).some(c => c > 0), 'Enter the cash denominations.');
  }
  if (form.mode === 'Cheque') {
    need(/^\d{6}$/.test(form.cheque.instrumentNo), 'Instrument No must be six digits.');
    need(!!form.cheque.draweeBankId, 'Drawee Bank is required.');
    need(!!form.cheque.draweeBranchId, 'Drawee Branch is required.');
  }
  if (form.mode === 'BankTransfer') {
    need(!!form.faBankAccountId, 'Deposit Bank is required.');
  }
  return errors;
}

const blank = (s: string): string | null => (s.trim() === '' ? null : s.trim());

export function buildRequest(form: ReceiptForm, catalog: CashDenominationOption[]): CreateReceiptRequest {
  const cash = form.mode === 'Cash';
  return {
    clientRequestId: form.clientRequestId,
    entryMode: form.entryMode,
    docNo: form.entryMode === 'Manual' ? form.docNo.trim() : null,
    receiptType: form.receiptType,
    docDate: form.docDate,
    valueDate: form.valueDate,
    lineOfBusinessId: form.lineOfBusinessId,
    branchId: form.branchId,
    marketingOfficerUserId: form.marketingOfficerUserId,
    mode: form.mode,
    receiptFrom: form.receiptFrom,
    glBased: form.glBased,
    customerCode: form.customerCode.trim(),
    trRefNo: trRefVisible(form.receiptType) ? blank(form.trRefNo) : null,
    docAmount: num(form.docAmount),
    lines: form.lines.map(l => ({
      primeAccountNo: l.primeAccountNo.trim(), subAccountNo: blank(l.subAccountNo), componentCode: l.componentCode,
      glAccountCode: form.glBased ? blank(l.glAccountCode) : null, slAccountCode: form.glBased ? blank(l.slAccountCode) : null,
      amount: num(l.amount),
    })),
    addLess: form.addLess.length === 0 ? null : form.addLess.map(a => ({
      accountNumber: a.accountNumber.trim(), vehicleOrMachineNo: blank(a.vehicleOrMachineNo), kind: a.kind,
      glAccountCode: a.glAccountCode.trim(), slAccountCode: blank(a.slAccountCode), amount: num(a.amount), remarks: blank(a.remarks),
    })),
    denominations: cash
      ? catalog
          .map(c => ({ kind: c.kind, faceValue: c.faceValue, count: form.denominationCounts[denominationKey(c.kind, c.faceValue)] ?? 0 }))
          .filter(d => d.count > 0)
      : null,
    cheque: form.mode === 'Cheque' ? {
      instrumentNo: form.cheque.instrumentNo.trim(), instrumentDate: blank(form.cheque.instrumentDate),
      draweeBankId: form.cheque.draweeBankId, draweeBranchId: form.cheque.draweeBranchId,
      paymentGatewayRefNo: blank(form.cheque.paymentGatewayRefNo), ackNo: blank(form.cheque.ackNo),
    } : null,
    bankTransfer: form.mode === 'BankTransfer' ? { faBankAccountId: form.faBankAccountId } : null,
  };
}
```

- [ ] 15. Frontend components, pages, route, navigation
  - [ ] 15.1 CREATE `src/components/receipt/AccountDetailsGrid.tsx`

```tsx
import { Box, Button, IconButton, Table, TableBody, TableCell, TableHead, TableRow, TextField, Typography } from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import DeleteIcon from '@mui/icons-material/Delete';
import type { ReceiptComponentOption, ReceiptContractOption } from '../../models/receipt.model';
import { emptyLine } from '../../utils/receiptForm';
import type { ReceiptLineForm } from '../../utils/receiptForm';
import { formatInr, toPaise } from '../../utils/receiptCalculator';

interface Props {
  lines: ReceiptLineForm[];
  contracts: ReceiptContractOption[];
  components: ReceiptComponentOption[];
  glBased: boolean;
  onChange: (lines: ReceiptLineForm[]) => void;
}

/** "Account Details" grid: Prime Account No, Sub Account No, Component, GL Account, SL Account, Amount. */
export default function AccountDetailsGrid({ lines, contracts, components, glBased, onChange }: Props) {
  const patch = (key: string, change: Partial<ReceiptLineForm>) =>
    onChange(lines.map(l => (l.key === key ? { ...l, ...change } : l)));
  const total = lines.reduce((sum, l) => sum + toPaise(l.amount), 0) / 100;

  return (
    <Box>
      <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 1 }}>Account Details</Typography>
      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell>Prime Account No</TableCell>
            <TableCell>Sub Account No</TableCell>
            <TableCell>Account Description</TableCell>
            <TableCell>Component</TableCell>
            <TableCell>GL Account</TableCell>
            <TableCell>SL Account</TableCell>
            <TableCell align="right">Amount</TableCell>
            <TableCell />
          </TableRow>
        </TableHead>
        <TableBody>
          {lines.map((l, i) => (
            <TableRow key={l.key}>
              <TableCell>
                <TextField select size="small" SelectProps={{ native: true }} value={l.primeAccountNo}
                  onChange={e => patch(l.key, { primeAccountNo: e.target.value })}
                  inputProps={{ 'aria-label': `Prime Account No ${i + 1}` }}>
                  <option value="">Select</option>
                  {contracts.map(c => <option key={c.accountNumber} value={c.accountNumber}>{c.accountNumber}</option>)}
                </TextField>
              </TableCell>
              <TableCell>
                <TextField size="small" value={l.subAccountNo} inputProps={{ maxLength: 30, 'aria-label': `Sub Account No ${i + 1}` }}
                  onChange={e => patch(l.key, { subAccountNo: e.target.value })} />
              </TableCell>
              <TableCell>{contracts.find(c => c.accountNumber === l.primeAccountNo)?.accountName ?? ''}</TableCell>
              <TableCell>
                <TextField select size="small" SelectProps={{ native: true }} value={l.componentCode}
                  onChange={e => patch(l.key, { componentCode: e.target.value })}
                  inputProps={{ 'aria-label': `Component ${i + 1}` }}>
                  <option value="">Select</option>
                  {components.map(c => <option key={c.code} value={c.code}>{c.label}</option>)}
                </TextField>
              </TableCell>
              <TableCell>
                <TextField size="small" value={l.glAccountCode} disabled={!glBased}
                  inputProps={{ maxLength: 9, inputMode: 'numeric', 'aria-label': `GL Account ${i + 1}` }}
                  onChange={e => patch(l.key, { glAccountCode: e.target.value })} />
              </TableCell>
              <TableCell>
                <TextField size="small" value={l.slAccountCode} disabled={!glBased}
                  inputProps={{ maxLength: 20, 'aria-label': `SL Account ${i + 1}` }}
                  onChange={e => patch(l.key, { slAccountCode: e.target.value })} />
              </TableCell>
              <TableCell align="right">
                <TextField size="small" value={l.amount} inputProps={{ inputMode: 'decimal', 'aria-label': `Line Amount ${i + 1}` }}
                  onChange={e => patch(l.key, { amount: e.target.value })} />
              </TableCell>
              <TableCell>
                <IconButton aria-label={`Remove line ${i + 1}`} disabled={lines.length === 1}
                  onClick={() => onChange(lines.filter(x => x.key !== l.key))}>
                  <DeleteIcon fontSize="small" />
                </IconButton>
              </TableCell>
            </TableRow>
          ))}
          <TableRow>
            <TableCell colSpan={6} align="right"><strong>Lines total (INR)</strong></TableCell>
            <TableCell align="right"><strong>{formatInr(total)}</strong></TableCell>
            <TableCell />
          </TableRow>
        </TableBody>
      </Table>
      <Button size="small" startIcon={<AddIcon />} sx={{ mt: 1 }} disabled={lines.length >= 50}
        onClick={() => onChange([...lines, emptyLine()])}>
        Add Line
      </Button>
    </Box>
  );
}
```

  - [ ] 15.2 CREATE `src/components/receipt/AddLessGrid.tsx`

```tsx
import { useState } from 'react';
import { Box, Button, Collapse, IconButton, Table, TableBody, TableCell, TableHead, TableRow, TextField, Typography } from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import DeleteIcon from '@mui/icons-material/Delete';
import type { AddLessKind, ReceiptContractOption } from '../../models/receipt.model';
import { emptyAddLess } from '../../utils/receiptForm';
import type { AddLessForm } from '../../utils/receiptForm';

interface Props {
  rows: AddLessForm[];
  contracts: ReceiptContractOption[];
  onChange: (rows: AddLessForm[]) => void;
}

/** Collapsible "Add/Less Details" grid. An Add row increases and a Less row decreases the Doc Amount. */
export default function AddLessGrid({ rows, contracts, onChange }: Props) {
  const [open, setOpen] = useState(true);
  const patch = (key: string, change: Partial<AddLessForm>) =>
    onChange(rows.map(r => (r.key === key ? { ...r, ...change } : r)));

  return (
    <Box>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 1 }}>
        <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>Add/Less Details</Typography>
        <Button size="small" onClick={() => setOpen(o => !o)}>{open ? 'Hide Details' : 'Show Details'}</Button>
      </Box>
      <Collapse in={open}>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Account Number</TableCell>
              <TableCell>Vehicle No / Machine Serial No</TableCell>
              <TableCell>Add or Less</TableCell>
              <TableCell>Account Description</TableCell>
              <TableCell>GL Account</TableCell>
              <TableCell>SL Account</TableCell>
              <TableCell align="right">Amount</TableCell>
              <TableCell>Remarks</TableCell>
              <TableCell />
            </TableRow>
          </TableHead>
          <TableBody>
            {rows.length === 0 && (
              <TableRow><TableCell colSpan={9}>No Records Found</TableCell></TableRow>
            )}
            {rows.map((r, i) => (
              <TableRow key={r.key}>
                <TableCell>
                  <TextField select size="small" SelectProps={{ native: true }} value={r.accountNumber}
                    onChange={e => patch(r.key, { accountNumber: e.target.value })}
                    inputProps={{ 'aria-label': `Add/Less Account Number ${i + 1}` }}>
                    <option value="">Select</option>
                    {contracts.map(c => <option key={c.accountNumber} value={c.accountNumber}>{c.accountNumber}</option>)}
                  </TextField>
                </TableCell>
                <TableCell>
                  <TextField size="small" value={r.vehicleOrMachineNo} inputProps={{ maxLength: 50, 'aria-label': `Vehicle No ${i + 1}` }}
                    onChange={e => patch(r.key, { vehicleOrMachineNo: e.target.value })} />
                </TableCell>
                <TableCell>
                  <TextField select size="small" SelectProps={{ native: true }} value={r.kind}
                    onChange={e => patch(r.key, { kind: e.target.value as AddLessKind })}
                    inputProps={{ 'aria-label': `Add or Less ${i + 1}` }}>
                    <option value="Add">Add</option>
                    <option value="Less">Less</option>
                  </TextField>
                </TableCell>
                <TableCell>{contracts.find(c => c.accountNumber === r.accountNumber)?.accountName ?? ''}</TableCell>
                <TableCell>
                  <TextField size="small" value={r.glAccountCode}
                    inputProps={{ maxLength: 9, inputMode: 'numeric', 'aria-label': `Add/Less GL Account ${i + 1}` }}
                    onChange={e => patch(r.key, { glAccountCode: e.target.value })} />
                </TableCell>
                <TableCell>
                  <TextField size="small" value={r.slAccountCode} inputProps={{ maxLength: 20, 'aria-label': `Add/Less SL Account ${i + 1}` }}
                    onChange={e => patch(r.key, { slAccountCode: e.target.value })} />
                </TableCell>
                <TableCell align="right">
                  <TextField size="small" value={r.amount} inputProps={{ inputMode: 'decimal', 'aria-label': `Add/Less Amount ${i + 1}` }}
                    onChange={e => patch(r.key, { amount: e.target.value })} />
                </TableCell>
                <TableCell>
                  <TextField size="small" value={r.remarks} inputProps={{ maxLength: 200, 'aria-label': `Remarks ${i + 1}` }}
                    onChange={e => patch(r.key, { remarks: e.target.value })} />
                </TableCell>
                <TableCell>
                  <IconButton aria-label={`Remove adjustment ${i + 1}`} onClick={() => onChange(rows.filter(x => x.key !== r.key))}>
                    <DeleteIcon fontSize="small" />
                  </IconButton>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
        <Button size="small" startIcon={<AddIcon />} sx={{ mt: 1 }} disabled={rows.length >= 50}
          onClick={() => onChange([...rows, emptyAddLess()])}>
          Add
        </Button>
      </Collapse>
    </Box>
  );
}
```

  - [ ] 15.3 CREATE `src/components/receipt/CashDenominationDialog.tsx`

```tsx
import {
  Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, Table, TableBody, TableCell, TableHead, TableRow, TextField,
} from '@mui/material';
import type { CashDenominationOption } from '../../models/receipt.model';
import { denominationKey, DENOMINATION_MISMATCH_MESSAGE } from '../../utils/receiptForm';
import { denominationTotal, formatInr, toPaise } from '../../utils/receiptCalculator';

interface Props {
  open: boolean;
  catalog: CashDenominationOption[];
  counts: Record<string, number>;
  docAmount: number;
  onChange: (counts: Record<string, number>) => void;
  onClose: () => void;
}

/** Currency denomination grid. Row amount = face value x count; the total must equal the Doc Amount. */
export default function CashDenominationDialog({ open, catalog, counts, docAmount, onChange, onClose }: Props) {
  const rows = catalog.map(c => ({ ...c, key: denominationKey(c.kind, c.faceValue), count: counts[denominationKey(c.kind, c.faceValue)] ?? 0 }));
  const total = denominationTotal(rows.map(r => ({ faceValue: r.faceValue, count: r.count })));
  const mismatch = toPaise(total) !== toPaise(docAmount);

  const setCount = (key: string, raw: string) => {
    const n = raw === '' ? 0 : Math.max(0, Math.min(1_000_000, Math.trunc(Number(raw)) || 0));
    onChange({ ...counts, [key]: n });
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Cash Denominations</DialogTitle>
      <DialogContent>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Denomination</TableCell>
              <TableCell align="right">Count</TableCell>
              <TableCell align="right">Amount (INR)</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {rows.map(r => (
              <TableRow key={r.key}>
                <TableCell>{r.label}</TableCell>
                <TableCell align="right">
                  <TextField size="small" type="number" value={r.count === 0 ? '' : r.count}
                    inputProps={{ min: 0, max: 1_000_000, 'aria-label': `${r.label} count` }}
                    onChange={e => setCount(r.key, e.target.value)} sx={{ width: 120 }} />
                </TableCell>
                <TableCell align="right">{formatInr((toPaise(r.faceValue) * r.count) / 100)}</TableCell>
              </TableRow>
            ))}
            <TableRow>
              <TableCell colSpan={2}><strong>Total</strong></TableCell>
              <TableCell align="right"><strong>{formatInr(total)}</strong></TableCell>
            </TableRow>
            <TableRow>
              <TableCell colSpan={2}>Document Amount</TableCell>
              <TableCell align="right">{formatInr(docAmount)}</TableCell>
            </TableRow>
          </TableBody>
        </Table>
        {mismatch && <Alert severity="warning" sx={{ mt: 2 }}>{DENOMINATION_MISMATCH_MESSAGE}</Alert>}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Close</Button>
      </DialogActions>
    </Dialog>
  );
}
```

  - [ ] 15.4 CREATE `src/components/receipt/BankDetailsTab.tsx`

```tsx
import { Box, TextField } from '@mui/material';
import type {
  ReceiptDraweeBankOption, ReceiptDraweeBranchOption, ReceiptFaBankAccountOption, ReceiptMode,
} from '../../models/receipt.model';
import type { ChequeForm } from '../../utils/receiptForm';

interface Props {
  mode: ReceiptMode;
  cheque: ChequeForm;
  onChequeChange: (change: Partial<ChequeForm>) => void;
  draweeBanks: ReceiptDraweeBankOption[];
  draweeBranches: ReceiptDraweeBranchOption[];
  faAccounts: ReceiptFaBankAccountOption[];
  faBankAccountId: string;
  onFaBankAccountChange: (id: string) => void;
}

const grid = { display: 'grid', gridTemplateColumns: { xs: '1fr', md: 'repeat(3, 1fr)' }, gap: 2 } as const;

/** Bank Details tab. Cheque: instrument and drawee fields. Bank Transfer: F&A deposit bank with read-only details. */
export default function BankDetailsTab(p: Props) {
  if (p.mode === 'Cheque') {
    return (
      <Box sx={grid}>
        <TextField id="instrumentNo" label="Instrument No" required size="small" value={p.cheque.instrumentNo}
          inputProps={{ maxLength: 6, inputMode: 'numeric' }}
          error={p.cheque.instrumentNo !== '' && !/^\d{6}$/.test(p.cheque.instrumentNo)}
          helperText="Six digits"
          onChange={e => p.onChequeChange({ instrumentNo: e.target.value.replace(/\D/g, '') })} />
        <TextField id="instrumentDate" label="Instrument Date" type="date" size="small" value={p.cheque.instrumentDate}
          InputLabelProps={{ shrink: true }} onChange={e => p.onChequeChange({ instrumentDate: e.target.value })} />
        <TextField id="draweeBank" label="Drawee Bank" required select size="small" SelectProps={{ native: true }}
          InputLabelProps={{ shrink: true }} value={p.cheque.draweeBankId}
          onChange={e => p.onChequeChange({ draweeBankId: e.target.value, draweeBranchId: '' })}>
          <option value="">Select</option>
          {p.draweeBanks.map(b => <option key={b.id} value={b.id}>{b.bankName}</option>)}
        </TextField>
        <TextField id="draweeBranch" label="Drawee Branch" required select size="small" SelectProps={{ native: true }}
          InputLabelProps={{ shrink: true }} value={p.cheque.draweeBranchId} disabled={!p.cheque.draweeBankId}
          onChange={e => p.onChequeChange({ draweeBranchId: e.target.value })}>
          <option value="">Select</option>
          {p.draweeBranches.map(b => <option key={b.id} value={b.id}>{b.placeName}</option>)}
        </TextField>
        <TextField id="paymentGatewayRefNo" label="Payment Gateway Ref No" size="small" value={p.cheque.paymentGatewayRefNo}
          inputProps={{ maxLength: 50 }} onChange={e => p.onChequeChange({ paymentGatewayRefNo: e.target.value })} />
        <TextField id="ackNo" label="ACK No" size="small" value={p.cheque.ackNo}
          inputProps={{ maxLength: 50 }} onChange={e => p.onChequeChange({ ackNo: e.target.value })} />
      </Box>
    );
  }

  if (p.mode === 'BankTransfer') {
    const selected = p.faAccounts.find(a => a.id === p.faBankAccountId);
    return (
      <Box sx={grid}>
        <TextField id="depositBank" label="Deposit Bank" required select size="small" SelectProps={{ native: true }}
          InputLabelProps={{ shrink: true }} value={p.faBankAccountId} onChange={e => p.onFaBankAccountChange(e.target.value)}>
          <option value="">Select</option>
          {p.faAccounts.map(a => <option key={a.id} value={a.id}>{`${a.bankName} - ${a.accountNumber}`}</option>)}
        </TextField>
        <TextField label="Account No" size="small" value={selected?.accountNumber ?? ''} InputProps={{ readOnly: true }} InputLabelProps={{ shrink: true }} />
        <TextField label="MICR" size="small" value={selected?.micrCode ?? ''} InputProps={{ readOnly: true }} InputLabelProps={{ shrink: true }} />
        <TextField label="Bank Branch" size="small" value={selected?.bankBranch ?? ''} InputProps={{ readOnly: true }} InputLabelProps={{ shrink: true }} />
        <TextField label="GL Code" size="small" value={selected?.glCode ?? ''} InputProps={{ readOnly: true }} InputLabelProps={{ shrink: true }} />
        <TextField label="SL Code" size="small" value={selected?.slCode ?? ''} InputProps={{ readOnly: true }} InputLabelProps={{ shrink: true }} />
      </Box>
    );
  }

  return null;
}
```

  - [ ] 15.5 CREATE `src/components/receipt/CustomerSection.tsx`

```tsx
import { useEffect, useState } from 'react';
import {
  Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, IconButton, InputAdornment, List, ListItemButton,
  ListItemText, Table, TableBody, TableCell, TableHead, TableRow, TextField, Typography,
} from '@mui/material';
import SearchIcon from '@mui/icons-material/Search';
import { receiptService } from '../../services';
import type { ReceiptContractOption, ReceiptCustomer, ReceiptCustomerOption } from '../../models/receipt.model';

interface Props {
  customerCode: string;
  customer: ReceiptCustomer | null;
  contracts: ReceiptContractOption[];
  onCodeChange: (code: string) => void;
  /** Called when the Customer Code field loses focus or Enter is pressed. */
  onLoad: (code: string) => void;
}

const grid = { display: 'grid', gridTemplateColumns: { xs: '1fr', md: 'repeat(3, 1fr)' }, gap: 2 } as const;

/** Customer Information: lookup by code or name, read-only contact fields, and the Show list of live contracts. */
export default function CustomerSection({ customerCode, customer, contracts, onCodeChange, onLoad }: Props) {
  const [searchOpen, setSearchOpen] = useState(false);
  const [showOpen, setShowOpen] = useState(false);
  const [term, setTerm] = useState('');
  const [results, setResults] = useState<ReceiptCustomerOption[]>([]);

  useEffect(() => {
    if (!searchOpen) return;
    let cancelled = false;
    receiptService.searchCustomers(term, 1, 20)
      .then(res => { if (!cancelled) setResults(res.data); })
      .catch(() => { if (!cancelled) setResults([]); });
    return () => { cancelled = true; };
  }, [searchOpen, term]);

  const readOnly = { readOnly: true } as const;
  const shrink = { shrink: true } as const;

  return (
    <Box>
      <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 1 }}>Customer Information</Typography>
      <Box sx={grid}>
        <TextField id="customerCode" label="Customer Code" required size="small" value={customerCode}
          InputLabelProps={shrink} inputProps={{ maxLength: 20 }}
          onChange={e => onCodeChange(e.target.value)}
          onBlur={() => onLoad(customerCode)}
          onKeyDown={e => { if (e.key === 'Enter') onLoad(customerCode); }}
          InputProps={{
            endAdornment: (
              <InputAdornment position="end">
                <IconButton aria-label="Search customer" size="small" onClick={() => setSearchOpen(true)}><SearchIcon fontSize="small" /></IconButton>
              </InputAdornment>
            ),
          }} />
        <TextField label="Customer Name" size="small" value={customer?.customerName ?? ''} InputProps={readOnly} InputLabelProps={shrink} />
        <TextField label="Phone" size="small" value={customer?.phone ?? ''} InputProps={readOnly} InputLabelProps={shrink} />
        <TextField label="Address" size="small" multiline minRows={2} value={customer?.address ?? ''} InputProps={readOnly} InputLabelProps={shrink} />
        <TextField label="Mobile" size="small" value={customer?.mobile ?? ''} InputProps={readOnly} InputLabelProps={shrink} />
        <TextField label="Email Id" size="small" value={customer?.email ?? ''} InputProps={readOnly} InputLabelProps={shrink} />
        <TextField label="Website" size="small" value={customer?.website ?? ''} InputProps={readOnly} InputLabelProps={shrink} />
        <Box sx={{ display: 'flex', alignItems: 'center' }}>
          <Button variant="outlined" size="small" disabled={!customer} onClick={() => setShowOpen(true)}>Show</Button>
        </Box>
      </Box>

      <Dialog open={searchOpen} onClose={() => setSearchOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Find Customer</DialogTitle>
        <DialogContent>
          <TextField autoFocus fullWidth size="small" label="Customer Code or Name" value={term}
            onChange={e => setTerm(e.target.value)} sx={{ mt: 1 }} />
          <List dense>
            {results.map(r => (
              <ListItemButton key={r.id} onClick={() => {
                onCodeChange(r.customerCode);
                onLoad(r.customerCode);
                setSearchOpen(false);
              }}>
                <ListItemText primary={r.customerName} secondary={r.customerCode} />
              </ListItemButton>
            ))}
            {results.length === 0 && <ListItemText secondary="No Records Found" sx={{ px: 2 }} />}
          </List>
        </DialogContent>
        <DialogActions><Button onClick={() => setSearchOpen(false)}>Close</Button></DialogActions>
      </Dialog>

      <Dialog open={showOpen} onClose={() => setShowOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Live Contracts</DialogTitle>
        <DialogContent>
          <Table size="small">
            <TableHead><TableRow><TableCell>Account Number</TableCell><TableCell>Account Description</TableCell></TableRow></TableHead>
            <TableBody>
              {contracts.map(c => (
                <TableRow key={c.accountNumber}><TableCell>{c.accountNumber}</TableCell><TableCell>{c.accountName}</TableCell></TableRow>
              ))}
              {contracts.length === 0 && <TableRow><TableCell colSpan={2}>No Records Found</TableCell></TableRow>}
            </TableBody>
          </Table>
        </DialogContent>
        <DialogActions><Button onClick={() => setShowOpen(false)}>Close</Button></DialogActions>
      </Dialog>
    </Box>
  );
}
```

  - [ ] 15.6 CREATE `src/components/receipt/ReceiptDetailDialog.tsx` (read-only view of a saved receipt, General and Bank Details tabs; Authorize and Revoke are a follow-up)

```tsx
import { useState } from 'react';
import {
  Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, Tab, Table, TableBody, TableCell, TableHead, TableRow, Tabs,
  TextField, Typography,
} from '@mui/material';
import type { Receipt, ReceiptAuditEntry } from '../../models/receipt.model';
import { RECEIPT_FROM_LABELS, RECEIPT_MODE_LABELS, RECEIPT_TYPE_LABELS } from '../../models/receipt.model';
import { formatInr } from '../../utils/receiptCalculator';

interface Props { receipt: Receipt | null; audit: ReceiptAuditEntry[]; onClose: () => void }

const grid = { display: 'grid', gridTemplateColumns: { xs: '1fr', md: 'repeat(3, 1fr)' }, gap: 2, mb: 2 } as const;

function Field({ label, value }: { label: string; value: string | number | null | undefined }) {
  return <TextField label={label} size="small" value={value ?? ''} InputProps={{ readOnly: true }} InputLabelProps={{ shrink: true }} />;
}

export default function ReceiptDetailDialog({ receipt, audit, onClose }: Props) {
  const [tab, setTab] = useState(0);
  if (!receipt) return null;
  const hasBank = receipt.mode !== 'Cash';

  return (
    <Dialog open onClose={onClose} maxWidth="lg" fullWidth>
      <DialogTitle>Receipt {receipt.docNo}</DialogTitle>
      <DialogContent>
        <Tabs value={hasBank ? tab : 0} onChange={(_, v: number) => setTab(v)} sx={{ mb: 2 }}>
          <Tab label="General" />
          <Tab label="Bank Details" disabled={!hasBank} />
        </Tabs>

        {(!hasBank || tab === 0) && (
          <>
            <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 1 }}>Receipt Information</Typography>
            <Box sx={grid}>
              <Field label="Receipt Type" value={RECEIPT_TYPE_LABELS[receipt.receiptType]} />
              <Field label="Line of Business" value={receipt.lineOfBusinessName} />
              <Field label="Location" value={receipt.branchName} />
              <Field label="Doc No" value={receipt.docNo} />
              <Field label="Doc Date" value={receipt.docDate} />
              <Field label="Value Date" value={receipt.valueDate} />
              <Field label="Mode" value={RECEIPT_MODE_LABELS[receipt.mode]} />
              <Field label="Receipt From" value={RECEIPT_FROM_LABELS[receipt.receiptFrom]} />
              {receipt.receiptType !== 'Temporary' && <Field label="TR Ref" value={receipt.trRefNo} />}
              <Field label="Marketing Officer" value={receipt.marketingOfficerName} />
              <Field label="Receipt Amount (INR)" value={formatInr(receipt.docAmount)} />
              <Field label="Status" value={receipt.statusText} />
              <Field label="Authorized By" value={receipt.authorizedBy} />
              <Field label="Authorized Date" value={receipt.authorizedAtUtc} />
            </Box>

            <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 1 }}>Customer Information</Typography>
            <Box sx={grid}>
              <Field label="Customer Code" value={receipt.customerCode} />
              <Field label="Customer Name" value={receipt.customerName} />
              <Field label="Address" value={receipt.customer?.address} />
              <Field label="Phone" value={receipt.customer?.phone} />
              <Field label="Mobile" value={receipt.customer?.mobile} />
              <Field label="Email Id" value={receipt.customer?.email} />
              <Field label="Website" value={receipt.customer?.website} />
            </Box>

            <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 1 }}>Account Details</Typography>
            <Table size="small" sx={{ mb: 2 }}>
              <TableHead>
                <TableRow>
                  <TableCell>Prime Account No</TableCell><TableCell>Sub Account No</TableCell><TableCell>Account Description</TableCell>
                  <TableCell>Component</TableCell><TableCell>GL Account</TableCell><TableCell>SL Account</TableCell>
                  <TableCell align="right">Amount</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {receipt.lines.map(l => (
                  <TableRow key={l.id}>
                    <TableCell>{l.primeAccountNo}</TableCell><TableCell>{l.subAccountNo}</TableCell><TableCell>{l.accountDescription}</TableCell>
                    <TableCell>{l.componentCode}</TableCell><TableCell>{l.creditGlCode}</TableCell><TableCell>{l.slAccountCode}</TableCell>
                    <TableCell align="right">{formatInr(l.amount)}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>

            <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 1 }}>Add Less Details</Typography>
            <Table size="small" sx={{ mb: 2 }}>
              <TableHead>
                <TableRow>
                  <TableCell>Account Number</TableCell><TableCell>Vehicle No / Machine Serial No</TableCell><TableCell>Add or Less</TableCell>
                  <TableCell>GL Account</TableCell><TableCell>SL Account</TableCell><TableCell align="right">Amount</TableCell><TableCell>Remarks</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {receipt.addLess.length === 0 && <TableRow><TableCell colSpan={7}>No Records Found</TableCell></TableRow>}
                {receipt.addLess.map(a => (
                  <TableRow key={a.id}>
                    <TableCell>{a.accountNumber}</TableCell><TableCell>{a.vehicleOrMachineNo}</TableCell><TableCell>{a.kind}</TableCell>
                    <TableCell>{a.glAccountCode}</TableCell><TableCell>{a.slAccountCode}</TableCell>
                    <TableCell align="right">{formatInr(a.amount)}</TableCell><TableCell>{a.remarks}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>

            <Typography variant="body2" sx={{ mb: 2 }}><strong>Amount In Words:</strong> {receipt.amountInWords}</Typography>

            {audit.length > 0 && (
              <>
                <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 1 }}>Audit Trail</Typography>
                <Table size="small">
                  <TableHead><TableRow><TableCell>When (UTC)</TableCell><TableCell>Action</TableCell><TableCell>Doc No</TableCell><TableCell>Sequence No</TableCell><TableCell>By</TableCell></TableRow></TableHead>
                  <TableBody>
                    {audit.map(a => (
                      <TableRow key={a.id}>
                        <TableCell>{a.occurredAtUtc}</TableCell><TableCell>{a.action}</TableCell><TableCell>{a.docNo}</TableCell>
                        <TableCell>{a.sequenceNumber ?? ''}</TableCell><TableCell>{a.actor}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </>
            )}
          </>
        )}

        {hasBank && tab === 1 && (
          <Box sx={grid}>
            {receipt.cheque && (
              <>
                <Field label="Instrument No" value={receipt.cheque.instrumentNo} />
                <Field label="Instrument Date" value={receipt.cheque.instrumentDate} />
                <Field label="Drawee Bank" value={receipt.cheque.draweeBankName} />
                <Field label="Drawee Branch" value={receipt.cheque.draweePlaceName} />
                <Field label="Payment Gateway Ref No" value={receipt.cheque.paymentGatewayRefNo} />
                <Field label="ACK No" value={receipt.cheque.ackNo} />
              </>
            )}
            {receipt.bankTransfer && (
              <>
                <Field label="Deposit Bank" value={receipt.bankTransfer.bankName} />
                <Field label="Account No" value={receipt.bankTransfer.accountNo} />
                <Field label="MICR" value={receipt.bankTransfer.micrCode} />
                <Field label="Bank Branch" value={receipt.bankTransfer.bankBranch} />
                <Field label="GL Code" value={receipt.bankTransfer.glCode} />
                <Field label="SL Code" value={receipt.bankTransfer.slCode} />
              </>
            )}
          </Box>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Close</Button>
      </DialogActions>
    </Dialog>
  );
}
```

  - [ ] 15.7 CREATE `src/pages/ReceiptProcessing.tsx`

```tsx
import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Alert, Box, Button, Checkbox, FormControlLabel, Paper, Radio, RadioGroup, Stack, Tab, Tabs, TextField, Typography,
} from '@mui/material';
import { receiptService } from '../services';
import type {
  CashDenominationOption, Receipt, ReceiptBranchOption, ReceiptComponentOption, ReceiptContractOption, ReceiptCustomer,
  ReceiptDraweeBankOption, ReceiptDraweeBranchOption, ReceiptEntryMode, ReceiptFaBankAccountOption, ReceiptFrom,
  ReceiptLobOption, ReceiptMode, ReceiptOfficerOption, ReceiptType,
} from '../models/receipt.model';
import { RECEIPT_FROM_LABELS, RECEIPT_MODE_LABELS, RECEIPT_TYPE_LABELS } from '../models/receipt.model';
import {
  buildRequest, createEmptyForm, DENOMINATION_MISMATCH_MESSAGE, formComputedDocAmount, formDenominationTotal, liveIssues,
  trRefVisible, validateForm,
} from '../utils/receiptForm';
import type { ReceiptForm } from '../utils/receiptForm';
import { formatInr } from '../utils/receiptCalculator';
import { amountInWords } from '../utils/amountInWords';
import CustomerSection from '../components/receipt/CustomerSection';
import AccountDetailsGrid from '../components/receipt/AccountDetailsGrid';
import AddLessGrid from '../components/receipt/AddLessGrid';
import CashDenominationDialog from '../components/receipt/CashDenominationDialog';
import BankDetailsTab from '../components/receipt/BankDetailsTab';

const grid = { display: 'grid', gridTemplateColumns: { xs: '1fr', md: 'repeat(3, 1fr)' }, gap: 2 } as const;

function messageOf(e: unknown): string {
  const data = (e as { response?: { data?: { message?: string } } })?.response?.data;
  return data?.message ?? (e instanceof Error ? e.message : 'The request failed.');
}

interface SelectFieldProps {
  id: string; label: string; value: string; required?: boolean; disabled?: boolean;
  options: { value: string; label: string }[]; allowBlank?: boolean; onChange: (value: string) => void;
}

function SelectField({ id, label, value, required, disabled, options, allowBlank = true, onChange }: SelectFieldProps) {
  return (
    <TextField id={id} label={label} required={required} disabled={disabled} select size="small" value={value}
      SelectProps={{ native: true }} InputLabelProps={{ shrink: true }} onChange={e => onChange(e.target.value)}>
      {allowBlank && <option value="">Select</option>}
      {options.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
    </TextField>
  );
}

/** Receipt Processing - Create (FINNOVA-61). Tabs: General and Bank Details. Buttons: Save, Clear, Cancel. */
export default function ReceiptProcessing() {
  const navigate = useNavigate();
  const [form, setForm] = useState<ReceiptForm>(() => createEmptyForm());
  const [tab, setTab] = useState(0);
  const [denomOpen, setDenomOpen] = useState(false);

  const [lobs, setLobs] = useState<ReceiptLobOption[]>([]);
  const [branches, setBranches] = useState<ReceiptBranchOption[]>([]);
  const [officers, setOfficers] = useState<ReceiptOfficerOption[]>([]);
  const [components, setComponents] = useState<ReceiptComponentOption[]>([]);
  const [draweeBanks, setDraweeBanks] = useState<ReceiptDraweeBankOption[]>([]);
  const [draweeBranches, setDraweeBranches] = useState<ReceiptDraweeBranchOption[]>([]);
  const [faAccounts, setFaAccounts] = useState<ReceiptFaBankAccountOption[]>([]);
  const [catalog, setCatalog] = useState<CashDenominationOption[]>([]);
  const [customer, setCustomer] = useState<ReceiptCustomer | null>(null);
  const [contracts, setContracts] = useState<ReceiptContractOption[]>([]);

  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState<Receipt | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [problems, setProblems] = useState<string[]>([]);

  const update = useCallback(<K extends keyof ReceiptForm>(key: K, value: ReceiptForm[K]) => {
    setForm(f => ({ ...f, [key]: value }));
  }, []);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const [l, b, o, c, d, fa, cat] = await Promise.all([
          receiptService.getLinesOfBusiness(), receiptService.getBranches(), receiptService.getMarketingOfficers(),
          receiptService.getComponents(), receiptService.getDraweeBanks(), receiptService.getBankAccounts(),
          receiptService.getDenominations(),
        ]);
        if (cancelled) return;
        setLobs(l); setBranches(b); setOfficers(o); setComponents(c); setDraweeBanks(d); setFaAccounts(fa); setCatalog(cat);
      } catch (e) {
        if (!cancelled) setError(messageOf(e));
      }
    })();
    return () => { cancelled = true; };
  }, []);

  useEffect(() => {
    const bankId = form.cheque.draweeBankId;
    if (!bankId) { setDraweeBranches([]); return; }
    let cancelled = false;
    receiptService.getDraweeBranches(bankId, form.valueDate)
      .then(r => { if (!cancelled) setDraweeBranches(r); })
      .catch(e => { if (!cancelled) setError(messageOf(e)); });
    return () => { cancelled = true; };
  }, [form.cheque.draweeBankId, form.valueDate]);

  const loadCustomer = useCallback(async (code: string) => {
    const trimmed = code.trim();
    if (!trimmed) { setCustomer(null); setContracts([]); return; }
    try {
      const [c, k] = await Promise.all([receiptService.getCustomer(trimmed), receiptService.getContracts(trimmed)]);
      setCustomer(c); setContracts(k); setError(null);
    } catch (e) {
      setCustomer(null); setContracts([]); setError(messageOf(e));
    }
  }, []);

  const issues = useMemo(() => liveIssues(form), [form]);
  const computed = useMemo(() => formComputedDocAmount(form), [form]);
  const entered = Number(form.docAmount) || 0;
  const wordsAmount = issues.docAmountMismatch ? computed : entered;
  const canSave = !saving && !saved && !issues.denominationMismatch && !issues.docAmountMismatch;
  const isCash = form.mode === 'Cash';

  const onModeChange = (mode: ReceiptMode) => {
    setForm(f => ({ ...f, mode }));
    if (mode === 'Cash') setTab(0);
  };

  const handleSave = async () => {
    const found = validateForm(form);
    setProblems(found);
    if (found.length > 0) return;
    setError(null);
    setSaving(true);
    try {
      const result = await receiptService.create(buildRequest(form, catalog));
      setSaved(result.receipt);
    } catch (e) {
      setError(messageOf(e));
    } finally {
      setSaving(false);
    }
  };

  const handleClear = () => {
    setForm(createEmptyForm());
    setCustomer(null); setContracts([]); setSaved(null); setError(null); setProblems([]); setTab(0);
  };

  return (
    <Box sx={{ p: 2 }}>
      <Typography variant="h5" sx={{ mb: 2 }}>Receipt Processing</Typography>

      {saved && (
        <Alert severity="success" sx={{ mb: 2 }}>
          Receipt saved. Doc No: <strong>{saved.docNo}</strong>. Status: {saved.statusText}.
        </Alert>
      )}
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      {problems.length > 0 && (
        <Alert severity="warning" sx={{ mb: 2 }}>
          <ul style={{ margin: 0, paddingLeft: 18 }}>{problems.map(p => <li key={p}>{p}</li>)}</ul>
        </Alert>
      )}

      <Tabs value={tab} onChange={(_, v: number) => setTab(v)} sx={{ mb: 2 }}>
        <Tab label="General" />
        <Tab label="Bank Details" disabled={isCash} />
      </Tabs>

      {tab === 0 && (
        <Stack spacing={2}>
          <Paper sx={{ p: 2 }}>
            <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 1 }}>Receipt Information</Typography>
            <RadioGroup row value={form.entryMode} onChange={(_, v) => update('entryMode', v as ReceiptEntryMode)}>
              <FormControlLabel value="Manual" control={<Radio />} label="Manual" />
              <FormControlLabel value="Online" control={<Radio />} label="Online" />
            </RadioGroup>
            <Box sx={grid}>
              <SelectField id="receiptType" label="Receipt Type" required value={form.receiptType} allowBlank={false}
                options={(Object.keys(RECEIPT_TYPE_LABELS) as ReceiptType[]).map(k => ({ value: k, label: RECEIPT_TYPE_LABELS[k] }))}
                onChange={v => update('receiptType', v as ReceiptType)} />
              {form.entryMode === 'Online'
                ? <TextField id="docNo" label="Doc No" size="small" value="Generated on Save" disabled InputLabelProps={{ shrink: true }} />
                : <TextField id="docNo" label="Doc No" required size="small" value={form.docNo} inputProps={{ maxLength: 30 }}
                    InputLabelProps={{ shrink: true }} onChange={e => update('docNo', e.target.value)} />}
              <TextField id="docDate" label="Doc Date" required type="date" size="small" value={form.docDate}
                InputLabelProps={{ shrink: true }} onChange={e => update('docDate', e.target.value)} />
              <TextField id="valueDate" label="Value Date" required type="date" size="small" value={form.valueDate}
                InputLabelProps={{ shrink: true }} onChange={e => update('valueDate', e.target.value)} />
              {trRefVisible(form.receiptType) && (
                <TextField id="trRefNo" label="TR Ref No" size="small" value={form.trRefNo} inputProps={{ maxLength: 30 }}
                  InputLabelProps={{ shrink: true }} onChange={e => update('trRefNo', e.target.value)} />
              )}
            </Box>
          </Paper>

          <Paper sx={{ p: 2 }}>
            <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 1 }}>Receipt Details</Typography>
            <Box sx={grid}>
              <SelectField id="lineOfBusiness" label="Line of Business" required value={form.lineOfBusinessId}
                options={lobs.map(l => ({ value: l.id, label: l.lobName }))} onChange={v => update('lineOfBusinessId', v)} />
              <SelectField id="location" label="Location" required value={form.branchId}
                options={branches.map(b => ({ value: b.id, label: b.label }))} onChange={v => update('branchId', v)} />
              <SelectField id="marketingOfficer" label="Marketing Officer" required value={form.marketingOfficerUserId}
                options={officers.map(o => ({ value: o.id, label: o.name }))} onChange={v => update('marketingOfficerUserId', v)} />
              <SelectField id="mode" label="Mode" required value={form.mode} allowBlank={false}
                options={(Object.keys(RECEIPT_MODE_LABELS) as ReceiptMode[]).map(k => ({ value: k, label: RECEIPT_MODE_LABELS[k] }))}
                onChange={v => onModeChange(v as ReceiptMode)} />
              <SelectField id="receiptFrom" label="Receipt From" required value={form.receiptFrom} allowBlank={false}
                options={(Object.keys(RECEIPT_FROM_LABELS) as ReceiptFrom[]).map(k => ({ value: k, label: RECEIPT_FROM_LABELS[k] }))}
                onChange={v => update('receiptFrom', v as ReceiptFrom)} />
              <TextField id="docAmount" label="Doc Amount" required size="small" value={form.docAmount}
                InputLabelProps={{ shrink: true }} inputProps={{ inputMode: 'decimal' }}
                error={issues.docAmountMismatch}
                helperText={issues.docAmountMismatch ? `Lines and Add/Less total ${formatInr(computed)}` : 'INR'}
                onChange={e => update('docAmount', e.target.value)} />
              <FormControlLabel label="GL Based"
                control={<Checkbox checked={form.glBased} onChange={e => update('glBased', e.target.checked)} />} />
              <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
                <Button variant="outlined" size="small" disabled={!isCash} onClick={() => setDenomOpen(true)}>Cash Denominations</Button>
                {isCash && (
                  <Typography variant="body2">Total: {formatInr(formDenominationTotal(form))}</Typography>
                )}
              </Box>
            </Box>
            {isCash && issues.denominationMismatch && (
              <Typography color="error" role="alert" variant="body2" sx={{ mt: 1 }}>{DENOMINATION_MISMATCH_MESSAGE}</Typography>
            )}
          </Paper>

          <Paper sx={{ p: 2 }}>
            <CustomerSection customerCode={form.customerCode} customer={customer} contracts={contracts}
              onCodeChange={code => update('customerCode', code)} onLoad={loadCustomer} />
          </Paper>

          <Paper sx={{ p: 2 }}>
            <AccountDetailsGrid lines={form.lines} contracts={contracts} components={components} glBased={form.glBased}
              onChange={lines => update('lines', lines)} />
          </Paper>

          <Paper sx={{ p: 2 }}>
            <AddLessGrid rows={form.addLess} contracts={contracts} onChange={rows => update('addLess', rows)} />
          </Paper>

          <Paper sx={{ p: 2 }}>
            <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>Amount In Words</Typography>
            <Typography variant="body1" data-testid="amount-in-words">{amountInWords(wordsAmount)}</Typography>
          </Paper>
        </Stack>
      )}

      {tab === 1 && !isCash && (
        <Paper sx={{ p: 2 }}>
          <BankDetailsTab mode={form.mode} cheque={form.cheque} draweeBanks={draweeBanks} draweeBranches={draweeBranches}
            faAccounts={faAccounts} faBankAccountId={form.faBankAccountId}
            onChequeChange={change => setForm(f => ({ ...f, cheque: { ...f.cheque, ...change } }))}
            onFaBankAccountChange={id => update('faBankAccountId', id)} />
        </Paper>
      )}

      <Stack direction="row" spacing={2} sx={{ mt: 3 }}>
        <Button variant="contained" onClick={handleSave} disabled={!canSave}>Save</Button>
        <Button variant="outlined" onClick={handleClear}>Clear</Button>
        <Button onClick={() => navigate('/receipts')}>Cancel</Button>
      </Stack>

      <CashDenominationDialog open={denomOpen} catalog={catalog} counts={form.denominationCounts} docAmount={entered}
        onChange={counts => update('denominationCounts', counts)} onClose={() => setDenomOpen(false)} />
    </Box>
  );
}
```

  - [ ] 15.8 CREATE `src/pages/ReceiptList.tsx`

```tsx
import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Box, Button, Stack, TextField, Typography } from '@mui/material';
import { DataGrid } from '@mui/x-data-grid';
import type { GridColDef } from '@mui/x-data-grid';
import AddIcon from '@mui/icons-material/Add';
import { receiptService } from '../services';
import type { Receipt, ReceiptAuditEntry, ReceiptListItem, ReceiptMode, ReceiptType } from '../models/receipt.model';
import { RECEIPT_MODE_LABELS, RECEIPT_TYPE_LABELS } from '../models/receipt.model';
import { formatInr } from '../utils/receiptCalculator';
import ReceiptDetailDialog from '../components/receipt/ReceiptDetailDialog';

const columns: GridColDef<ReceiptListItem>[] = [
  { field: 'docNo', headerName: 'Doc No', flex: 1.2, minWidth: 160 },
  { field: 'receiptType', headerName: 'Type', flex: 1, valueFormatter: (v: ReceiptType) => RECEIPT_TYPE_LABELS[v] ?? v },
  { field: 'mode', headerName: 'Mode', flex: 0.8, valueFormatter: (v: ReceiptMode) => RECEIPT_MODE_LABELS[v] ?? v },
  { field: 'statusText', headerName: 'Status', flex: 1 },
  { field: 'docDate', headerName: 'Doc Date', flex: 0.8 },
  { field: 'customerCode', headerName: 'Customer Code', flex: 1 },
  { field: 'customerName', headerName: 'Customer Name', flex: 1.2 },
  { field: 'branchName', headerName: 'Location', flex: 1 },
  { field: 'docAmount', headerName: 'Amount (INR)', flex: 0.8, align: 'right', headerAlign: 'right',
    valueFormatter: (v: number) => formatInr(v) },
];

/** Receipt list with search and filters, plus a read-only detail dialog. */
export default function ReceiptList() {
  const navigate = useNavigate();
  const [rows, setRows] = useState<ReceiptListItem[]>([]);
  const [rowCount, setRowCount] = useState(0);
  const [loading, setLoading] = useState(false);
  const [search, setSearch] = useState('');
  const [type, setType] = useState<ReceiptType | ''>('');
  const [mode, setMode] = useState<ReceiptMode | ''>('');
  const [paging, setPaging] = useState({ page: 0, pageSize: 20 });
  const [selected, setSelected] = useState<Receipt | null>(null);
  const [audit, setAudit] = useState<ReceiptAuditEntry[]>([]);
  const debounce = useRef<ReturnType<typeof setTimeout>>();

  const load = useCallback(async (s: string, t: ReceiptType | '', m: ReceiptMode | '', page: number, pageSize: number) => {
    setLoading(true);
    try {
      const res = await receiptService.list({
        search: s || undefined, receiptType: t || undefined, mode: m || undefined, page: page + 1, pageSize,
      });
      setRows(res.data);
      setRowCount(res.total);
    } finally {
      setLoading(false);   // errors are toasted by the shared axios interceptor
    }
  }, []);

  useEffect(() => {
    if (debounce.current) clearTimeout(debounce.current);
    debounce.current = setTimeout(() => { load(search, type, mode, paging.page, paging.pageSize); }, 300);
    return () => { if (debounce.current) clearTimeout(debounce.current); };
  }, [search, type, mode, paging, load]);

  const open = async (id: string) => {
    const [receipt, trail] = await Promise.all([receiptService.getById(id), receiptService.getAuditTrail(id)]);
    setSelected(receipt);
    setAudit(trail);
  };

  return (
    <Box sx={{ p: 2 }}>
      <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 2 }}>
        <Typography variant="h5">Receipts</Typography>
        <Button variant="contained" startIcon={<AddIcon />} onClick={() => navigate('/receipts/new')}>New Receipt</Button>
      </Stack>
      <Stack direction={{ xs: 'column', md: 'row' }} spacing={2} sx={{ mb: 2 }}>
        <TextField size="small" label="Search Doc No or Customer Code" value={search}
          onChange={e => { setSearch(e.target.value); setPaging(p => ({ ...p, page: 0 })); }} />
        <TextField select size="small" label="Type" value={type} SelectProps={{ native: true }} InputLabelProps={{ shrink: true }}
          onChange={e => { setType(e.target.value as ReceiptType | ''); setPaging(p => ({ ...p, page: 0 })); }}>
          <option value="">All</option>
          {(Object.keys(RECEIPT_TYPE_LABELS) as ReceiptType[]).map(k => <option key={k} value={k}>{RECEIPT_TYPE_LABELS[k]}</option>)}
        </TextField>
        <TextField select size="small" label="Mode" value={mode} SelectProps={{ native: true }} InputLabelProps={{ shrink: true }}
          onChange={e => { setMode(e.target.value as ReceiptMode | ''); setPaging(p => ({ ...p, page: 0 })); }}>
          <option value="">All</option>
          {(Object.keys(RECEIPT_MODE_LABELS) as ReceiptMode[]).map(k => <option key={k} value={k}>{RECEIPT_MODE_LABELS[k]}</option>)}
        </TextField>
      </Stack>
      <DataGrid rows={rows} columns={columns} loading={loading} rowCount={rowCount} paginationMode="server"
        paginationModel={{ page: paging.page, pageSize: paging.pageSize }}
        onPaginationModelChange={m => setPaging({ page: m.page, pageSize: m.pageSize })}
        pageSizeOptions={[10, 20, 50, 100]} disableRowSelectionOnClick autoHeight
        onRowClick={p => { void open(String(p.id)); }} />
      <ReceiptDetailDialog receipt={selected} audit={audit} onClose={() => setSelected(null)} />
    </Box>
  );
}
```

  - [ ] 15.9 MODIFY `src/App.tsx`. Add the imports next to the other page imports:

```tsx
import ReceiptProcessing from './pages/ReceiptProcessing';
import ReceiptList from './pages/ReceiptList';
```

Add the routes inside the protected layout route, after the `/demand-processing/spooling` route:

```tsx
                <Route path="/receipts" element={<ReceiptList />} />
                <Route path="/receipts/new" element={<ReceiptProcessing />} />
```

  - [ ] 15.10 MODIFY `src/components/Layout.tsx`. In the `Operations` group, after the `Demand Spooling` item, add (both icons are already imported in that file):

```tsx
      { text: 'Receipt Processing', icon: <ReceiptLongIcon />, path: '/receipts/new' },
      { text: 'Receipts', icon: <ReceiptLongIcon />, path: '/receipts' },
```

    If the permission provider gates routes or menu items by a program name, register a `ReceiptProcessing` program in the screen-permission catalog as well. That wiring is outside this spec.

- [ ] 16. Frontend tests (Vitest + React Testing Library, jsdom)
  - [ ] 16.1 CREATE `src/utils/amountInWords.test.ts`

```ts
import { describe, expect, it } from 'vitest';
import { amountInWords } from './amountInWords';

// The same vectors are asserted by the backend ReceiptHelperProperties tests.
describe('amountInWords', () => {
  it.each([
    [0, 'Rupees Zero Only'],
    [0.05, 'Rupees Zero and Five Paise Only'],
    [3500, 'Rupees Three Thousand Five Hundred Only'],
    [100000, 'Rupees One Lakh Only'],
    [10000000, 'Rupees One Crore Only'],
    [1234567.5, 'Rupees Twelve Lakh Thirty-Four Thousand Five Hundred Sixty-Seven and Fifty Paise Only'],
    [999999999.99, 'Rupees Ninety-Nine Crore Ninety-Nine Lakh Ninety-Nine Thousand Nine Hundred Ninety-Nine and Ninety-Nine Paise Only'],
  ])('converts %s', (amount, expected) => {
    expect(amountInWords(amount)).toBe(expected);
  });

  it('returns an empty string outside the supported range', () => {
    expect(amountInWords(-1)).toBe('');
    expect(amountInWords(1_000_000_000)).toBe('');
    expect(amountInWords(Number.NaN)).toBe('');
  });
});
```

  - [ ] 16.2 CREATE `src/utils/receiptCalculator.test.ts`

```ts
import { describe, expect, it } from 'vitest';
import { denominationTotal, docAmount, toPaise } from './receiptCalculator';

describe('receiptCalculator', () => {
  it('computes Doc Amount as lines plus Add minus Less', () => {
    expect(docAmount([3200, 300], [])).toBe(3500);
    expect(docAmount([3200, 300], [{ kind: 'Add', amount: 300 }, { kind: 'Less', amount: 400 }])).toBe(3400);
  });

  it('is exact for amounts that are inexact in binary floating point', () => {
    expect(docAmount([0.1, 0.2], [])).toBe(0.3);
  });

  it('an Add and a Less of the same value cancel', () => {
    expect(docAmount([123.45], [{ kind: 'Add', amount: 77.7 }, { kind: 'Less', amount: 77.7 }])).toBe(123.45);
  });

  it('sums face value times count for denominations', () => {
    expect(denominationTotal([{ faceValue: 500, count: 1 }, { faceValue: 0.5, count: 3 }])).toBe(501.5);
    expect(denominationTotal([{ faceValue: 100, count: 4 }, { faceValue: 50, count: 1 }])).toBe(450);
  });

  it('converts to paise and ignores invalid input', () => {
    expect(toPaise('12.34')).toBe(1234);
    expect(toPaise('abc')).toBe(0);
  });
});
```

  - [ ] 16.3 CREATE `src/services/mock/receipt.mock.test.ts`

```ts
import { beforeEach, describe, expect, it } from 'vitest';
import { receiptMockService, resetReceiptMock } from './receipt.mock';
import { DENOMINATION_MISMATCH_MESSAGE, uuidv4 } from '../../utils/receiptForm';
import type { CreateReceiptRequest } from '../../models/receipt.model';

const today = '2025-06-30';

function cheque(): CreateReceiptRequest {
  return {
    clientRequestId: uuidv4(), entryMode: 'Online', docNo: null, receiptType: 'Regular', docDate: today, valueDate: today,
    lineOfBusinessId: 'lob-vehicle', branchId: 'br-001', marketingOfficerUserId: 'mo-001', mode: 'Cheque',
    receiptFrom: 'Customer', glBased: true, customerCode: 'CUST-100234', trRefNo: null, docAmount: 3500,
    lines: [
      { primeAccountNo: 'ACC-889021', componentCode: 'INSTALLMENT_INFLOW', glAccountCode: '110100101', amount: 3200 },
      { primeAccountNo: 'ACC-889021', componentCode: 'ODI', glAccountCode: '110100101', amount: 300 },
    ],
    addLess: null, denominations: null,
    cheque: { instrumentNo: '458921', instrumentDate: today, draweeBankId: 'db-hdfc', draweeBranchId: 'dbr-hdfc-mum' },
    bankTransfer: null,
  };
}

describe('receiptMockService', () => {
  beforeEach(() => resetReceiptMock());

  it('saves the Regular cheque scenario as Pending Authorization with a generated Doc No and audit entry', async () => {
    const { receipt, alreadyExisted } = await receiptMockService.create(cheque());
    expect(alreadyExisted).toBe(false);
    expect(receipt.docNo).toMatch(/^RRC-\d{4}-0000001$/);
    expect(receipt.statusText).toBe('Pending Authorization');
    expect(receipt.amountInWords).toBe('Rupees Three Thousand Five Hundred Only');
    const trail = await receiptMockService.getAuditTrail(receipt.id);
    expect(trail).toHaveLength(1);
    expect(trail[0].sequenceNumber).toBe(1);
  });

  it('returns the same receipt for the same clientRequestId', async () => {
    const request = cheque();
    const first = await receiptMockService.create(request);
    const second = await receiptMockService.create(request);
    expect(second.alreadyExisted).toBe(true);
    expect(second.receipt.id).toBe(first.receipt.id);
  });

  it('rejects a Cash receipt whose denominations do not match the Doc Amount', async () => {
    const request: CreateReceiptRequest = {
      ...cheque(), mode: 'Cash', cheque: null, docAmount: 500,
      lines: [{ primeAccountNo: 'ACC-889021', componentCode: 'ODI', glAccountCode: '110100101', amount: 500 }],
      denominations: [{ kind: 'Note', faceValue: 100, count: 4 }, { kind: 'Note', faceValue: 50, count: 1 }],
    };
    await expect(receiptMockService.create(request)).rejects.toMatchObject({
      response: { status: 422, data: { rule: 'CASH_DENOMINATION_MISMATCH', message: DENOMINATION_MISMATCH_MESSAGE } },
    });
  });

  it('rejects an account that does not belong to the customer', async () => {
    const request = cheque();
    request.lines[0].primeAccountNo = 'ACC-889022';
    await expect(receiptMockService.create(request)).rejects.toMatchObject({ response: { status: 422 } });
  });

  it('lists saved receipts newest first and filters by search', async () => {
    await receiptMockService.create(cheque());
    const page = await receiptMockService.list({ search: 'cust-100234', page: 1, pageSize: 10 });
    expect(page.total).toBe(1);
    expect((await receiptMockService.list({ search: 'nothing' })).total).toBe(0);
  });
});
```

  - [ ] 16.4 CREATE `src/pages/ReceiptProcessing.test.tsx`

```tsx
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import ReceiptProcessing from './ReceiptProcessing';

const svc = vi.hoisted(() => ({
  create: vi.fn(),
  getLinesOfBusiness: vi.fn(),
  getBranches: vi.fn(),
  getMarketingOfficers: vi.fn(),
  getComponents: vi.fn(),
  getDraweeBanks: vi.fn(),
  getDraweeBranches: vi.fn(),
  getBankAccounts: vi.fn(),
  getDenominations: vi.fn(),
  getCustomer: vi.fn(),
  getContracts: vi.fn(),
  searchCustomers: vi.fn(),
}));

vi.mock('../services', () => ({ receiptService: svc }));

function renderPage() {
  return render(<MemoryRouter><ReceiptProcessing /></MemoryRouter>);
}

describe('ReceiptProcessing', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    svc.getLinesOfBusiness.mockResolvedValue([{ id: 'lob-1', lobName: 'Vehicle', lobDescription: '' }]);
    svc.getBranches.mockResolvedValue([{ id: 'br-1', code: 'BR001', label: 'Mumbai Fort Branch' }]);
    svc.getMarketingOfficers.mockResolvedValue([{ id: 'mo-1', userCode: 'MO001', name: 'Meera Shah' }]);
    svc.getComponents.mockResolvedValue([{ code: 'ODI', label: 'Overdue Interest (ODI)' }]);
    svc.getDraweeBanks.mockResolvedValue([]);
    svc.getDraweeBranches.mockResolvedValue([]);
    svc.getBankAccounts.mockResolvedValue([]);
    svc.getDenominations.mockResolvedValue([{ kind: 'Note', faceValue: 500, label: 'Note 500' }]);
  });

  it('hides TR Ref No for Temporary receipts and shows it for Regular and Third-Party', async () => {
    renderPage();
    expect(screen.queryByLabelText(/TR Ref No/)).toBeNull();

    fireEvent.change(screen.getByLabelText(/Receipt Type/), { target: { value: 'Regular' } });
    expect(await screen.findByLabelText(/TR Ref No/)).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText(/Receipt Type/), { target: { value: 'ThirdParty' } });
    expect(screen.getByLabelText(/TR Ref No/)).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText(/Receipt Type/), { target: { value: 'Temporary' } });
    expect(screen.queryByLabelText(/TR Ref No/)).toBeNull();
  });

  it('disables the Bank Details tab for Cash and enables it for Cheque', async () => {
    renderPage();
    const bankTab = screen.getByRole('tab', { name: 'Bank Details' });
    expect(bankTab).toBeEnabled();                       // default mode is Cheque

    fireEvent.change(screen.getByLabelText(/^Mode/), { target: { value: 'Cash' } });
    await waitFor(() => expect(screen.getByRole('tab', { name: 'Bank Details' })).toBeDisabled());
    expect(screen.getByRole('button', { name: 'Cash Denominations' })).toBeEnabled();

    fireEvent.change(screen.getByLabelText(/^Mode/), { target: { value: 'Cheque' } });
    await waitFor(() => expect(screen.getByRole('tab', { name: 'Bank Details' })).toBeEnabled());
    expect(screen.getByRole('button', { name: 'Cash Denominations' })).toBeDisabled();
  });

  it('shows the denomination mismatch message and disables Save for a Cash receipt', async () => {
    renderPage();
    fireEvent.change(screen.getByLabelText(/^Mode/), { target: { value: 'Cash' } });
    fireEvent.change(screen.getByLabelText(/^Doc Amount/), { target: { value: '500' } });

    expect(await screen.findByText('Document Amount must match the sum of Cash Denominations')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled();
  });

  it('shows Doc No as generated on Save for Online entry and editable for Manual', async () => {
    renderPage();
    expect(screen.getByLabelText(/^Doc No/)).toHaveValue('Generated on Save');
    fireEvent.click(screen.getByLabelText('Manual'));
    await waitFor(() => expect(screen.getByLabelText(/^Doc No/)).toHaveValue(''));
    expect(screen.getByLabelText(/^Doc No/)).toBeEnabled();
  });

  it('disables the GL and SL cells of a line while GL Based is No', async () => {
    renderPage();
    expect(screen.getByLabelText('GL Account 1')).toBeDisabled();
    fireEvent.click(screen.getByLabelText('GL Based'));
    await waitFor(() => expect(screen.getByLabelText('GL Account 1')).toBeEnabled());
  });

  it('blocks Save with a problem list, then shows the server error and keeps the entered values', async () => {
    svc.getBankAccounts.mockResolvedValue([{
      id: 'fa-1', bankName: 'HDFC Bank Ltd', bankBranch: 'Mumbai Fort', accountNumber: '50200012345678',
      micrCode: '400240002', glCode: '110100101', slCode: null,
    }]);
    svc.getCustomer.mockResolvedValue({
      id: 'cu-1', customerCode: 'CUST-100234', customerName: 'Ravi Kumar', address: null, phone: null, mobile: null, email: null, website: null,
    });
    svc.getContracts.mockResolvedValue([{ accountNumber: 'ACC-889021', accountName: 'Vehicle Loan - Ravi Kumar' }]);
    svc.create.mockRejectedValue({ response: { data: { message: 'Account ACC-889021 is not live.' } } });
    renderPage();

    await screen.findByRole('option', { name: 'Vehicle' });
    fireEvent.change(screen.getByLabelText(/Line of Business/), { target: { value: 'lob-1' } });
    fireEvent.change(screen.getByLabelText(/^Location/), { target: { value: 'br-1' } });
    fireEvent.change(screen.getByLabelText(/Marketing Officer/), { target: { value: 'mo-1' } });
    fireEvent.change(screen.getByLabelText(/^Mode/), { target: { value: 'BankTransfer' } });
    fireEvent.change(screen.getByLabelText(/Customer Code/), { target: { value: 'CUST-100234' } });
    fireEvent.blur(screen.getByLabelText(/Customer Code/));
    await waitFor(() => expect(svc.getContracts).toHaveBeenCalledWith('CUST-100234'));
    await screen.findByRole('option', { name: 'ACC-889021' });

    fireEvent.change(screen.getByLabelText('Prime Account No 1'), { target: { value: 'ACC-889021' } });
    fireEvent.change(screen.getByLabelText('Component 1'), { target: { value: 'ODI' } });
    fireEvent.change(screen.getByLabelText('Line Amount 1'), { target: { value: '500' } });
    fireEvent.change(screen.getByLabelText(/^Doc Amount/), { target: { value: '500' } });

    // Deposit Bank is required for Bank Transfer: Save reports the problem list and does not call the service.
    fireEvent.click(screen.getByRole('button', { name: 'Save' }));
    expect(await screen.findByText('Deposit Bank is required.')).toBeInTheDocument();
    expect(svc.create).not.toHaveBeenCalled();

    fireEvent.click(screen.getByRole('tab', { name: 'Bank Details' }));
    fireEvent.change(await screen.findByLabelText(/Deposit Bank/), { target: { value: 'fa-1' } });
    fireEvent.click(screen.getByRole('tab', { name: 'General' }));
    fireEvent.click(await screen.findByRole('button', { name: 'Save' }));

    expect(await screen.findByText('Account ACC-889021 is not live.')).toBeInTheDocument();
    expect(svc.create).toHaveBeenCalledTimes(1);
    expect(screen.getByLabelText(/^Doc Amount/)).toHaveValue('500');
  });
});
```
- [ ] 17. Verification (run yourself; this plan does not run builds or tests)
  - [ ] 17.1 Run the backend and frontend checks

```powershell
# Backend (repository root d:\Projects\Finnova\Finnova-API)
dotnet build Finnova.Backend.slnx
dotnet test Finnova.Tests --filter "FullyQualifiedName~Receipts|FullyQualifiedName~RoleSeedingAndGatewayTests"
```

```powershell
# Frontend (repository root D:\Projects\Finnova\Finnova-UI)
npm run build
npx vitest --run src/utils src/services/mock/receipt.mock.test.ts src/pages/ReceiptProcessing.test.tsx
```

## Notes

- **Open confirmations (see requirements Assumptions):** A1 localization and denominations, A2 authorize/revoke/posting as a follow-up, A4 Frozen accounts, A8 F&A Bank Master ownership, A14 TR Ref No linkage, A15 cheque controls (stale, post-dated, duplicate), A17 LOB/Branch scoping of users.
- **Appropriation:** the legacy screen has an "Appropriation" button (automatic allocation of the amount across dues). It is not in the story and is not implemented; the Show button lists live contracts only (Assumption A20 in `requirements.md`).
- **Number gaps:** a Document Number is issued after all checks pass; only a database failure at the final save can leave a skipped number (Assumption A6).
