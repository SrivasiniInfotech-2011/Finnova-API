namespace Finnova.Models.Domain.Enums;

/// <summary>Depth of a GL account in the four-level Chart of Accounts (L1 top .. L4 leaf).</summary>
public enum GLLevel { L1 = 1, L2 = 2, L3 = 3, L4 = 4 }

/// <summary>Accounting classification; derived from the parent GL (R1.9).</summary>
public enum AccountNature { Asset, Liability, Equity, Income, Expense }

/// <summary>Side a GL normally carries its balance on; derived from nature (R1.10).</summary>
public enum NormalBalance { Debit, Credit }

/// <summary>GL type that drives conditional-mandatory attributes (R1.11).</summary>
public enum GLType { Normal, Control, Tax }

/// <summary>Lifecycle state of a GL account.</summary>
public enum GLStatus
{
    Draft,
    PendingAuthorization,
    Active,
    Rejected,
    PendingModification,
    Frozen,
    Inactive
}

/// <summary>System GL designations configured per company (R7).</summary>
public enum SystemGLType
{
    Suspense,
    RoundOff,
    InterBranchClearingHO,
    InterBranchClearingBranch,
    RetainedEarnings,
    RealizedFX,
    UnrealizedFX
}

/// <summary>Governance action captured by an audit entry (R10).</summary>
public enum GLAuditAction
{
    Create,
    Submit,
    Approve,
    Reject,
    Update,
    ModApprove,
    ModReject,
    Freeze,
    Deactivate,
    Delete,
    SystemGLMap
}

/// <summary>Manual-vs-system indicator for a voucher validation request (R6).</summary>
public enum JvEntryMode { Manual, System }

/// <summary>Supported export formats (R8.7/R8.10).</summary>
public enum ExportFormat { Xlsx, Csv }

/// <summary>State of a pending non-structural modification version (R4).</summary>
public enum GLVersionState { Pending, Authorized, Rejected }
