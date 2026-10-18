using Finnova.Models.Contracts.GL;
using MediatR;

namespace Finnova.Service.GL.Queries.GetSystemGLCompleteness;

public record GetSystemGLCompletenessQuery(Guid CompanyId) : IRequest<SystemGLCompletenessResponse>;
