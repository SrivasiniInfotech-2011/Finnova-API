namespace Finnova.Models.Contracts.Assets;

public record CreateMakeCodeRequest(string Code, string Description, bool? IsActive);
public record UpdateMakeCodeRequest(string Code, string Description, bool IsActive);
public record MakeCodeResponse(
    Guid Id, string Code, string Description, bool IsActive, DateTime CreatedAt, DateTime UpdatedAt);
