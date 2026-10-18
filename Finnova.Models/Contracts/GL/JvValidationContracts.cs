using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Contracts.GL;

/// <summary>One journal-voucher line to validate against GL master rules (R6).</summary>
public record JvLineInput(
    int LineNumber,
    Guid GLAccountId,
    Guid CurrencyId,
    Guid? CostCentreId,
    Guid? ProfitCentreId,
    string? SubLedgerReference);

/// <summary>Validation request issued in-process by the JV posting engine (FINNOVA-25).</summary>
public record ValidateJournalVoucherInput(
    Guid CompanyId,
    DateTime ValueDate,
    Guid BranchId,
    JvEntryMode Mode,
    IReadOnlyList<JvLineInput> Lines);

/// <summary>Per-line result: Accepted iff Reasons is empty; reasons asc by criterion (R6.12).</summary>
public record JvLineResult(int LineNumber, bool Accepted, IReadOnlyList<string> Reasons);

public record VoucherValidationResult(IReadOnlyList<JvLineResult> Lines);
