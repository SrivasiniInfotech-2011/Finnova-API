using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.GL;
using MediatR;

namespace Finnova.Service.GL.Queries.GetGLAccountsPaged;

public record GetGLAccountsPagedQuery(GLAccountFilter Filter)
    : IRequest<PaginatedResponse<GLAccountResponse>>;
