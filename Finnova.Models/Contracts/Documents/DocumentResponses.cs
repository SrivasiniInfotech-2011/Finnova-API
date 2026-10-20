namespace Finnova.Models.Contracts.Documents;

// ---- Applicability rule in responses. ----
public record ApplicabilityRuleResponse(
    Guid Id, string Constitution, string EntityRole, bool IsMandatory
);

// ---- Full admin-facing response. ----
public record DocumentResponse(
    Guid Id,
    string DocCode,
    string DocName,
    string? DocShortName,
    string? Description,
    string DocCategory,
    string ApplicableEntity,
    bool IsMandatory,
    bool HasExpiry,
    bool RequiresMasking,
    bool IsAadhaarDocument,
    string ValidationEngine,
    string? FormatRegex,
    string? MaskPattern,
    int MaxFileSizeMb,
    IReadOnlyList<string> AllowedMimeTypes,
    string Status,                 // DocumentStatus name
    string? MakerId,
    string? CheckerId,
    int AuthorizedVersionNumber,
    string? RejectionReason,
    IReadOnlyList<ApplicabilityRuleResponse> ApplicabilityRules,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

// ---- Checker review: live vs pending side by side (R5.1). ----
public record DocumentPendingReviewResponse(
    Guid Id,
    string Status,
    DocumentResponse Live,         // currently authorized values
    DocumentResponse Pending       // proposed values (from PendingValuesJson, or the row itself
                                   // for a never-authorized PendingApproval doc)
);

// ---- Audit entry (R8.4). ----
public record DocumentAuditEntryResponse(
    Guid Id,
    Guid DocumentId,
    string Action,
    string? OldValues,
    string? NewValues,
    string Summary,
    string? Remarks,
    string ChangedBy,
    DateTime ChangedAtUtc
);

// ---- Active-document consumption projection for Customer Master (R11.2). ----
public record ActiveDocumentForConsumptionResponse(
    Guid Id,
    string DocCode,
    string DocName,
    bool IsMandatory,              // resolved from the matching applicability rule (R11.2)
    bool HasExpiry,
    string ValidationEngine,
    string? FormatRegex,
    bool RequiresMasking,
    string? MaskPattern,
    int MaxFileSizeMb,
    IReadOnlyList<string> AllowedMimeTypes
);