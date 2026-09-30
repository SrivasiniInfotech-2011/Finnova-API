using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Contracts.Courts;

/// <summary>Request to create a court. IsActive is nullable so the service defaults it to true (R1.2).</summary>
public record CreateCourtRequest(
    string Code,
    string Name,
    CourtType CourtType,
    string Jurisdiction,
    string Location,
    bool? IsActive);