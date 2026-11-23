namespace Finnova.Models.Domain.Enums;

public enum AuthorizationDecision
{
    PendingAuthorization = 0,
    Authorized = 1,
    Rejected = 2
}

public enum PostingStatus
{
    NotSubmitted = 0,
    PostingPending = 1,
    RetryPending = 2,
    Failed = 3,
    Posted = 4
}

public enum ReceiptBranchRelationship
{
    SameBranch = 0,
    InterBranch = 1
}

public enum ReceiptAuthorizationAction
{
    Authorize = 0,
    Reject = 1
}

public enum ReceiptAuthorizationAuditKind
{
    AuthorizationDecision = 0,
    PostingLifecycle = 1,
    IntegrityAlert = 2
}