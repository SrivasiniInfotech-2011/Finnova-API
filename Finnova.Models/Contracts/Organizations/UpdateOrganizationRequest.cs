namespace Finnova.Models.Contracts.Organizations;

public record UpdateOrganizationRequest(
    string Name,
    string Code,
    string? Description,
    string? Address,
    string? Phone,
    string? Email,
    string Status
);
