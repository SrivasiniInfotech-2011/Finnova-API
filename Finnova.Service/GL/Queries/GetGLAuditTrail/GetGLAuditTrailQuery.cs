using Finnova.Models.Contracts.GL;
using MediatR;

namespace Finnova.Service.GL.Queries.GetGLAuditTrail;

public record GetGLAuditTrailQuery(Guid GLAccountId) : IRequest<List<GLAuditEntryResponse>>;
