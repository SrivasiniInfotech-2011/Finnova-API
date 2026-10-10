namespace Finnova.Models.Contracts.Assets;

public record CreateModelCodeRequest(string Code, string Description, bool? IsActive);
public record UpdateModelCodeRequest(string Code, string Description, bool IsActive);
public record ModelCodeResponse(
    Guid Id, string Code, string Description, bool IsActive, DateTime CreatedAt, DateTime UpdatedAt);
