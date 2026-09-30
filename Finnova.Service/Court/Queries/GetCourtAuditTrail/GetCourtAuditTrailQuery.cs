using Finnova.Models.Contracts.Courts;
using MediatR;

namespace Finnova.Service.Court.Queries.GetCourtAuditTrail;

public record GetCourtAuditTrailQuery(Guid CourtId) : IRequest<List<CourtAuditEntryResponse>>;