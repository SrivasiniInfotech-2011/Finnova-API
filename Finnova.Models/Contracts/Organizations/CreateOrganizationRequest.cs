namespace Finnova.Models.Contracts.Organizations;

public record CreateOrganizationRequest(
    string Name,
    string Code,
    string? Description,
    string? Address,
    string? Phone,
    string? Email
);
