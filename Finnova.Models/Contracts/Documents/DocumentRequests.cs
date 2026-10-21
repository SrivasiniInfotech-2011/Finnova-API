namespace Finnova.Models.Contracts.Documents;

// ---- Create (R1). Nullable flags/size so the service applies defaults (R1.6, R1.9). ----
public record CreateDocumentRequest(
    string DocCode,
    string DocName,
    string? DocShortName,
    string? Description,
    string DocCategory,
    string ApplicableEntity,
    bool? IsMandatory,
    bool? HasExpiry,
    bool? RequiresMasking,
    bool? IsAadhaarDocument,
    int? MaxFileSizeMb
);

// ---- Edit document attributes (R3). DocCode is NOT editable once Active (R2.3). ----
public record UpdateDocumentRequest(
    string DocName,
    string? DocShortName,
    string? Description,
    string DocCategory,
    string ApplicableEntity,
    bool IsMandatory,
    bool HasExpiry
);

// ---- One applicability mapping (R4). ----
public record ApplicabilityRuleRequest(
    string Constitution,
    string EntityRole,
    bool IsMandatory
);

// ---- Replace the applicability matrix as a pending change (R4.1). ----
public record UpdateApplicabilityRulesRequest(
    IReadOnlyList<ApplicabilityRuleRequest> Rules
);

// ---- Validation / masking configuration (R9). ----
public record UpdateValidationConfigRequest(
    string ValidationEngine,
    string? FormatRegex,
    bool RequiresMasking,
    string? MaskPattern,
    int MaxFileSizeMb,
    IReadOnlyList<string> AllowedMimeTypes   // subset of {PDF,JPG,PNG}, non-empty
);

// ---- Reject carries a mandatory remark (R5.6). Authorize/Resubmit carry no body. ----
public record RejectDocumentRequest(string Reason);