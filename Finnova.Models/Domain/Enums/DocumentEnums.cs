namespace Finnova.Models.Domain.Enums;

/// <summary>
/// Lifecycle state of a document definition. Domain-enum (not Lookup-backed) so the
/// maker-checker state machine is enforced in code, consistent with GLStatus.
/// </summary>
public enum DocumentStatus
{
    PendingApproval,     // newly created, never authorized (requirements: PENDING_APPROVAL)
    Active,              // authorized & consumable        (requirements: ACTIVE)
    Inactive,            // authorized then retired         (requirements: INACTIVE)
    PendingModification, // edit of an Active doc awaiting authorization (live values still served)
    ReturnedToMaker      // post-reject; pending change returned for correction (requirements: RETURNED_TO_MAKER)
}

/// <summary>Governance action captured by a document audit entry (R8).</summary>
public enum DocumentAuditAction
{
    Create,
    Submit,                 // resubmit after correction
    Update,                 // edit of attributes
    ApplicabilityChange,
    ValidationConfigChange,
    Approve,
    Reject,
    Inactivate,
    Reactivate,
    Delete
}