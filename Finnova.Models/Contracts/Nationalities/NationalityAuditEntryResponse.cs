namespace Finnova.Models.Contracts.Nationalities;

// One audit row (R4.4). Action is the enum name string ("Create" | "Update").
public record NationalityAuditEntryResponse(
    Guid Id,
    Guid NationalityId,
    string Action,
    string? OldName,
    string NewName,
    string ChangedBy,
    DateTime ChangedAtUtc
);
