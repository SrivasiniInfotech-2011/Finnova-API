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