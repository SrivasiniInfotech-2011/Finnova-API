namespace Finnova.Models.Contracts.Entities;

public record EntityAuditEntryResponse(
    Guid Id,
    Guid EntityId,
    string Action,          // "Create" | "Update"
    string? OldValues,
    string NewValues,
    string Summary,
    string ChangedBy,
    DateTime ChangedAtUtc);