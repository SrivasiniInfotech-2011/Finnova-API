using Finnova.Models.Contracts.GL;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.GL.Queries.GetGLAccountById;

public class GetGLAccountByIdQueryHandler : IRequestHandler<GetGLAccountByIdQuery, GLAccountResponse>
{
    private readonly IGLAccountRepository _repository;
    public GetGLAccountByIdQueryHandler(IGLAccountRepository repository) => _repository = repository;

    public async Task<GLAccountResponse> Handle(GetGLAccountByIdQuery request, CancellationToken ct)
    {
        var entity = await _repository.GetWithBranchesAsync(request.Id, ct) ?? throw new GLNotFoundException();
        return entity.ToResponse();
    }
}
