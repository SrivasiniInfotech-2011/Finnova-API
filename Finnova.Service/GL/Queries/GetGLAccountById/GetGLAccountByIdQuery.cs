using Finnova.Models.Contracts.GL;
using MediatR;

namespace Finnova.Service.GL.Queries.GetGLAccountById;

public record GetGLAccountByIdQuery(Guid Id) : IRequest<GLAccountResponse>;
