using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Contracts.Courts;

/// <summary>Request to update a court's editable fields. Code is immutable after creation (R2.3, R4.2).</summary>
public record UpdateCourtRequest(
    string Name,
    CourtType CourtType,
    string Jurisdiction,
    string Location,
    bool IsActive);