namespace Finnova.Models.Contracts.Courts;

public record CourtAuditEntryResponse(
    Guid Id,
    Guid CourtId,
    string Action,          // "Create" | "Update"
    string? OldValues,
    string NewValues,
    string Summary,
    string ChangedBy,
    DateTime ChangedAtUtc);