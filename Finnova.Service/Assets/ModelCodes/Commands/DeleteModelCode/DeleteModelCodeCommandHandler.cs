using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using MediatR;

namespace Finnova.Service.Assets.ModelCodes.Commands.DeleteModelCode;

public class DeleteModelCodeCommandHandler(IModelCodeRepository repository, IAssetRepository assets) : IRequestHandler<DeleteModelCodeCommand>
{
    private readonly IModelCodeRepository _repository = repository;
    private readonly IAssetRepository _assets = assets;

    public async Task Handle(DeleteModelCodeCommand request, CancellationToken ct)
    {
        var entity = await _repository.GetByIdAsync(request.Id, ct)
            ?? throw new CodeNotFoundException("Model Code", request.Id);    // R6.3 -> 404

        if (request.HardDelete)
        {
            if (await _assets.IsModelCodeReferencedAsync(request.Id, ct))    // R6.4
                throw new CodeInUseException("Model Code");                  // -> 409
            await _repository.DeleteAsync(entity, ct);
            return;
        }

        entity.IsActive = false;                                             // R6.1 soft retire
        entity.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(entity, ct);
    }
}
