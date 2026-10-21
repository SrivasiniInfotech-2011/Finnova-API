using Finnova.Models.Contracts.GL;
using Finnova.Models.Domain.Entities;
using Finnova.Repository.Interfaces;
using MediatR;

namespace Finnova.Service.GL.Queries.GetGLTree;

public class GetGLTreeQueryHandler : IRequestHandler<GetGLTreeQuery, IReadOnlyList<GLTreeNodeResponse>>
{
    private readonly IGLAccountRepository _repository;
    public GetGLTreeQueryHandler(IGLAccountRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<GLTreeNodeResponse>> Handle(GetGLTreeQuery request, CancellationToken ct)
    {
        var all = await _repository.GetAllForCompanyAsync(request.CompanyId, ct);   // ordered by GLCode (R8.6)
        var byParent = all.ToLookup(x => x.ParentGLId);

        IReadOnlyList<GLTreeNodeResponse> Build(Guid? parentId) =>
            byParent[parentId]
                .OrderBy(x => x.GLCode)                                             // R8.6 sibling order
                .Select(x => new GLTreeNodeResponse(
                    x.Id, x.GLCode, x.Name, x.Level, x.IsPostable, x.Status, Build(x.Id)))
                .ToList();

        return Build(null);   // roots = L1 (ParentGLId null)
    }
}
