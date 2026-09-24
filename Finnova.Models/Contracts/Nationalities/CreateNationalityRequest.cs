namespace Finnova.Models.Contracts.Nationalities;

// Create (R1). IsActive nullable so the service can default it to true (R1.2).
public record CreateNationalityRequest(
    string Code,
    string Name,
    bool? IsActive
);
