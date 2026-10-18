using Finnova.Models.Contracts.GL;
using Finnova.Models.Domain.Enums;
using MediatR;

namespace Finnova.Service.GL.Commands.MapSystemGL;

public record MapSystemGLCommand(Guid CompanyId, SystemGLType Type, Guid GLAccountId, string ActingUser)
    : IRequest<SystemGLCompletenessResponse>;
