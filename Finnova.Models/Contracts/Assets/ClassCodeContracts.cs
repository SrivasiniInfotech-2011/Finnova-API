namespace Finnova.Models.Contracts.Assets;

// Create (R2.1). IsActive nullable -> service defaults true (R2.2).
public record CreateClassCodeRequest(string Code, string Description, bool? IsActive);
// Update editable fields (R2.5).
public record UpdateClassCodeRequest(string Code, string Description, bool IsActive);
// Admin-facing response (R2.1 returns id + resolved IsActive).
public record ClassCodeResponse(
    Guid Id, string Code, string Description, bool IsActive, DateTime CreatedAt, DateTime UpdatedAt);
