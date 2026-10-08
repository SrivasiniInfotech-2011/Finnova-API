using MediatR;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;

namespace Finnova.Service.Assets.ClassCodes.Commands.DeleteClassCode;

public class DeleteClassCodeCommandHandler : IRequestHandler<DeleteClassCodeCommand>
{
    private readonly IClassCodeRepository _repository;
    private readonly IAssetRepository _assets;
    public DeleteClassCodeCommandHandler(IClassCodeRepository repository, IAssetRepository assets)
    {
        _repository = repository;
        _assets = assets;
    }

    public async Task Handle(DeleteClassCodeCommand request, CancellationToken ct)
    {
        var entity = await _repository.GetByIdAsync(request.Id, ct)
            ?? throw new CodeNotFoundException("Class Code", request.Id);    // R6.3 -> 404

        if (request.HardDelete)
        {
            if (await _assets.IsClassCodeReferencedAsync(request.Id, ct))    // R6.4
                throw new CodeInUseException("Class Code");                  // -> 409
            await _repository.DeleteAsync(entity, ct);
            return;
        }

        entity.IsActive = false;                                             // R6.1 soft retire
        entity.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(entity, ct);
    }
}
