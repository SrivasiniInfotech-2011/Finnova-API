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