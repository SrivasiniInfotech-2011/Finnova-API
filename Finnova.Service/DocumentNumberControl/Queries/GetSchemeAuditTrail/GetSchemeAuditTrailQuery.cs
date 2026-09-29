using Finnova.Models.Contracts.DocumentNumberControl;
using MediatR;

namespace Finnova.Service.DocumentNumberControl.Queries.GetSchemeAuditTrail;

/// <summary>Audit trail for a scheme, newest-first (R6.6). Missing id yields [] (R6.7).</summary>
public record GetSchemeAuditTrailQuery(Guid SchemeId) : IRequest<List<NumberingSchemeAuditEntryResponse>>;
