namespace Finnova.Models.Contracts.Assets;

public record CreateTypeCodeRequest(string Code, string Description, bool? IsActive);
public record UpdateTypeCodeRequest(string Code, string Description, bool IsActive);
public record TypeCodeResponse(
    Guid Id, string Code, string Description, bool IsActive, DateTime CreatedAt, DateTime UpdatedAt);
