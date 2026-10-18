using Finnova.Models.Contracts.GL;
using MediatR;

namespace Finnova.Service.GL.Queries.GetGLTree;

public record GetGLTreeQuery(Guid CompanyId) : IRequest<IReadOnlyList<GLTreeNodeResponse>>;
