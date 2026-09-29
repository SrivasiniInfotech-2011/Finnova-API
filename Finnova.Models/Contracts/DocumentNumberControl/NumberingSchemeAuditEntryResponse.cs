namespace Finnova.Models.Contracts.DocumentNumberControl;

/// <summary>One audit row for a scheme's configuration change (R6.6).</summary>
public record NumberingSchemeAuditEntryResponse(
    Guid Id,
    Guid SchemeId,
    string Action,          // "Create" | "Update"
    string? OldValues,
    string NewValues,
    string Summary,
    string ChangedBy,
    DateTime ChangedAtUtc);
