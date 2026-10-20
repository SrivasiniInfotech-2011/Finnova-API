using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

/// <summary>
/// A single document definition in the Document Master (FINNOVA-49/50/52). India-only: single
/// English display fields. The row always holds the latest submitted values; Status gates
/// consumer visibility. For an edit of an Active document, PendingValuesJson holds the pending
/// edit while the live columns keep serving consumers until authorization.
/// </summary>
public class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // --- identity & display (English-only) ---
    public string DocCode { get; set; } = string.Empty;          // max 20; letters/digits/hyphen; unique (CI)
    public string DocName { get; set; } = string.Empty;          // max 150
    public string? DocShortName { get; set; }                    // optional short label
    public string? Description { get; set; }                     // optional

    // --- classification (lookup-backed, validated against ACTIVE lookup values) ---
    public string DocCategory { get; set; } = string.Empty;      // Lookup DOC_CATEGORY code
    public string ApplicableEntity { get; set; } = string.Empty; // Lookup APPLICABLE_ENTITY code

    // --- document flags ---
    public bool IsMandatory { get; set; }                        // default false (R1.9)
    public bool HasExpiry { get; set; }                          // default false (R1.9)
    public bool RequiresMasking { get; set; }                    // default false (R1.9)
    public bool IsAadhaarDocument { get; set; }                  // Aadhaar marker (R9.4)

    // --- validation / masking configuration (stored, NOT executed — R9.11) ---
    public string ValidationEngine { get; set; } = "NONE";       // Lookup VALIDATION_ENGINE code
    public string? FormatRegex { get; set; }                     // max 500; must compile if non-empty
    public string? MaskPattern { get; set; }                     // max 200; required when RequiresMasking
    public int MaxFileSizeMb { get; set; } = 5;                  // 1..25 inclusive; default 5 (R1.6)
    public string AllowedMimeTypes { get; set; } = string.Empty; // CSV of {PDF,JPG,PNG}; non-empty

    // --- maker-checker lifecycle (mirrors GLAccount) ---
    public DocumentStatus Status { get; set; } = DocumentStatus.PendingApproval; // R1.1
    public string? MakerId { get; set; }                         // last maker of the pending change
    public string? CheckerId { get; set; }                       // last checker
    public int AuthorizedVersionNumber { get; set; }             // starts 0; +1 per authorized modification
    public string? RejectionReason { get; set; }                 // last rejection remark (<=500)

    /// <summary>
    /// Pending edit payload for an edit of an already-Active document. While set, the scalar
    /// columns above remain the LIVE (authorized) values served to consumers; the Checker view
    /// compares live columns vs this payload. Cleared on authorize (copy pending->live) or when
    /// the document was never authorized (pending values live directly in the columns).
    /// </summary>
    public string? PendingValuesJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // --- applicability matrix (R4) ---
    public List<DocumentApplicabilityRule> ApplicabilityRules { get; set; } = new();
}