using MediatR;
using Finnova.Models.Contracts.Nationalities;

namespace Finnova.Service.Nationality.Queries.GetNationalityAuditTrail;

/// <summary>
/// Reads the full audit trail for a single nationality, newest first (R4.4).
/// A missing id yields an empty list rather than an error (R4.5).
/// </summary>
public record GetNationalityAuditTrailQuery(
    Guid NationalityId
) : IRequest<List<NationalityAuditEntryResponse>>;
